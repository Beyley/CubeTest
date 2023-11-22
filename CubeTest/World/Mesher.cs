using CubeTest.Game;
using CubeTest.Helpers;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace CubeTest.World;

public static unsafe class Mesher {
	public static Buffer* BlocksBuffer;

	public static ulong VertexOutputBufferSize;
	public static ulong IndexOutputBufferSize;
	public static ulong BlocksBufferSize;
	public static ulong CountsBufferSize;

	public static ComputePipeline* MeshPipeline;
	public static ShaderModule*    MeshShader;
	public static BindGroupLayout* MeshBindGroupLayout;
	public static BindGroup*       MeshBindGroup;
	
	public static Buffer* VertexOutputBuffers;
	public static Buffer* IndexOutputBuffers;
	public static Buffer* CountsBuffers;

	public static void Initialize(ulong chunkCount) {
		CreateBlocksBuffer();
		
		CreateOutputBuffers(chunkCount);

		CreateBindGroupLayout();
		CreateBindGroup();

		CreateShader();
		CreatePipeline();
	}

	public static void Dispose() {
		Graphics.Disposal.Dispose(VertexOutputBuffers);
		Graphics.Disposal.Dispose(IndexOutputBuffers);
		Graphics.Disposal.Dispose(BlocksBuffer);
		Graphics.Disposal.Dispose(CountsBuffers);

		Graphics.Disposal.Dispose(MeshBindGroupLayout);
		Graphics.Disposal.Dispose(MeshBindGroup);
		Graphics.Disposal.Dispose(MeshShader);
		Graphics.Disposal.Dispose(MeshPipeline);
	}

	private static void CreateBindGroupLayout() {
		const int entryCount = 4;

		BindGroupLayoutEntry* entries = stackalloc BindGroupLayoutEntry[entryCount];
		entries[0] = new BindGroupLayoutEntry {
			Binding    = 0,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				NextInChain      = null,
				Type             = BufferBindingType.Storage,
				HasDynamicOffset = true,
				MinBindingSize   = VertexOutputBufferSize
			}
		};
		entries[1] = new BindGroupLayoutEntry {
			Binding    = 1,
			Visibility = ShaderStage.Compute,
			Buffer = new BufferBindingLayout {
				Type             = BufferBindingType.Storage,
				HasDynamicOffset = true,
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
				HasDynamicOffset = true,
				MinBindingSize   = CountsBufferSize
			}
		};

		MeshBindGroupLayout = Graphics.WebGPU.DeviceCreateBindGroupLayout(Graphics.Device, new BindGroupLayoutDescriptor {
			EntryCount = entryCount,
			Entries    = entries
		});
	}

	private static void CreateBindGroup() {
		const int entryCount = 4;

		BindGroupEntry* entries = stackalloc BindGroupEntry[entryCount];

		entries[0] = new BindGroupEntry {
			Binding = 0,
			Buffer  = VertexOutputBuffers,
			Offset  = 0,
			Size    = VertexOutputBufferSize
		};
		entries[1] = new BindGroupEntry {
			Binding = 1,
			Buffer  = IndexOutputBuffers,
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
			Buffer  = CountsBuffers,
			Offset  = 0,
			Size    = CountsBufferSize
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

	public static void ResetCounts(ulong chunkIndex) {
		AtomicCounts atomicCounts = new AtomicCounts {
			VertexCount   = 0,
			InstanceCount = 1, //this is 1, as we only draw 1 instance
			FirstIndex    = 0,
			BaseVertex    = 0,
			FirstInstance = 0,
			IndexCount    = 0
		};

		Graphics.WebGPU.QueueWriteBuffer(Graphics.Queue, CountsBuffers, CountsBufferSize * chunkIndex, &atomicCounts, (nuint)sizeof(AtomicCounts));
	}

	private static void CreateBlocksBuffer() {
		BlocksBuffer = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Usage            = BufferUsage.Storage | BufferUsage.CopyDst,
			Size             = BlocksBufferSize = sizeof(BlockId) * Chunk.CHUNK_SIZE_CU + sizeof(int) * Chunk.CHUNK_POS_SIZE, //size of a single block * blocks in chunk
			MappedAtCreation = false
		});

		Console.WriteLine($"Created mesh blocks buffer 0x{(nint)BlocksBuffer:x}");
	}

	private static void CreateOutputBuffers(ulong chunkCount)
	{
		VertexOutputBufferSize = (ulong)(sizeof(WorldVertex) * Chunk.CHUNK_SIZE_CU * 6 * 4); //size of one vertex * blocks in chunk * 6 faces * 4 vertices per face
		IndexOutputBufferSize = sizeof(uint) * Chunk.CHUNK_SIZE_CU * 6 * 6; //size of one index * blocks in chunk * 6 faces * 6 indices per face
		CountsBufferSize = Math.Max((ulong)sizeof(AtomicCounts), 256);
		
		VertexOutputBuffers = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Label            = null,
			Usage            = BufferUsage.Storage | BufferUsage.Vertex | BufferUsage.CopySrc,
			Size             = VertexOutputBufferSize * chunkCount,
			MappedAtCreation = false
		});
		Console.WriteLine($"Created mesh vertex output buffers 0x{(nint)VertexOutputBuffers:x}");
		
		IndexOutputBuffers = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Label            = null,
			Usage            = BufferUsage.Storage | BufferUsage.Index | BufferUsage.CopySrc,
			Size             = IndexOutputBufferSize * chunkCount,
			MappedAtCreation = false
		});
		Console.WriteLine($"Created mesh index output buffers 0x{(nint)IndexOutputBuffers:x}");
		
		CountsBuffers = Graphics.WebGPU.DeviceCreateBuffer(Graphics.Device, new BufferDescriptor {
			Usage            = BufferUsage.Storage | BufferUsage.CopyDst | BufferUsage.Indirect | BufferUsage.CopySrc,
			Size             = CountsBufferSize * chunkCount,
			MappedAtCreation = false
		});
		Console.WriteLine($"Created mesh counts buffer 0x{(nint)CountsBuffers:x}");

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

	public static void Mesh(ComputePassEncoder* computePass, uint offsetIndex)
	{
		const int offsetCount = 3;
		uint* offsets = stackalloc uint[offsetCount];
		offsets[0] = (uint)VertexOutputBufferSize * offsetIndex;
		offsets[1] = (uint)IndexOutputBufferSize * offsetIndex;
		offsets[2] = (uint)CountsBufferSize * offsetIndex;

		Graphics.WebGPU.ComputePassEncoderSetPipeline(computePass, MeshPipeline);
		Graphics.WebGPU.ComputePassEncoderSetBindGroup(computePass, 0, MeshBindGroup, offsetCount, offsets);
		Graphics.WebGPU.ComputePassEncoderDispatchWorkgroups(computePass, Chunk.CHUNK_SIZE / 4, Chunk.CHUNK_SIZE / 4, Chunk.CHUNK_SIZE / 4);
	}
}
