using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using CubeTest.Abstractions;
using CubeTest.Ui;
using CubeTest.World;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Input.Sdl;
using Silk.NET.Maths;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Disposal;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using Color = Silk.NET.WebGPU.Color;

namespace CubeTest;

public static unsafe class Graphics {
	public static  IWindow       Window = null!;
	private static IInputContext Input  = null!;

	// ReSharper disable once InconsistentNaming
	public static WebGPU WebGPU = null!;
	// ReSharper disable once InconsistentNaming
	public static WebGPUDisposal Disposal = null!;

	public static Instance* Instance;
	public static Adapter*  Adapter;
	public static Device*   Device;
	public static Queue*    Queue;

	public static  Surface*      Surface;
	public static  TextureFormat SwapchainFormat;
	public static  SwapChain*    Swapchain;
	private static DepthTexture  _DepthTexture;

	public static void Initialize() {
		//Register GLFW and SDL windowing/input, for AOT scenarios (like WASM or NativeAOT)
		GlfwWindowing.RegisterPlatform();
		SdlWindowing.RegisterPlatform();

		GlfwInput.RegisterPlatform();
		SdlInput.RegisterPlatform();
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

		Vector2 last = Vector2.Zero;
		Window.Update += d => {
			foreach (IMouse mouse in Input.Mice) {
				if (mouse.IsButtonPressed(MouseButton.Left))
				{
					mouse.Cursor.CursorMode = CursorMode.Raw;
			
					WorldGraphics.Camera.Pitch -= (mouse.Position.Y - last.Y) * 0.1f;
					WorldGraphics.Camera.Yaw   += (mouse.Position.X - last.X) * 0.1f;
				}
				else
				{
					mouse.Cursor.CursorMode = CursorMode.Normal;
				}
				
				last = mouse.Position;
			}

			foreach (IKeyboard kb in Input.Keyboards) {
				if (kb.IsKeyPressed(Key.A))
					WorldGraphics.Camera.Position -= Vector3.Normalize(Vector3.Cross(WorldGraphics.Camera.Front, WorldGraphics.Camera.Up)) * (float)d;
				if (kb.IsKeyPressed(Key.D))
					WorldGraphics.Camera.Position += Vector3.Normalize(Vector3.Cross(WorldGraphics.Camera.Front, WorldGraphics.Camera.Up)) * (float)d;

				if (kb.IsKeyPressed(Key.ShiftLeft))
					WorldGraphics.Camera.Position -= WorldGraphics.Camera.Up * (float)d;
				if (kb.IsKeyPressed(Key.Space))
					WorldGraphics.Camera.Position += WorldGraphics.Camera.Up * (float)d;

				if (kb.IsKeyPressed(Key.W))
					WorldGraphics.Camera.Position += WorldGraphics.Camera.Front * (float)d;
				if (kb.IsKeyPressed(Key.S))
					WorldGraphics.Camera.Position -= WorldGraphics.Camera.Front * (float)d;
				
				float cameraSpeed = 150f;
				if (kb.IsKeyPressed(Key.ControlLeft))
					cameraSpeed /= 2;
				
				if(kb.IsKeyPressed(Key.Up))
					WorldGraphics.Camera.Pitch += cameraSpeed * (float)d;
				if(kb.IsKeyPressed(Key.Down))
					WorldGraphics.Camera.Pitch -= cameraSpeed * (float)d;
				if(kb.IsKeyPressed(Key.Left))
					WorldGraphics.Camera.Yaw -= cameraSpeed * (float)d;
				if(kb.IsKeyPressed(Key.Right))
					WorldGraphics.Camera.Yaw += cameraSpeed * (float)d;
			}
			
			foreach (IGamepad gamepad in Input.Gamepads)
			{
				gamepad.Deadzone = new Deadzone(0.15f, DeadzoneMethod.Traditional);
				Thumbstick leftStick = gamepad.Thumbsticks[0];
				WorldGraphics.Camera.Position += Vector3.Normalize(Vector3.Cross(WorldGraphics.Camera.Front, WorldGraphics.Camera.Up)) * (float)d * leftStick.X;
				WorldGraphics.Camera.Position -= WorldGraphics.Camera.Front * (float)d * leftStick.Y;
				
				Thumbstick rightStick = gamepad.Thumbsticks[1];
				WorldGraphics.Camera.Yaw += 150f * rightStick.X * (float)d;
				WorldGraphics.Camera.Pitch -= 150f * rightStick.Y * (float)d;
				
				// Up/down
				if(gamepad.A().Pressed)
					WorldGraphics.Camera.Position += WorldGraphics.Camera.Up * (float)d;
				if(gamepad.B().Pressed)
					WorldGraphics.Camera.Position -= WorldGraphics.Camera.Up * (float)d;
			}
			
			WorldGraphics.Camera.Pitch = Math.Clamp(WorldGraphics.Camera.Pitch, -89.99f, 89.99f);
		};

		Window.Run();
	}

