using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Silk.NET.Core.Native;
using Silk.NET.Input.Glfw;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Disposal;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace CubeTest;

public static unsafe class Graphics {
	public static IWindow Window;

	// ReSharper disable once InconsistentNaming
	public static WebGPU WebGPU = null!;
	// ReSharper disable once InconsistentNaming
	public static WebGPUDisposal Disposal = null!;

	public static Instance* Instance;
	public static Adapter*  Adapter;
	public static Device*   Device;

	public static Surface*      Surface;
	public static TextureFormat SwapchainFormat;
	public static SwapChain*    Swapchain;

	private static ShaderModule* UiShader;

	private static Buffer* UiProjectionMatrixBuffer;

	public static void Initialize() {
		//Register GLFW and SDL windowing/input, for AOT scenarios (like WASM or NativeAOT)
		GlfwWindowing.RegisterPlatform();
		SdlWindowing.RegisterPlatform();

		GlfwInput.RegisterPlatform();
		SdlInput.RegisterPlatform();

		Window = Silk.NET.Windowing.Window.Create(WindowOptions.Default with {
			API = GraphicsAPI.None,
			ShouldSwapAutomatically = false,
			IsContextControlDisabled = true
		});

		Window.Load              += Load;
		Window.Render            += Render;
		Window.FramebufferResize += FramebufferResize;
		Window.Closing           += Dispose;
	}

	private static void Dispose() {
		Disposal.Dispose(UiProjectionMatrixBuffer);
		Disposal.Dispose(UiShader);
		Disposal.Dispose(Device);
	}

	private static void FramebufferResize(Vector2D<int> obj) {
		CreateSwapchain();
		UpdateUiProjectionMatrixBuffer();
	}

	private static void Render(double obj) {
		TextureView* nextView = GetNextTextureView();

		//Lets skip this frame, and try again next frame
		if (nextView == null) {
			return;
		}

		//Create our command encoder
		CommandEncoder* encoder = WebGPU.DeviceCreateCommandEncoder(Device, new CommandEncoderDescriptor());

		//Create our colour attatchment, with a clear value of green
		RenderPassColorAttachment colorAttachment = new RenderPassColorAttachment {
			View          = nextView,
			ResolveTarget = null,
			LoadOp        = LoadOp.Clear,
			StoreOp       = StoreOp.Store,
			ClearValue = new Color {
				R = 0,
				G = 1,
				B = 0,
				A = 1
			}
		};

		//Create our render pass
		RenderPassEncoder* renderPass = WebGPU.CommandEncoderBeginRenderPass(encoder, new RenderPassDescriptor {
			ColorAttachments       = &colorAttachment,
			ColorAttachmentCount   = 1,
			DepthStencilAttachment = null
		});

		//End the render pass
		WebGPU.RenderPassEncoderEnd(renderPass);
		//Dispose of the TextureView* of the SwapChain
		Disposal.Dispose(nextView);

		//Get the device queue
		Queue* queue = WebGPU.DeviceGetQueue(Device);

		//Finish the command encoder
		CommandBuffer* commandBuffer = WebGPU.CommandEncoderFinish(encoder, new CommandBufferDescriptor());

		//Submit the command buffer to the queue
		WebGPU.QueueSubmit(queue, 1, &commandBuffer);

		//Present the swapchain
		WebGPU.SwapChainPresent(Swapchain);
		Window.SwapBuffers();
	}

	[return: MaybeNull]
	private static TextureView* GetNextTextureView() {
		TextureView* nextView = null;

		for (int attempt = 0; attempt < 2; attempt++) {
			nextView = WebGPU.SwapChainGetCurrentTextureView(Swapchain);

			if (attempt == 0 && nextView == null) {
				Console.WriteLine("Getting swapchain TextureView* failed, creating a new swapchain to see if that helps...\n");
				CreateSwapchain();
				continue;
			}

			break;
		}

		if (nextView == null) {
			Console.WriteLine("Failed to get TextureView* for SwapChain* after multiple attempts; giving up.\n");
			return nextView;
		}

		return nextView;
	}

