using System.Numerics;

namespace CubeTest.Game.Input;

public struct FlyInputs
{
    /// <summary>
    /// 0 to 1, controls where the camera should move to
    /// </summary>
    public Vector2 Move;
    /// <summary>
    /// 0 to inf, controls how far the camera should turn
    /// </summary>
    public Vector2 Turn;
    /// <summary>
    /// -1 to 1, controls whether to move the camera up or down
    /// </summary>
    public float UpDown;
}