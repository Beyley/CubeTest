using System.Numerics;

namespace CubeTest.World;

public struct Camera {
	public Vector3 Position = new Vector3(0, 0, 0);
	public Vector3 Front    = new Vector3(0, 0, -1);
	public Vector3 Up       = new Vector3(0, 1, 0);

	public float Yaw   = -90;
	public float Pitch = 0;

	public Camera() {}
}
