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
    private Vector3 _velocity;
    private Vector3 _targetVelocity;

    private bool _onGround;

    public float Pitch;
    public float Yaw;
    
    private readonly PlayerInputHandler _inputHandler;

    public void Update(float d)
    {
        this._inputHandler.Update(d);

        this._onGround = this.IsOnGround();

        const float acceleration = 1.0f;
        const float deceleration = 0.25f;
        const float maxSpeed = 0.01f;

        if (this._targetVelocity.X != 0) this._velocity.X += this._targetVelocity.X * acceleration * d;
        else this._velocity.X -= this._velocity.X * deceleration;
        
        if (this._targetVelocity.Z != 0) this._velocity.Z += this._targetVelocity.Z * acceleration * d;
        else this._velocity.Z -= this._velocity.Z * deceleration;

        if (this._onGround) this._velocity.Y -= 1 * d;
        else this._velocity.Y = 0;

        this._velocity = Vector3.Clamp(this._velocity, -Vector3.One * maxSpeed, Vector3.One * maxSpeed);
        
        Console.WriteLine(this._velocity);
            
        this.Position += _velocity;
    }

    private bool IsOnGround() => this.Position.Y > 0;
    
    public void HandleInputs(float d, FlyInputs inputs)
    {
        Yaw += inputs.Turn.X;
        Pitch += inputs.Turn.Y;
        Pitch = Math.Clamp(Pitch, -89.99f, 89.99f);
        
        this._targetVelocity = Vector3.Normalize(Vector3.Cross(WorldGraphics.Camera.Front, WorldGraphics.Camera.Up)) * d * inputs.Move.X;
        this._targetVelocity += Vector3.Normalize(WorldGraphics.Camera.Front with { Y = 0 }) * d * inputs.Move.Y;
    }
}