using System.Diagnostics;
using System.Numerics;
using CubeTest.Abstractions;
using CubeTest.Helpers;
using CubeTest.ModelLoader;
using CubeTest.ModelLoader.WavefrontObj;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;
using Texture = CubeTest.Abstractions.Texture;

namespace CubeTest.World;

public static unsafe class WorldGraphics {
	private static ShaderModule*    _Shader = null!;
	private static BindGroupLayout* _ProjectionMatrixBindGroupLayout;
	private static BindGroup*       _ProjectionMatrixBindGroup;
	private static BindGroupLayout* _TextureSamplerBindGroupLayout;
	private static BindGroup*       _TextureBindGroup;
	private static RenderPipeline*  _Pipeline;
	private static Texture          _Texture;
	private static Sampler*         _Sampler;
	private static Buffer*          _ProjectionMatrixBuffer;
	private static Buffer*          _ModelMatrixBuffer;
	private static Buffer*          _CameraInfoBuffer;
	private static Buffer*          _LightInfoBuffer;
	private static Model            _Model;
	private static ulong            _VertexBufferSize;
	private static Buffer*          _VertexBuffer;
	private static ulong            _IndexBufferSize;
	private static Buffer*          _IndexBuffer;

	public static  Camera  Camera = new Camera();

	public static void Dispose() {
		Graphics.Disposal.Dispose(_Shader);
	}

	public static void Initialize() {
		CreateShader();

		CreateMatrixBuffers();
		UpdateProjectionMatrixBuffer();

		CreateTestTexture();

		CreateSampler();

		CreateProjectionMatrixBindGroup();
		CreateShaderTextureBindGroup();

		CreatePipeline();

		_Model = new ObjModelLoader().LoadModel(ResourceHelpers.ReadResource("Models/windows.obj"));

		CreateVertexBuffer();
		CreateIndexBuffer();
	}

	private static void CreateVertexBuffer() {
		BufferDescriptor descriptor = new BufferDescriptor {
			Size  = _VertexBufferSize = (ulong)(sizeof(WorldVertex) * _Model.Vertices.Length),
			Usage = BufferUsage.Vertex | BufferUsage.CopyDst
		};

		_VertexBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, descriptor);

