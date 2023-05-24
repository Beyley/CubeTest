using System.Numerics;
using CubeTest.World;
using Silk.NET.Input;
using Silk.NET.Input.Glfw;
using Silk.NET.Input.Sdl;

namespace CubeTest.Game.Input;

public abstract class InputHandler<TInputs> where TInputs : struct
{
    private IInputContext _Input = null!;
    protected Vector2 _LastMousePosition { get; private set; } = Vector2.Zero;

    public void Initialize()
    {
        //Register GLFW and SDL input, for AOT scenarios (like WASM or NativeAOT)
        GlfwInput.RegisterPlatform();
        SdlInput.RegisterPlatform();
    }

    public void Load(IInputContext input)
    {
        _Input = input;
    }

    public void Update(float d)
    {
        // IMPORTANT! All updates to properties in this struct should be differential.
        // So, instead of setting directly with `inputs.Move.X = 1.0f`, use `inputs.Move.X += 1.0f`.
        // This is to support using multiple input methods at once. It all gets clamped down in the end.
        TInputs inputs = new();
        
        HandleAllInputs(ref inputs);
        
        this.ProcessInputs(d, inputs);
    }

    protected abstract void ProcessInputs(float d, TInputs inputs);

    private void HandleAllInputs(ref TInputs inputs)
    {
        foreach (IMouse mouse in _Input.Mice)
        {
            HandleMouseInputs(mouse, ref inputs);
            
            // Technically breaks when there are multiple mice
            _LastMousePosition = mouse.Position;
        }
        
        foreach (IKeyboard kb in _Input.Keyboards) HandleKeyboardInputs(kb, ref inputs);
        foreach (IGamepad gamepad in _Input.Gamepads) HandleGamepadInputs(gamepad, ref inputs);
    }

    protected Vector2 HandleMouseLookInputs(IMouse mouse)
    {
        mouse.Cursor.CursorMode = CursorMode.Raw;
        
        float x = (mouse.Position.X - _LastMousePosition.X) * 0.1f;
        float y = (mouse.Position.Y - _LastMousePosition.Y) * 0.1f;
        return new Vector2(x, -y);
    }
    
    protected Vector2 HandleGamepadLookInputs(IGamepad gamepad)
    {
        Thumbstick rightStick = gamepad.Thumbsticks[1];
        return new Vector2(rightStick.X, -rightStick.Y) * 2;
    }

    protected Vector2 HandleGamepadMoveInputs(IGamepad gamepad)
    {
        Thumbstick leftStick = gamepad.Thumbsticks[0];
        return new Vector2(leftStick.X, -leftStick.Y);
    }

    protected abstract void HandleMouseInputs(IMouse mouse, ref TInputs inputs);

    protected abstract void HandleKeyboardInputs(IKeyboard kb, ref TInputs inputs);

    protected abstract void HandleGamepadInputs(IGamepad gamepad, ref TInputs inputs);
}