	public static void Load() {
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
			if (status != RequestAdapterStatus.Success) {
				throw new Exception($"Unable to create adapter: {SilkMarshal.PtrToString((nint)message)}");
			}

			Adapter = adapter;
			Console.WriteLine($"Adapter 0x{(nint)Adapter:x8} created!");
		}), null);

		//Create our device
		WebGPU.AdapterRequestDevice(Adapter, null, new PfnRequestDeviceCallback((status, device, message, userData) => {
			if (status != RequestDeviceStatus.Success) {
				throw new Exception($"Unable to create device: {SilkMarshal.PtrToString((nint)message)}");
			}

			Device = device;
			Console.WriteLine($"Device 0x{(nint)Device:x8} created!");
		}), null);

		//Create our disposal extension
		Disposal = new WebGPUDisposal(WebGPU);

		//Set up our error callbacks
		WebGPU.DeviceSetUncapturedErrorCallback(Device, new PfnErrorCallback(UncapturedError), null);
		WebGPU.DeviceSetDeviceLostCallback(Device, new PfnDeviceLostCallback(DeviceLost), null);

		CreateShaders();

		CreateSwapchain();

		CreateUiProjectionMatrixBuffer();
		UpdateUiProjectionMatrixBuffer();
	}

	private static void UpdateUiProjectionMatrixBuffer() {
		//Get a device queue
		Queue* queue = WebGPU.DeviceGetQueue(Device);

		//Create our projection matrix
		Matrix4x4 projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, Window.Size.X, Window.Size.Y, 0, 0, 1);

		//Write the projection matrix into the buffer
		WebGPU.QueueWriteBuffer(queue, UiProjectionMatrixBuffer, 0, &projectionMatrix, (nuint)sizeof(Matrix4x4));

		//Get a command encoder
		CommandEncoder* commandEncoder = WebGPU.DeviceCreateCommandEncoder(Device, new CommandEncoderDescriptor());

		//Finish the command encoder to get a command buffer
		CommandBuffer* commandBuffer = WebGPU.CommandEncoderFinish(commandEncoder, new CommandBufferDescriptor());

		//Submit the queue to the GPU
		WebGPU.QueueSubmit(queue, 1, &commandBuffer);
	}

	private static void CreateUiProjectionMatrixBuffer() {
		UiProjectionMatrixBuffer = WebGPU.DeviceCreateBuffer(Device, new BufferDescriptor {
			Size = (ulong)sizeof(Matrix4x4),
			//We will be using this buffer as a uniform, and we need CopyDst to write to it using QueueWriteBuffer
			Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
			MappedAtCreation = false
		});
	}

	private static void CreateSwapchain() {
		SwapchainFormat = WebGPU.SurfaceGetPreferredFormat(Surface, Adapter);

		SwapChainDescriptor swapChainDescriptor = new SwapChainDescriptor {
			Usage       = TextureUsage.RenderAttachment,
			Format      = SwapchainFormat,
			Width       = (uint)Window.FramebufferSize.X,
			Height      = (uint)Window.FramebufferSize.Y,
			PresentMode = PresentMode.Fifo
		};

		Swapchain = WebGPU.DeviceCreateSwapChain(Device, Surface, swapChainDescriptor);
	}

	private static void CreateShaders() {
		byte[] shader = ResourceHelpers.ReadResource("Shaders/Ui.wgsl");

		fixed (byte* ptr = shader) {
			ShaderModuleWGSLDescriptor wgslDescriptor = new ShaderModuleWGSLDescriptor {
				Code  = ptr,
				Chain = new ChainedStruct(sType: SType.ShaderModuleWgsldescriptor)
			};

			ShaderModuleDescriptor descriptor = new ShaderModuleDescriptor {
				NextInChain = (ChainedStruct*)(&wgslDescriptor)
			};

			UiShader = WebGPU.DeviceCreateShaderModule(Device, descriptor);
			Console.WriteLine($"Shader 0x{(nint)UiShader:x8} created!");
		}
	}

	private static void DeviceLost(DeviceLostReason reason, byte* message, void* userData) {
		throw new Exception($"WebGPU Device lost! {SilkMarshal.PtrToString((nint)message)} reason: {reason}");
	}

	private static void UncapturedError(ErrorType type, byte* message, void* userData) {
		throw new Exception($"Uncaptured WebGPU error! {SilkMarshal.PtrToString((nint)message)} type: {type}");
	}
}
