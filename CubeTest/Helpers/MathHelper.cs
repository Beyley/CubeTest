using System.Runtime.CompilerServices;

namespace CubeTest.Helpers; 

public class MathHelper {
	[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
	public static float DegToRad(float degrees) {
		return degrees * (float)Math.PI / 180;
	}
}
