using System.Numerics;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace CubeTest;

public static unsafe class UiGraphics {
	private static ShaderModule* _Shader;

	private static Texture  _Texture = null!;
	private static Sampler* _Sampler;

	private static BindGroupLayout* _TextureSamplerBindGroupLayout;
	private static BindGroup*       _TextureBindGroup;

	private static Buffer* _ProjectionMatrixBuffer;

	private static BindGroupLayout* _ProjectionMatrixBindGroupLayout;
	private static BindGroup*       _ProjectionMatrixBindGroup;

	private static RenderPipeline* _Pipeline;

	private static ulong   _VertexBufferSize;
	private static Buffer* _VertexBuffer;

	public static void Dispose() {
		Graphics.Disposal.Dispose(_VertexBuffer);
		
		Graphics.Disposal.Dispose(_TextureSamplerBindGroupLayout);
		Graphics.Disposal.Dispose(_TextureBindGroup);
		
		Graphics.Disposal.Dispose(_ProjectionMatrixBindGroupLayout);
		Graphics.Disposal.Dispose(_ProjectionMatrixBindGroup);
		
		Graphics.Disposal.Dispose(_Sampler);
		_Texture.Dispose();
		
		Graphics.Disposal.Dispose(_ProjectionMatrixBuffer);
		
		Graphics.Disposal.Dispose(_Pipeline);

		Graphics.Disposal.Dispose(_Shader);
	}

	public static void Initalize() {
		CreateShader();
		
		CreateProjectionMatrixBuffer();
		UpdateProjectionMatrixBuffer();

		CreateTestTexture();
		
		CreateSampler();

		CreateProjectionMatrixBindGroup();
		CreateShaderTextureBindGroup();

		CreatePipeline();

		CreateVertexBuffer();
	}

	private static void CreateVertexBuffer() {
		BufferDescriptor descriptor = new BufferDescriptor {
			Size  = _VertexBufferSize = (ulong)(sizeof(UiVertex) * 6),
			Usage = BufferUsage.Vertex | BufferUsage.CopyDst
		};

		_VertexBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, descriptor);

		UiVertex* data = stackalloc UiVertex[6];

		const float xPos   = 100;
		const float yPos   = 100;
		float       width  = _Texture.Size.X * 8;
		float       height = _Texture.Size.Y * 8;

		//Fill data with a quad with a CCW front face
		data[0] = new UiVertex(new Vector2(xPos, yPos), new Vector2(0, 0));                        //Top left
		data[1] = new UiVertex(new Vector2(xPos + width, yPos), new Vector2(1, 0));                //Top right
		data[2] = new UiVertex(new Vector2(xPos + width, yPos + height), new Vector2(1, 1));       //Bottom right
		data[3] = new UiVertex(new Vector2(xPos, yPos), new Vector2(0, 0));                        //Top left
		data[4] = new UiVertex(new Vector2(xPos       + width, yPos + height), new Vector2(1, 1)); //Bottom right
		data[5] = new UiVertex(new Vector2(xPos, yPos + height), new Vector2(0, 1));               //Bottom left

