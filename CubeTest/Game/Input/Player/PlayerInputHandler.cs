using CubeTest.Game.Input.Fly;
using Silk.NET.Input;

namespace CubeTest.Game.Input.Player;

public class PlayerInputHandler : InputHandler<FlyInputs>
{
    internal Game.Player Player;
    
    protected override void ProcessInputs(float d, FlyInputs inputs) => Player.HandleInputs(d, inputs);

    protected override void HandleMouseInputs(IMouse mouse, ref FlyInputs inputs)
    {
        inputs.Turn += HandleMouseLookInputs(mouse);
    }

    protected override void HandleKeyboardInputs(IKeyboard kb, ref FlyInputs inputs)
    {
        if (kb.IsKeyPressed(Key.W))
            inputs.Move.Y += 1.0f;
        if (kb.IsKeyPressed(Key.S))
            inputs.Move.Y -= 1.0f;
        
        if (kb.IsKeyPressed(Key.A))
            inputs.Move.X -= 1.0f;
        if (kb.IsKeyPressed(Key.D))
            inputs.Move.X += 1.0f;
    }

    protected override void HandleGamepadInputs(IGamepad gamepad, ref FlyInputs inputs)
    {
        gamepad.Deadzone = new Deadzone(0.20f, DeadzoneMethod.Traditional);
        inputs.Move += HandleGamepadMoveInputs(gamepad);
        inputs.Turn += HandleGamepadLookInputs(gamepad);
    }
}