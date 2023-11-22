using CubeTest.Abstractions;
using CubeTest.Game.Input;
using CubeTest.Ui;
using CubeTest.World;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using Color = Silk.NET.WebGPU.Color;

namespace CubeTest;

public static unsafe class Graphics {
	public static IWindow Window = null!;

	// ReSharper disable once InconsistentNaming
	public static WebGPU WebGPU = null!;

	public static Instance* Instance;
	public static Adapter*  Adapter;
	public static Device*   Device;
	public static Queue*    Queue;

	public static  Surface*             Surface;
	public static  SurfaceCapabilities  SurfaceCapabilities;
	public static  SurfaceConfiguration SurfaceConfiguration;
	private static DepthTexture         _DepthTexture;
	
	private static InputHandler<FlyInputs> _InputHandler = null!;

	public static void Initialize() {
		//Register GLFW and SDL windowing, for AOT scenarios (like WASM or NativeAOT)
		GlfwWindowing.RegisterPlatform();
		SdlWindowing.RegisterPlatform();

		_InputHandler = new FlyInputHandler();
		_InputHandler.Initialize();
	}

	public static void Run() {
		Window = Silk.NET.Windowing.Window.Create(WindowOptions.Default with {
			API = GraphicsAPI.None,
			ShouldSwapAutomatically = false,
			IsContextControlDisabled = true, Position = new Vector2D<int>(2000, 0)
		});

		Window.Load              += Load;
		Window.Render            += Render;
		Window.FramebufferResize += FramebufferResize;
		Window.Closing           += WindowClosing;
		
		Window.Update += d => {
			_InputHandler.Update((float)d);
		};

		Window.Run();
	}

	public static void Dispose() {
		Window.Dispose();
	}

	private static void WindowClosing() {
		WorldGraphics.Dispose();
		UiGraphics.Dispose();
		WebGPU.DeviceRelease(Device);
	}

	private static void FramebufferResize(Vector2D<int> obj) {
		ConfigureSurface();
		UiGraphics.UpdateProjectionMatrixBuffer();
	}

	private static void Render(double obj)
	{
		SurfaceTexture surfaceTexture;
		WebGPU.SurfaceGetCurrentTexture(Surface, &surfaceTexture);
		
		switch(surfaceTexture.Status) {
			case SurfaceGetCurrentTextureStatus.Lost:
			case SurfaceGetCurrentTextureStatus.Outdated:
			case SurfaceGetCurrentTextureStatus.Timeout:
				WebGPU.TextureRelease(surfaceTexture.Texture);
				ConfigureSurface();
				return; // Skip this frame
				
			case SurfaceGetCurrentTextureStatus.OutOfMemory:
			case SurfaceGetCurrentTextureStatus.DeviceLost:
			case SurfaceGetCurrentTextureStatus.Force32:
				throw new Exception($"Could not get current surface texture: {surfaceTexture.Status}");
		}
		
		TextureView* currentView = WebGPU.TextureCreateView(surfaceTexture.Texture, null);

		// Lets skip this frame, and try again next frame
		if (currentView == null)
			return;

		//Create our command encoder
		CommandEncoder* encoder = WebGPU.DeviceCreateCommandEncoder(Device, new CommandEncoderDescriptor());

		//Create our colour attachment, with a clear value of sky blue
		RenderPassColorAttachment colorAttachment = new RenderPassColorAttachment {
			View          = currentView,
			ResolveTarget = null,
			LoadOp        = LoadOp.Clear,
			StoreOp       = StoreOp.Store,
			ClearValue = new Color {
				R = 70 / 255d,
				G = 179 / 255d,
				B = 234 / 255d,
				A = 1 / 255d,
			}
		};

		RenderPassDepthStencilAttachment depthStencilAttachment = new RenderPassDepthStencilAttachment {
			View            = _DepthTexture.RawTextureView,
			DepthLoadOp     = LoadOp.Clear,
			DepthClearValue = 1,
			DepthStoreOp    = StoreOp.Store,
			DepthReadOnly   = false,
			StencilLoadOp   = LoadOp.Clear,
			StencilStoreOp  = StoreOp.Discard,
			StencilReadOnly = true
		};

		//Create our render pass
		RenderPassEncoder* renderPass = WebGPU.CommandEncoderBeginRenderPass(encoder, new RenderPassDescriptor {
			ColorAttachments       = &colorAttachment,
			ColorAttachmentCount   = 1,
			DepthStencilAttachment = &depthStencilAttachment
		});

		WorldGraphics.Draw(encoder, renderPass);

		//Draws a simple textured quad to the screen
		// UiGraphics.TestDraw(renderPass);

		//End the render pass
		WebGPU.RenderPassEncoderEnd(renderPass);

		//Finish the command encoder
		CommandBuffer* commandBuffer = WebGPU.CommandEncoderFinish(encoder, new CommandBufferDescriptor());

		//Submit the command buffer to the queue
		WebGPU.QueueSubmit(Queue, 1, &commandBuffer);

		//Present the surface
		WebGPU.SurfacePresent(Surface);
		Window.SwapBuffers();
		
		WebGPU.CommandBufferRelease(commandBuffer);
		WebGPU.RenderPassEncoderRelease(renderPass);
		WebGPU.CommandEncoderRelease(encoder);
		WebGPU.TextureViewRelease(currentView);
		WebGPU.TextureRelease(surfaceTexture.Texture);
	}
	