		//Write the data to the buffer
		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _VertexBuffer, 0, data, (nuint)_VertexBufferSize);
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
			Format    = Graphics.SwapchainFormat,
			Blend     = &blendState,
			WriteMask = ColorWriteMask.All
		};

		FragmentState fragmentState = new FragmentState {
			Module      = _Shader,
			TargetCount = 1,
			Targets     = &colorTargetState,
			EntryPoint  = (byte*)SilkMarshal.StringToPtr("fs_main")
		};

		BindGroupLayout** bindGroupLayouts = stackalloc BindGroupLayout*[2];
		bindGroupLayouts[0] = _TextureSamplerBindGroupLayout;
		bindGroupLayouts[1] = _ProjectionMatrixBindGroupLayout;

		PipelineLayoutDescriptor pipelineLayoutDescriptor = new PipelineLayoutDescriptor {
			BindGroupLayoutCount = 2,
			BindGroupLayouts     = bindGroupLayouts
		};

		PipelineLayout* pipelineLayout = Graphics.WebGPU.DeviceCreatePipelineLayout(Graphics.Device, pipelineLayoutDescriptor);

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
		
		DepthStencilState state = new DepthStencilState {
			Format              = DepthTexture.DepthFormat,
			DepthWriteEnabled   = true,
			DepthCompare        = CompareFunction.Less,
			StencilFront        = new StencilFaceState(CompareFunction.Always, StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep),
			StencilBack         = new StencilFaceState(CompareFunction.Always, StencilOperation.Keep, StencilOperation.Keep, StencilOperation.Keep),
			StencilReadMask     = 0xFFFFFFFF,
			StencilWriteMask    = 0,
			DepthBias           = 0,
			DepthBiasSlopeScale = 0,
			DepthBiasClamp      = 0
		};

		RenderPipelineDescriptor renderPipelineDescriptor = new RenderPipelineDescriptor {
			Vertex = new VertexState {
				Module      = _Shader,
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
			DepthStencil = &state,
			Layout       = pipelineLayout
		};

		_Pipeline = Graphics.WebGPU.DeviceCreateRenderPipeline(Graphics.Device, renderPipelineDescriptor);

		SilkMarshal.Free((nint)renderPipelineDescriptor.Vertex.EntryPoint);
		SilkMarshal.Free((nint)fragmentState.EntryPoint);

		Console.WriteLine($"Created pipeline 0x{(nuint)_Pipeline:x}");
	}

	private static void CreateProjectionMatrixBindGroup() {
		BindGroupLayoutEntry entry = new BindGroupLayoutEntry {
			Binding = 0,
			Buffer = new BufferBindingLayout {
				Type           = BufferBindingType.Uniform,
				MinBindingSize = (ulong)sizeof(Matrix4x4)
			},
			Visibility = ShaderStage.Vertex,
		};

		_ProjectionMatrixBindGroupLayout = Graphics.WebGPU.DeviceCreateBindGroupLayout
		(
			Graphics.Device, new BindGroupLayoutDescriptor {
				Entries    = &entry,
				EntryCount = 1
			}
		);

		BindGroupEntry bindGroupEntry = new BindGroupEntry {
			Binding = 0,
			Buffer  = _ProjectionMatrixBuffer,
			Size    = (ulong)sizeof(Matrix4x4)
		};

		_ProjectionMatrixBindGroup = Graphics.WebGPU.DeviceCreateBindGroup
		(
			Graphics.Device, new BindGroupDescriptor {
				Entries    = &bindGroupEntry,
				EntryCount = 1,
				Layout     = _ProjectionMatrixBindGroupLayout
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

		_TextureSamplerBindGroupLayout = Graphics.WebGPU.DeviceCreateBindGroupLayout(Graphics.Device, layoutDescriptor);

		BindGroupEntry* bindGroupEntries = stackalloc BindGroupEntry[2];
		bindGroupEntries[0] = new BindGroupEntry {
			Binding     = 0,
			TextureView = _Texture.RawTextureView
		};
		bindGroupEntries[1] = new BindGroupEntry {
			Binding = 1,
			Sampler = _Sampler
		};

		BindGroupDescriptor descriptor = new BindGroupDescriptor {
			Entries    = bindGroupEntries,
			EntryCount = 2,
			Layout     = _TextureSamplerBindGroupLayout
		};

		_TextureBindGroup = Graphics.WebGPU.DeviceCreateBindGroup(Graphics.Device, descriptor);
	}

	internal static void UpdateProjectionMatrixBuffer() {
		//Create our projection matrix
		Matrix4x4 projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, Graphics.Window.Size.X, Graphics.Window.Size.Y, 0, 0, 1);

		//Write the projection matrix into the buffer
		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _ProjectionMatrixBuffer, 0, &projectionMatrix, (nuint)sizeof(Matrix4x4));
	}

	private static void CreateProjectionMatrixBuffer() {
		_ProjectionMatrixBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Size = (ulong)sizeof(Matrix4x4),
			//We will be using this buffer as a uniform, and we need CopyDst to write to it using QueueWriteBuffer
			Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
			MappedAtCreation = false
		});
	}

	internal static void TestDraw(RenderPassEncoder* renderPass) {
		Graphics.WebGPU.RenderPassEncoderSetPipeline(renderPass, _Pipeline);
		Graphics.WebGPU.RenderPassEncoderSetBindGroup(renderPass, 0, _TextureBindGroup, 0, null);
		Graphics.WebGPU.RenderPassEncoderSetBindGroup(renderPass, 1, _ProjectionMatrixBindGroup, 0, null);
		Graphics.WebGPU.RenderPassEncoderSetVertexBuffer(renderPass, 0, _VertexBuffer, 0, _VertexBufferSize);
		Graphics.WebGPU.RenderPassEncoderDraw(renderPass, 6, 1, 0, 0);
	}

	private static void CreateSampler() {
		_Sampler = Graphics.WebGPU.DeviceCreateSampler(Graphics.Device, new SamplerDescriptor {
			AddressModeU = AddressMode.Repeat,
			AddressModeV = AddressMode.Repeat,
			AddressModeW = AddressMode.Repeat,
			MagFilter    = FilterMode.Nearest,
			MinFilter    = FilterMode.Nearest,
			MipmapFilter = MipmapFilterMode.Nearest,
			Compare      = CompareFunction.Undefined,
		});
	}

	private static void CreateTestTexture() {
		_Texture = new Texture(ResourceHelpers.ReadResource("Textures/when.png"));
	}

	private static void CreateShader() {
		byte[] shader = ResourceHelpers.ReadResource("Shaders/Ui.wgsl");

		fixed (byte* ptr = shader) {
			ShaderModuleWGSLDescriptor wgslDescriptor = new ShaderModuleWGSLDescriptor {
				Code  = ptr,
				Chain = new ChainedStruct(sType: SType.ShaderModuleWgsldescriptor)
			};

			ShaderModuleDescriptor descriptor = new ShaderModuleDescriptor {
				NextInChain = (ChainedStruct*)(&wgslDescriptor)
			};

			_Shader = Graphics.WebGPU.DeviceCreateShaderModule(Graphics.Device, descriptor);
			Console.WriteLine($"Shader 0x{(nint)_Shader:x8} created!");
		}
	}
}
