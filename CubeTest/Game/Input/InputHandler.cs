using System.Numerics;
using CubeTest.World;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Input.Sdl;

namespace CubeTest.Game.Input;

public static class InputHandler
{
    private static IInputContext _Input = null!;
    private static Vector2 _LastMousePosition = Vector2.Zero;

    public static void Initialize()
    {
        //Register GLFW and SDL input, for AOT scenarios (like WASM or NativeAOT)
        GlfwInput.RegisterPlatform();
        SdlInput.RegisterPlatform();
    }

    public static void Load(IInputContext input)
    {
        _Input = input;
    }

    public static void ProcessInputs(float d)
    {
        // IMPORTANT! All updates to properties in this struct should be differential.
        // So, instead of setting directly with `inputs.Move.X = 1.0f`, use `inputs.Move.X += 1.0f`.
        // This is to support using multiple input methods at once. It all gets clamped down in the end.
        Inputs inputs = new();
        
        HandleAllInputs(ref inputs);

        if (inputs.Move.X != 0 || inputs.Move.Y != 0)
            inputs.Move = Vector2.Normalize(inputs.Move);

        WorldGraphics.Camera.Yaw += inputs.Turn.X;
        WorldGraphics.Camera.Pitch += inputs.Turn.Y;
        WorldGraphics.Camera.Pitch = Math.Clamp(WorldGraphics.Camera.Pitch, -89.99f, 89.99f);
        
        WorldGraphics.Camera.Position += WorldGraphics.Camera.Up * d * inputs.UpDown;
        WorldGraphics.Camera.Position += WorldGraphics.Camera.Front * d * inputs.Move.Y;
        
        WorldGraphics.Camera.Position += Vector3.Normalize(Vector3.Cross(WorldGraphics.Camera.Front, WorldGraphics.Camera.Up)) * d * inputs.Move.X;
    }

    private static void HandleAllInputs(ref Inputs inputs)
    {
        foreach (IMouse mouse in _Input.Mice) HandleMouseInputs(mouse, ref inputs);
        foreach (IKeyboard kb in _Input.Keyboards) HandleKeyboardInputs(kb, ref inputs);
        foreach (IGamepad gamepad in _Input.Gamepads) HandleGamepadInputs(gamepad, ref inputs);
    }

    private static void HandleMouseInputs(IMouse mouse, ref Inputs inputs)
    {
        if (mouse.IsButtonPressed(MouseButton.Left) || mouse.IsButtonPressed(MouseButton.Right))
        {
            mouse.Cursor.CursorMode = CursorMode.Raw;

            bool isTurn = mouse.IsButtonPressed(MouseButton.Left);
            Vector2 axis = isTurn ? inputs.Turn : inputs.Move;
            float x = (mouse.Position.X - _LastMousePosition.X) * 0.1f;
            float y = (mouse.Position.Y - _LastMousePosition.Y) * 0.1f;

            axis.X += x;
            axis.Y -= y;

            if (isTurn) inputs.Turn = axis;
            else // if panning
            {
                inputs.Move.X = axis.X;
                inputs.UpDown = axis.Y / 6;
            }
        }
        else
        {
            mouse.Cursor.CursorMode = CursorMode.Normal;
        }

        // Technically breaks when there are multiple mice
        _LastMousePosition = mouse.Position;
    }

    private static void HandleKeyboardInputs(IKeyboard kb, ref Inputs inputs)
    {
        if (kb.IsKeyPressed(Key.A))
            inputs.Move.X -= 1.0f;
        if (kb.IsKeyPressed(Key.D))
            inputs.Move.X += 1.0f;

        if (kb.IsKeyPressed(Key.ShiftLeft))
            inputs.UpDown -= 1.0f;
        if (kb.IsKeyPressed(Key.Space))
            inputs.UpDown += 1.0f;

        if (kb.IsKeyPressed(Key.W))
            inputs.Move.Y += 1.0f;
        if (kb.IsKeyPressed(Key.S))
            inputs.Move.Y -= 1.0f;

        float speed = 2.0f;
        if (kb.IsKeyPressed(Key.ControlLeft))
            speed /= 2;

        if (kb.IsKeyPressed(Key.Up))
            inputs.Turn.Y += speed;
        if (kb.IsKeyPressed(Key.Down))
            inputs.Turn.Y -= speed;
        if (kb.IsKeyPressed(Key.Left))
            inputs.Turn.X -= speed;
        if (kb.IsKeyPressed(Key.Right))
            inputs.Turn.X += speed;
    }

    private static void HandleGamepadInputs(IGamepad gamepad, ref Inputs inputs)
    {
        gamepad.Deadzone = new Deadzone(0.20f, DeadzoneMethod.Traditional);
        
        Thumbstick leftStick = gamepad.Thumbsticks[0];
        inputs.Move += new Vector2(leftStick.X, -leftStick.Y);

        Thumbstick rightStick = gamepad.Thumbsticks[1];
        inputs.Turn += new Vector2(rightStick.X, -rightStick.Y) * 2;

        // Up/down
        if (gamepad.A().Pressed)
            inputs.UpDown += 1.0f;
        if (gamepad.B().Pressed)
            inputs.UpDown -= 1.0f;
    }
}