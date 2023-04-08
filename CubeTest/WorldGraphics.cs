using Silk.NET.WebGPU;

namespace CubeTest; 

public static unsafe class WorldGraphics {
	private static ShaderModule* _Shader = null!;
	
	public static void Dispose() {
		Graphics.Disposal.Dispose(_Shader);
	}

	public static void Initialize() {
		CreateShader();
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
