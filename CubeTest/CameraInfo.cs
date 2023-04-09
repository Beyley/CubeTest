using System.Numerics;

namespace CubeTest; 

public struct CameraInfo {
	public  Vector3   Position;
	private float     padding;
	public  Matrix4x4 View;
}
