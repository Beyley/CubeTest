using System.Numerics;

namespace CubeTest.World; 

public struct LightInfo {
	public Vector3 Position;
	private float padding1;
	public Vector3 Color;
	public float SpecularStrength;
	public Vector3 Ambient;
	private float padding3;
	public Vector3 Diffuse;
	private float padding4;
}
