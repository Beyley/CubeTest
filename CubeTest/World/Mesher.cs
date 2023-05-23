using CubeTest.Game;
using CubeTest.Helpers;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace CubeTest.World;

public static unsafe class Mesher {
	public static Buffer* VertexOutputBuffer;
	public static Buffer* IndexOutputBuffer;
	public static Buffer* BlocksBuffer;
	public static Buffer* CountsBuffer;
	public static Buffer* ChunkXBuffer;
	public static Buffer* ChunkYBuffer;

	public static ulong VertexOutputBufferSize;
	public static ulong IndexOutputBufferSize;
	public static ulong BlocksBufferSize;
	public static ulong CountsBufferSize;

	public static ComputePipeline* MeshPipeline;
	public static ShaderModule*    MeshShader;
	public static BindGroupLayout* MeshBindGroupLayout;
	public static BindGroup*       MeshBindGroup;

	public static void Initialize() {
		CreateOutputBuffers();
		CreateBlocksBuffer();
		CreateCountsBuffer();
		CreateChunkLocationBuffers();

		CreateBindGroupLayout();
		CreateBindGroup();

		CreateShader();
		CreatePipeline();
	}

	public static void Dispose() {
		Graphics.Disposal.Dispose(VertexOutputBuffer);
		Graphics.Disposal.Dispose(IndexOutputBuffer);
		Graphics.Disposal.Dispose(BlocksBuffer);
		Graphics.Disposal.Dispose(CountsBuffer);

		Graphics.Disposal.Dispose(MeshBindGroupLayout);
		Graphics.Disposal.Dispose(MeshBindGroup);
		Graphics.Disposal.Dispose(MeshShader);
		Graphics.Disposal.Dispose(MeshPipeline);
	}