		fixed (WorldVertex* data = _Model.Vertices) {
			//Write the data to the buffer
			Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _VertexBuffer, 0, data, (nuint)_VertexBufferSize);
		}
	}
	
	private static void CreateIndexBuffer() {
		BufferDescriptor descriptor = new BufferDescriptor {
			Size  = _IndexBufferSize = (ulong)(sizeof(uint) * _Model.Indices.Length),
			Usage = BufferUsage.Index | BufferUsage.CopyDst
		};

		_IndexBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, descriptor);

		fixed (uint* data = _Model.Indices) {
			//Write the data to the buffer
			Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _IndexBuffer, 0, data, (nuint)_IndexBufferSize);
		}
	}

	internal static void UpdateProjectionMatrixBuffer() {
		ModelMatrix model = new ModelMatrix {
			Model = Matrix4x4.CreateScale(new Vector3(0.25f)) * Matrix4x4.CreateRotationY((Stopwatch.GetTimestamp() / (float)Stopwatch.Frequency) / 4)
		};
		
		if (!Matrix4x4.Invert(model.Model, out Matrix4x4 invert))
			throw new Exception();

		model.Normal = Matrix4x4.Transpose(invert);

		Vector3 direction = new Vector3 {
			X = MathF.Cos(MathHelper.DegToRad(Camera.Yaw)) * MathF.Cos(MathHelper.DegToRad(Camera.Pitch)),
			Y = MathF.Sin(MathHelper.DegToRad(Camera.Pitch)),
			Z = MathF.Sin(MathHelper.DegToRad(Camera.Yaw)) * MathF.Cos(MathHelper.DegToRad(Camera.Pitch))
		};

		Camera.Front = Vector3.Normalize(direction);

		Matrix4x4 view = Matrix4x4.CreateLookAt(Camera.Position, Camera.Position + Camera.Front, Camera.Up);

		CameraInfo cameraInfo = new CameraInfo {
			Position = Camera.Position, 
			View = view
		};

		LightInfo lightInfo = new LightInfo {
			// Position = new Vector3(3, 3, 0),
			Position = Camera.Position,
			Color    = new Vector3(1, 1, 1),
			Ambient  = new Vector3(0.05f),
			Diffuse  = new Vector3(0.5f, 1f, 0.5f), 
			SpecularStrength = 32
		};
		
		//Create our projection matrix
		Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(70f * (MathF.PI / 180f), (float)Graphics.Window.Size.X / Graphics.Window.Size.Y, 0.1f, 100f);

		//Write the projection matrix into the buffer
		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _ProjectionMatrixBuffer, 0, &projectionMatrix, (nuint)sizeof(Matrix4x4));

		//Write the model matrix info
		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _ModelMatrixBuffer, 0, &model, (nuint)sizeof(ModelMatrix));
		
		//Write the camera info
		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _CameraInfoBuffer, 0, &cameraInfo, (nuint)sizeof(CameraInfo));
		
		//Write the light info
		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, _LightInfoBuffer, 0, &lightInfo, (nuint)sizeof(LightInfo));
	}

	private static void CreateMatrixBuffers() {
		_ProjectionMatrixBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Size = (ulong)sizeof(Matrix4x4),
			//We will be using this buffer as a uniform, and we need CopyDst to write to it using QueueWriteBuffer
			Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
			MappedAtCreation = false
		});
		
		_ModelMatrixBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Size = (ulong)sizeof(ModelMatrix),
			//We will be using this buffer as a uniform, and we need CopyDst to write to it using QueueWriteBuffer
			Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
			MappedAtCreation = false
		});
		
		_CameraInfoBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Size = (ulong)sizeof(CameraInfo),
			//We will be using this buffer as a uniform, and we need CopyDst to write to it using QueueWriteBuffer
			Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
			MappedAtCreation = false
		});
		
		_LightInfoBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Size = (ulong)sizeof(LightInfo),
			//We will be using this buffer as a uniform, and we need CopyDst to write to it using QueueWriteBuffer
			Usage            = BufferUsage.Uniform | BufferUsage.CopyDst,
			MappedAtCreation = false
		});
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
		_Texture = new Texture(ResourceHelpers.ReadResource("Textures/T_Atlas.png"));
	}

	private static void CreateProjectionMatrixBindGroup() {
		BindGroupLayoutEntry* entries = stackalloc BindGroupLayoutEntry[4];
		
		entries[0] = new BindGroupLayoutEntry {
			Binding = 0,
			Buffer = new BufferBindingLayout {
				Type           = BufferBindingType.Uniform,
				MinBindingSize = (ulong)sizeof(Matrix4x4)
			},
			Visibility = ShaderStage.Vertex,
		};
		entries[1] = new BindGroupLayoutEntry {
			Binding = 1,
			Buffer = new BufferBindingLayout {
				Type           = BufferBindingType.Uniform,
				MinBindingSize = (ulong)sizeof(ModelMatrix)
			},
			Visibility = ShaderStage.Vertex,
		};
		entries[2] = new BindGroupLayoutEntry {
			Binding = 2,
			Buffer = new BufferBindingLayout {
				Type           = BufferBindingType.Uniform,
				MinBindingSize = (ulong)sizeof(CameraInfo)
			},
			Visibility = ShaderStage.Vertex | ShaderStage.Fragment,
		};
		entries[3] = new BindGroupLayoutEntry {
			Binding = 3,
			Buffer = new BufferBindingLayout {
				Type           = BufferBindingType.Uniform,
				MinBindingSize = (ulong)sizeof(LightInfo)
			},
			Visibility = ShaderStage.Fragment,
		};

		_ProjectionMatrixBindGroupLayout = Graphics.WebGPU.DeviceCreateBindGroupLayout
		(
			Graphics.Device, new BindGroupLayoutDescriptor {
				Entries    = entries,
				EntryCount = 4
			}
		);

		BindGroupEntry* bindGroupEntries = stackalloc BindGroupEntry[4];

		bindGroupEntries[0] = new BindGroupEntry {
			Binding = 0,
			Buffer  = _ProjectionMatrixBuffer,
			Size    = (ulong)sizeof(Matrix4x4)
		};
		bindGroupEntries[1] = new BindGroupEntry {
			Binding = 1,
			Buffer  = _ModelMatrixBuffer,
			Size    = (ulong)sizeof(ModelMatrix)
		};
		bindGroupEntries[2] = new BindGroupEntry {
			Binding = 2,
			Buffer  = _CameraInfoBuffer,
			Size    = (ulong)sizeof(CameraInfo)
		};
		bindGroupEntries[3] = new BindGroupEntry {
			Binding = 3,
			Buffer  = _LightInfoBuffer,
			Size    = (ulong)sizeof(LightInfo)
		};

		_ProjectionMatrixBindGroup = Graphics.WebGPU.DeviceCreateBindGroup
		(
			Graphics.Device, new BindGroupDescriptor {
				Entries    = bindGroupEntries,
				EntryCount = 4,
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

		VertexAttribute* vertexAttributes = stackalloc VertexAttribute[3];

		vertexAttributes[0] = new VertexAttribute {
			Format         = VertexFormat.Float32x3,
			Offset         = 0,
			ShaderLocation = 0
		};
		vertexAttributes[1] = new VertexAttribute {
			Format         = VertexFormat.Float32x3,
			Offset         = (ulong)sizeof(Vector3),
			ShaderLocation = 1
		};
		vertexAttributes[2] = new VertexAttribute {
			Format         = VertexFormat.Float32x2,
			Offset         = (ulong)(sizeof(Vector3) + sizeof(Vector3)),
			ShaderLocation = 2
		};

		VertexBufferLayout vertexBufferLayout = new VertexBufferLayout {
			Attributes     = vertexAttributes,
			AttributeCount = 3,
			StepMode       = VertexStepMode.Vertex,
			ArrayStride    = (ulong)sizeof(WorldVertex)
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
			Layout       = pipelineLayout,
		};

		_Pipeline = Graphics.WebGPU.DeviceCreateRenderPipeline(Graphics.Device, renderPipelineDescriptor);

		SilkMarshal.Free((nint)renderPipelineDescriptor.Vertex.EntryPoint);
		SilkMarshal.Free((nint)fragmentState.EntryPoint);

		Console.WriteLine($"Created pipeline 0x{(nuint)_Pipeline:x}");
	}

	public static void Draw(RenderPassEncoder* renderPass) {
		UpdateProjectionMatrixBuffer();
		
		Graphics.WebGPU.RenderPassEncoderSetPipeline(renderPass, _Pipeline);
		Graphics.WebGPU.RenderPassEncoderSetBindGroup(renderPass, 0, _TextureBindGroup, 0, null);
		Graphics.WebGPU.RenderPassEncoderSetBindGroup(renderPass, 1, _ProjectionMatrixBindGroup, 0, null);
		Graphics.WebGPU.RenderPassEncoderSetVertexBuffer(renderPass, 0, _VertexBuffer, 0, _VertexBufferSize);
		Graphics.WebGPU.RenderPassEncoderSetIndexBuffer(renderPass, _IndexBuffer, IndexFormat.Uint32, 0, _IndexBufferSize);
		Graphics.WebGPU.RenderPassEncoderDrawIndexed(renderPass, (uint)_Model.Indices.Length, 1, 0, 0, 0);
	}
	
	private static void CreateShader() {
		byte[] shader = ResourceHelpers.ReadResource("Shaders/World.wgsl");

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
