using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
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
using Color = Silk.NET.WebGPU.Color;

namespace CubeTest;

public static unsafe class Graphics {
	public static IWindow Window = null!;

	// ReSharper disable once InconsistentNaming
	public static WebGPU WebGPU = null!;
	// ReSharper disable once InconsistentNaming
	public static WebGPUDisposal Disposal = null!;

	public static Instance* Instance;
	public static Adapter*  Adapter;
	public static Device*   Device;
	public static Queue*    Queue;

	public static Surface*      Surface;
	public static TextureFormat SwapchainFormat;
	public static SwapChain*    Swapchain;

	private static ShaderModule* UiShader;

	public static Texture  UiTexture;
	public static Sampler* UiSampler;

	private static BindGroupLayout* UiTextureSamplerBindGroupLayout;
	private static BindGroup*       UiTextureBindGroup;

	private static Buffer* UiProjectionMatrixBuffer;

	private static BindGroupLayout* UiProjectionMatrixBindGroupLayout;
	private static BindGroup*       UiProjectionMatrixBindGroup;

	private static RenderPipeline* UiPipeline;

	private static ulong   UiVertexBufferSize;
	private static Buffer* UiVertexBuffer;

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
			IsContextControlDisabled = true
		});

		Window.Load              += Load;
		Window.Render            += Render;
		Window.FramebufferResize += FramebufferResize;
		Window.Closing           += WindowClosing;

		Window.Run();
	}

	public static void Dispose() {
		Window.Dispose();
	}

	private static void WindowClosing() {
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
		
		WebGPU.RenderPassEncoderSetPipeline(renderPass, UiPipeline);
		WebGPU.RenderPassEncoderSetBindGroup(renderPass, 0, UiTextureBindGroup, 0, null);
		WebGPU.RenderPassEncoderSetBindGroup(renderPass, 1, UiProjectionMatrixBindGroup, 0, null);
		WebGPU.RenderPassEncoderSetVertexBuffer(renderPass, 0, UiVertexBuffer, 0, UiVertexBufferSize);
		WebGPU.RenderPassEncoderDraw(renderPass, 6, 1, 0, 0);

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

		//Get our Queue to submit things to
		Queue = WebGPU.DeviceGetQueue(Device);

		//Create our disposal extension
		Disposal = new WebGPUDisposal(WebGPU);

		//Set up our error callbacks
		WebGPU.DeviceSetUncapturedErrorCallback(Device, new PfnErrorCallback(UncapturedError), null);
		WebGPU.DeviceSetDeviceLostCallback(Device, new PfnDeviceLostCallback(DeviceLost), null);

		CreateShaders();

		CreateSwapchain();

		CreateUiProjectionMatrixBuffer();
		UpdateUiProjectionMatrixBuffer();

		UiTexture = new Texture(ResourceHelpers.ReadResource("Textures/when.png"));

		UiSampler = WebGPU.DeviceCreateSampler(Device, new SamplerDescriptor {
			AddressModeU = AddressMode.Repeat,
			AddressModeV = AddressMode.Repeat,
			AddressModeW = AddressMode.Repeat,
			MagFilter    = FilterMode.Nearest,
			MinFilter    = FilterMode.Nearest,
			MipmapFilter = MipmapFilterMode.Nearest,
			Compare      = CompareFunction.Undefined,
		});

		CreateUiProjectionMatrixBindGroup();
		CreateShaderTextureBindGroup();

		CreatePipeline();

		CreateVertexBuffer();
	}

	private static void CreateVertexBuffer() {
		BufferDescriptor descriptor = new BufferDescriptor {
			Size  = UiVertexBufferSize = (ulong)(sizeof(UiVertex) * 6),
			Usage = BufferUsage.Vertex | BufferUsage.CopyDst
		};

		UiVertexBuffer = WebGPU.DeviceCreateBuffer(Device, descriptor);

		UiVertex* data = stackalloc UiVertex[6];

		const float xPos   = 100;
		const float yPos   = 100;
		float width  = UiTexture.Size.X * 8;
		float height = UiTexture.Size.Y * 8;

		//Fill data with a quad with a CCW front face
		data[0] = new UiVertex(new Vector2(xPos, yPos), new Vector2(0, 0));                        //Top left
		data[1] = new UiVertex(new Vector2(xPos + width, yPos), new Vector2(1, 0));                //Top right
		data[2] = new UiVertex(new Vector2(xPos + width, yPos + height), new Vector2(1, 1));       //Bottom right
		data[3] = new UiVertex(new Vector2(xPos, yPos), new Vector2(0, 0));                        //Top left
		data[4] = new UiVertex(new Vector2(xPos       + width, yPos + height), new Vector2(1, 1)); //Bottom right
		data[5] = new UiVertex(new Vector2(xPos, yPos + height), new Vector2(0, 1));               //Bottom left

		//Write the data to the buffer
		WebGPU.QueueWriteBuffer(Queue, UiVertexBuffer, 0, data, (nuint)UiVertexBufferSize);
	}

	private static void CreatePipeline() {
		BlendState blendState = new BlendState {
			Color = new BlendComponent {
				SrcFactor = BlendFactor.SrcAlpha,
				DstFactor = BlendFactor.OneMinusSrcAlpha,
				Operation = BlendOperation.Add
			},
			Alpha = new BlendComponent {
				SrcFactor = BlendFactor.One,
				DstFactor = BlendFactor.OneMinusSrcAlpha,
				Operation = BlendOperation.Add
			}
		};

		ColorTargetState colorTargetState = new ColorTargetState {
			Format    = SwapchainFormat,
			Blend     = &blendState,
			WriteMask = ColorWriteMask.All
		};

		FragmentState fragmentState = new FragmentState {
			Module      = UiShader,
			TargetCount = 1,
			Targets     = &colorTargetState,
			EntryPoint  = (byte*)SilkMarshal.StringToPtr("fs_main")
		};

		BindGroupLayout** bindGroupLayouts = stackalloc BindGroupLayout*[2];
		bindGroupLayouts[0] = UiTextureSamplerBindGroupLayout;
		bindGroupLayouts[1] = UiProjectionMatrixBindGroupLayout;

		PipelineLayoutDescriptor pipelineLayoutDescriptor = new PipelineLayoutDescriptor {
			BindGroupLayoutCount = 2,
			BindGroupLayouts     = bindGroupLayouts
		};

		PipelineLayout* pipelineLayout = WebGPU.DeviceCreatePipelineLayout(Device, pipelineLayoutDescriptor);

		VertexAttribute* vertexAttributes = stackalloc VertexAttribute[2];

		vertexAttributes[0] = new VertexAttribute {
			Format         = VertexFormat.Float32x2,
			Offset         = 0,
			ShaderLocation = 0
		};
		vertexAttributes[1] = new VertexAttribute {
			Format         = VertexFormat.Float32x2,
			Offset         = (ulong)sizeof(Vector2),
			ShaderLocation = 1
		};

		VertexBufferLayout vertexBufferLayout = new VertexBufferLayout {
			Attributes     = vertexAttributes,
			AttributeCount = 2,
			StepMode       = VertexStepMode.Vertex,
			ArrayStride    = (ulong)sizeof(UiVertex)
		};

		RenderPipelineDescriptor renderPipelineDescriptor = new RenderPipelineDescriptor {
			Vertex = new VertexState {
				Module      = UiShader,
				EntryPoint  = (byte*)SilkMarshal.StringToPtr("vs_main"),
				Buffers     = &vertexBufferLayout,
				BufferCount = 1
			},
			Primitive = new PrimitiveState {
				Topology         = PrimitiveTopology.TriangleList,
				StripIndexFormat = IndexFormat.Undefined,
				FrontFace        = FrontFace.Ccw,
				CullMode         = CullMode.None
			},
			Multisample = new MultisampleState {
				Count                  = 1,
				Mask                   = ~0u,
				AlphaToCoverageEnabled = false
			},
			Fragment     = &fragmentState,
			DepthStencil = null,
			Layout       = pipelineLayout
		};

		UiPipeline = WebGPU.DeviceCreateRenderPipeline(Device, renderPipelineDescriptor);

		SilkMarshal.Free((nint)renderPipelineDescriptor.Vertex.EntryPoint);
		SilkMarshal.Free((nint)fragmentState.EntryPoint);

		Console.WriteLine($"Created pipeline 0x{(nuint)UiPipeline:x}");
	}

	private static void CreateUiProjectionMatrixBindGroup() {
		BindGroupLayoutEntry entry = new BindGroupLayoutEntry {
			Binding = 0,
			Buffer = new BufferBindingLayout {
				Type           = BufferBindingType.Uniform,
				MinBindingSize = (ulong)sizeof(Matrix4x4)
			},
			Visibility = ShaderStage.Vertex,
		};

		UiProjectionMatrixBindGroupLayout = WebGPU.DeviceCreateBindGroupLayout
		(
			Device, new BindGroupLayoutDescriptor {
				Entries    = &entry,
				EntryCount = 1
			}
		);

		BindGroupEntry bindGroupEntry = new BindGroupEntry {
			Binding = 0,
			Buffer  = UiProjectionMatrixBuffer,
			Size    = (ulong)sizeof(Matrix4x4)
		};

		UiProjectionMatrixBindGroup = WebGPU.DeviceCreateBindGroup
		(
			Device, new BindGroupDescriptor {
				Entries    = &bindGroupEntry,
				EntryCount = 1,
				Layout     = UiProjectionMatrixBindGroupLayout
			}
		);
	}

	private static void CreateShaderTextureBindGroup() {
		BindGroupLayoutEntry* entries = stackalloc BindGroupLayoutEntry[2];
		entries[0] = new BindGroupLayoutEntry {
			Binding = 0,
			Texture = new TextureBindingLayout {
				Multisampled  = false,
				SampleType    = TextureSampleType.Float,
				ViewDimension = TextureViewDimension.TextureViewDimension2D
			},
			Visibility = ShaderStage.Fragment
		};
		entries[1] = new BindGroupLayoutEntry {
			Binding = 1,
			Sampler = new SamplerBindingLayout {
				Type = SamplerBindingType.Filtering
			},
			Visibility = ShaderStage.Fragment
		};

		BindGroupLayoutDescriptor layoutDescriptor = new BindGroupLayoutDescriptor {
			Entries    = entries,
			EntryCount = 2
		};

		UiTextureSamplerBindGroupLayout = WebGPU.DeviceCreateBindGroupLayout(Device, layoutDescriptor);

		BindGroupEntry* bindGroupEntries = stackalloc BindGroupEntry[2];
		bindGroupEntries[0] = new BindGroupEntry {
			Binding     = 0,
			TextureView = UiTexture.RawTextureView
		};
		bindGroupEntries[1] = new BindGroupEntry {
			Binding = 1,
			Sampler = UiSampler
		};

		BindGroupDescriptor descriptor = new BindGroupDescriptor {
			Entries    = bindGroupEntries,
			EntryCount = 2,
			Layout     = UiTextureSamplerBindGroupLayout
		};

		UiTextureBindGroup = WebGPU.DeviceCreateBindGroup(Device, descriptor);
	}

	private static void UpdateUiProjectionMatrixBuffer() {
		//Create our projection matrix
		Matrix4x4 projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, Window.Size.X, Window.Size.Y, 0, 0, 1);

		//Write the projection matrix into the buffer
		WebGPU.QueueWriteBuffer(Queue, UiProjectionMatrixBuffer, 0, &projectionMatrix, (nuint)sizeof(Matrix4x4));

		//Get a command encoder
		CommandEncoder* commandEncoder = WebGPU.DeviceCreateCommandEncoder(Device, new CommandEncoderDescriptor());

		//Finish the command encoder to get a command buffer
		CommandBuffer* commandBuffer = WebGPU.CommandEncoderFinish(commandEncoder, new CommandBufferDescriptor());

		//Submit the queue to the GPU
		WebGPU.QueueSubmit(Queue, 1, &commandBuffer);
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