	public static void Load() {
		_InputHandler.Load(Window.CreateInput());
		
		WebGPU = WebGPU.GetApi();

		//Create our instance
		Instance = WebGPU.CreateInstance(new InstanceDescriptor());

		//Create our Surface
		Surface = Window.CreateWebGPUSurface(WebGPU, Instance);

		//Create our adapter
		WebGPU.InstanceRequestAdapter(Instance, new RequestAdapterOptions {
			CompatibleSurface    = Surface,
			PowerPreference      = PowerPreference.HighPerformance,
			ForceFallbackAdapter = false
		}, new PfnRequestAdapterCallback((status, adapter, message, userData) => {
			if (status != RequestAdapterStatus.Success)
				throw new Exception($"Unable to create adapter: {SilkMarshal.PtrToString((nint)message)}");

			Adapter = adapter;
			Console.WriteLine($"Adapter 0x{(nint)Adapter:x8} created!");
		}), null);

		FeatureName name = FeatureName.TimestampQuery;
		
		//Create our device
		WebGPU.AdapterRequestDevice(Adapter, new DeviceDescriptor {
			// RequiredFeaturesCount = 1,
			RequiredFeatures      = &name,
			RequiredLimits        = null,
			DefaultQueue          = default(QueueDescriptor)
		}, new PfnRequestDeviceCallback((status, device, message, userData) => {
			if (status != RequestDeviceStatus.Success)
				throw new Exception($"Unable to create device: {SilkMarshal.PtrToString((nint)message)}");

			Device = device;
			Console.WriteLine($"Device 0x{(nint)Device:x8} created!");
		}), null);
		
		WebGPU.SurfaceGetCapabilities(Surface, Adapter, ref SurfaceCapabilities);

		//Get our Queue to submit things to
		Queue = WebGPU.DeviceGetQueue(Device);

		//Set up our error callbacks
		WebGPU.DeviceSetUncapturedErrorCallback(Device, new PfnErrorCallback(UncapturedError), null);
		// WebGPU.DeviceSetDeviceLostCallback(Device, new PfnDeviceLostCallback(DeviceLost), null);

		ConfigureSurface();

		UiGraphics.Initalize();
		WorldGraphics.Initialize();
	}

	private static void ConfigureSurface() {
		SurfaceConfiguration = new SurfaceConfiguration
		{
			Usage       = TextureUsage.RenderAttachment,
			Device      = Device,
			Format      = SurfaceCapabilities.Formats[0],
			PresentMode = PresentMode.Fifo,
			AlphaMode   = SurfaceCapabilities.AlphaModes[0],
			Width       = (uint)Window.FramebufferSize.X,
			Height      = (uint)Window.FramebufferSize.Y,
		};

		_DepthTexture = new DepthTexture(SurfaceConfiguration.Width, SurfaceConfiguration.Height);
		
		WebGPU.SurfaceConfigure(Surface, in SurfaceConfiguration);
	}

	private static void UncapturedError(ErrorType type, byte* message, void* userData) {
		Console.WriteLine($"Uncaptured error: {SilkMarshal.PtrToString((nint)message)}");
		throw new Exception($"Uncaptured WebGPU error! {SilkMarshal.PtrToString((nint)message)} type: {type}");
	}
}
