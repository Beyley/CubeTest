using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using CubeTest.Helpers;
using CubeTest.ModelLoader;
using CubeTest.ModelLoader.WavefrontObj;

namespace CubeTest;

[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[MemoryDiagnoser]
public class Benchmarks {
	private byte[]         _data;
	private ObjModelLoader _loader;

	[GlobalSetup]
	public void Setup() {
		this._data   = ResourceHelpers.ReadResource("Models/windows.obj");
		this._loader = new ObjModelLoader();
	}

	[Benchmark]
	public void ParseCube() {
		Model cube = this._loader.LoadModel(this._data);
	}
}
