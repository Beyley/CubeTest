using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CubeTest.ModelLoader;
using CubeTest.ModelLoader.WavefrontObj;

namespace CubeTest; 

[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[MemoryDiagnoser]
public class Benchmarks {
	private byte[] _data;
	[GlobalSetup]
	public void Setup() {
		this._data = ResourceHelpers.ReadResource("Models/cube.obj");
	}
	
	[Benchmark]
	public void ParseCube() {
		Model cube = new ObjModelLoader().LoadModel(this._data);
	}
}
