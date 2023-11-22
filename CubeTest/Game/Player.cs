using System.Numerics;
using CubeTest.Game.Input.Fly;
using CubeTest.Game.Input.Player;
using CubeTest.World;

namespace CubeTest.Game;

public class Player
{
    public Player(PlayerInputHandler inputHandler)
    {
        _inputHandler = inputHandler;
        inputHandler.Player = this;
    }

    public readonly Vector3 Size = new(0.5f, 2f, 0.5f);

    public Vector3 Position;
    public bool OnGround;

    public float Pitch;
    public float Yaw;
    
    private readonly PlayerInputHandler _inputHandler;

    public void Update(float d)
    {
        _inputHandler.Update(d);
        if (!OnGround) this.Position.Y -= 2.5f * d;

        OnGround = IsOnGround();
    }

    private bool IsOnGround() => this.Position.Y <= 3.3;
    
    public void HandleInputs(float d, FlyInputs inputs)
    {
        Yaw += inputs.Turn.X;
        Pitch += inputs.Turn.Y;
        Pitch = Math.Clamp(Pitch, -89.99f, 89.99f);
        
        this.Position += Vector3.Normalize(Vector3.Cross(WorldGraphics.Camera.Front, WorldGraphics.Camera.Up)) * d * inputs.Move.X;
        this.Position += Vector3.Normalize(WorldGraphics.Camera.Front with { Y = 0 }) * d * inputs.Move.Y;
    }
}