	public static void Dispose() {
		Window.Dispose();
	}

	private static void WindowClosing() {
		WorldGraphics.Dispose();
		UiGraphics.Dispose();
		Disposal.Dispose(Device);
	}

	private static void FramebufferResize(Vector2D<int> obj) {
		CreateSwapchain();
		UiGraphics.UpdateProjectionMatrixBuffer();
	}

	private static void Render(double obj) {
		TextureView* nextView = GetNextTextureView();

		//Lets skip this frame, and try again next frame
		if (nextView == null)
			return;

		//Create our command encoder
		CommandEncoder* encoder = WebGPU.DeviceCreateCommandEncoder(Device, new CommandEncoderDescriptor());

		//Create our colour attachment, with a clear value of sky blue
		RenderPassColorAttachment colorAttachment = new RenderPassColorAttachment {
			View          = nextView,
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
		//Dispose of the TextureView* of the SwapChain
		Disposal.Dispose(nextView);

		//Finish the command encoder
		CommandBuffer* commandBuffer = WebGPU.CommandEncoderFinish(encoder, new CommandBufferDescriptor());

		//Submit the command buffer to the queue
		WebGPU.QueueSubmit(Queue, 1, &commandBuffer);

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
		Input = Window.CreateInput();

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

		//Create our device
		WebGPU.AdapterRequestDevice(Adapter, null, new PfnRequestDeviceCallback((status, device, message, userData) => {
			if (status != RequestDeviceStatus.Success)
				throw new Exception($"Unable to create device: {SilkMarshal.PtrToString((nint)message)}");

			Device = device;
			Console.WriteLine($"Device 0x{(nint)Device:x8} created!");
		}), null);

		//Get our Queue to submit things to
		Queue = WebGPU.DeviceGetQueue(Device);

		//Create our disposal extension
		Disposal = new WebGPUDisposal(WebGPU);

		//Set up our error callbacks
		WebGPU.DeviceSetUncapturedErrorCallback(Device, new PfnErrorCallback(UncapturedError), null);
		WebGPU.DeviceSetDeviceLostCallback(Device, new PfnDeviceLostCallback(DeviceLost), null);

		CreateSwapchain();

		UiGraphics.Initalize();
		WorldGraphics.Initialize();
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

		_DepthTexture = new DepthTexture(swapChainDescriptor.Width, swapChainDescriptor.Height);
	}

	private static void DeviceLost(DeviceLostReason reason, byte* message, void* userData) {
		throw new Exception($"WebGPU Device lost! {SilkMarshal.PtrToString((nint)message)} reason: {reason}");
	}

	private static void UncapturedError(ErrorType type, byte* message, void* userData) {
		Console.WriteLine($"Uncaptured error: {SilkMarshal.PtrToString((nint)message)}");
		throw new Exception($"Uncaptured WebGPU error! {SilkMarshal.PtrToString((nint)message)} type: {type}");
	}
}