	private static void CreateBindGroupLayout() {
		const int entryCount = 6;

		BindGroupLayoutEntry* entries = stackalloc BindGroupLayoutEntry[entryCount];
		entries[0] = new BindGroupLayoutEntry {
			Binding    = 0,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				NextInChain      = null,
				Type             = BufferBindingType.Storage,
				HasDynamicOffset = false,
				MinBindingSize   = VertexOutputBufferSize
			}
		};
		entries[1] = new BindGroupLayoutEntry {
			Binding    = 1,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				Type             = BufferBindingType.Storage,
				HasDynamicOffset = false,
				MinBindingSize   = IndexOutputBufferSize
			}
		};
		entries[2] = new BindGroupLayoutEntry {
			Binding    = 2,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				Type             = BufferBindingType.ReadOnlyStorage,
				HasDynamicOffset = false,
				MinBindingSize   = BlocksBufferSize
			}
		};
		entries[3] = new BindGroupLayoutEntry {
			Binding    = 3,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				Type             = BufferBindingType.Storage,
				HasDynamicOffset = false,
				MinBindingSize   = CountsBufferSize
			}
		};
		entries[4] = new BindGroupLayoutEntry {
			Binding    = 4,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				Type             = BufferBindingType.ReadOnlyStorage,
				HasDynamicOffset = false,
				MinBindingSize   = sizeof(int)
			}
		};
		entries[5] = new BindGroupLayoutEntry {
			Binding    = 5,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				Type             = BufferBindingType.ReadOnlyStorage,
				HasDynamicOffset = false,
				MinBindingSize   = sizeof(int)
			}
		};

		MeshBindGroupLayout = Graphics.WebGPU.DeviceCreateBindGroupLayout(Graphics.Device, new BindGroupLayoutDescriptor {
			EntryCount = entryCount,
			Entries    = entries
		});
	}

	private static void CreateBindGroup() {
		const int entryCount = 6;

		BindGroupEntry* entries = stackalloc BindGroupEntry[entryCount];

		entries[0] = new BindGroupEntry {
			Binding = 0,
			Buffer  = VertexOutputBuffer,
			Offset  = 0,
			Size    = VertexOutputBufferSize
		};
		entries[1] = new BindGroupEntry {
			Binding = 1,
			Buffer  = IndexOutputBuffer,
			Offset  = 0,
			Size    = IndexOutputBufferSize
		};
		entries[2] = new BindGroupEntry {
			Binding = 2,
			Buffer  = BlocksBuffer,
			Offset  = 0,
			Size    = BlocksBufferSize
		};
		entries[3] = new BindGroupEntry {
			Binding = 3,
			Buffer  = CountsBuffer,
			Offset  = 0,
			Size    = CountsBufferSize
		};
		entries[4] = new BindGroupEntry {
			Binding = 4,
			Buffer  = ChunkXBuffer,
			Offset  = 0,
			Size    = sizeof(int)
		};
		entries[5] = new BindGroupEntry {
			Binding = 5,
			Buffer  = ChunkYBuffer,
			Offset  = 0,
			Size    = sizeof(int)
		};

		MeshBindGroup = Graphics.WebGPU.DeviceCreateBindGroup(Graphics.Device, new BindGroupDescriptor {
			Layout     = MeshBindGroupLayout,
			EntryCount = entryCount,
			Entries    = entries
		});
	}

	private struct AtomicCounts {
		public uint IndexCount;
		public uint InstanceCount;
		public uint FirstIndex;
		public uint BaseVertex;
		public uint FirstInstance;
		public uint VertexCount;
	}

	private static void CreateCountsBuffer() {
		CountsBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Usage            = BufferUsage.Storage | BufferUsage.CopyDst | BufferUsage.Indirect,
			Size             = CountsBufferSize = (ulong)sizeof(AtomicCounts),
			MappedAtCreation = false
		});

		Console.WriteLine($"Created mesh counts buffer 0x{(nint)CountsBuffer:x}");
	}

	private static void CreateChunkLocationBuffers()
	{
		ChunkXBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Usage            = BufferUsage.Storage | BufferUsage.CopyDst,
			Size             = sizeof(int),
			MappedAtCreation = false
		});

		Console.WriteLine($"Created Chunk X buffer 0x{(nint)CountsBuffer:x}");
		
		ChunkYBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Usage            = BufferUsage.Storage | BufferUsage.CopyDst,
			Size             = sizeof(int),
			MappedAtCreation = false
		});

		Console.WriteLine($"Created Chunk Y buffer 0x{(nint)CountsBuffer:x}");
	}

	public static void ResetCounts() {
		AtomicCounts atomicCounts = new AtomicCounts {
			VertexCount   = 0,
			InstanceCount = 1, //this is 1, as we only draw 1 instance
			FirstIndex    = 0,
			BaseVertex    = 0,
			FirstInstance = 0,
			IndexCount    = 0
		};

		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, CountsBuffer, 0, &atomicCounts, (nuint)sizeof(AtomicCounts));
	}

	private static void CreateBlocksBuffer() {
		BlocksBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Usage            = BufferUsage.Storage | BufferUsage.CopyDst,
			Size             = BlocksBufferSize = sizeof(BlockId) * Chunk.CHUNK_SIZE_CU, //size of a single block * blocks in chunk
			MappedAtCreation = false
		});

		Console.WriteLine($"Created mesh blocks buffer 0x{(nint)BlocksBuffer:x}");
	}

	private static void CreateOutputBuffers() {
		VertexOutputBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Label            = null,
			Usage            = BufferUsage.Storage | BufferUsage.Vertex,
			Size             = VertexOutputBufferSize = (ulong)(sizeof(WorldVertex) * Chunk.CHUNK_SIZE_CU * 6 * 4), //size of one vertex * blocks in chunk * 6 faces * 4 vertices per face
			MappedAtCreation = false
		});
		IndexOutputBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Label            = null,
			Usage            = BufferUsage.Storage | BufferUsage.Index,
			Size             = IndexOutputBufferSize = sizeof(uint) * Chunk.CHUNK_SIZE_CU * 6 * 6, //size of one index * blocks in chunk * 6 faces * 6 indices per face
			MappedAtCreation = false
		});

		Console.WriteLine($"Created mesh ouptut buffers 0x{(nint)VertexOutputBuffer:x} and 0x{(nint)IndexOutputBuffer:x}");
	}

	private static void CreateShader() {
		byte[] shader = ResourceHelpers.ReadResource("Shaders/Mesh.wgsl");

		fixed (byte* ptr = shader) {
			ShaderModuleWGSLDescriptor wgslDescriptor = new ShaderModuleWGSLDescriptor {
				Code  = ptr,
				Chain = new ChainedStruct(sType: SType.ShaderModuleWgsldescriptor)
			};

			ShaderModuleDescriptor descriptor = new ShaderModuleDescriptor {
				NextInChain = (ChainedStruct*)(&wgslDescriptor)
			};

			MeshShader = Graphics.WebGPU.DeviceCreateShaderModule(Graphics.Device, descriptor);
			Console.WriteLine($"Mesh shader 0x{(nint)MeshShader:x8} created!");
		}
	}

	private static void CreatePipeline() {
		BindGroupLayout* layout = MeshBindGroupLayout;
		PipelineLayoutDescriptor pipelineLayoutDescriptor = new PipelineLayoutDescriptor {
			Label                = null,
			BindGroupLayoutCount = 1,
			BindGroupLayouts     = &layout
		};
		PipelineLayout* pipelineLayout = Graphics.WebGPU.DeviceCreatePipelineLayout(Graphics.Device, pipelineLayoutDescriptor);

		ComputePipelineDescriptor computePipelineDescriptor = new ComputePipelineDescriptor {
			Label  = null,
			Layout = pipelineLayout,
			Compute = new ProgrammableStageDescriptor {
				Module        = MeshShader,
				EntryPoint    = (byte*)SilkMarshal.StringToPtr("main"),
				ConstantCount = 0,
				Constants     = null
			}
		};
		MeshPipeline = Graphics.WebGPU.DeviceCreateComputePipeline(Graphics.Device, computePipelineDescriptor);

		Graphics.Disposal.Dispose(pipelineLayout);

		Console.WriteLine($"Created mesh pipeline 0x{(nuint)MeshPipeline:x}");

		SilkMarshal.Free((nint)computePipelineDescriptor.Compute.EntryPoint);
	}

	public static void Mesh(ComputePassEncoder* computePass) {
		Graphics.WebGPU.ComputePassEncoderSetPipeline(computePass, MeshPipeline);
		Graphics.WebGPU.ComputePassEncoderSetBindGroup(computePass, 0, MeshBindGroup, 0, null);
		Graphics.WebGPU.ComputePassEncoderDispatchWorkgroups(computePass, Chunk.CHUNK_SIZE / 4, Chunk.CHUNK_SIZE / 4, Chunk.CHUNK_SIZE / 4);
	}
}
