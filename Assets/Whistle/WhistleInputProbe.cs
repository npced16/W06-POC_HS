#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WhistlePOC
{
    // Test-only input driver: inject inside the Player loop, immediately before gameplay Update.
    [DefaultExecutionOrder(-10000)]
    public sealed class WhistleInputProbe : MonoBehaviour
    {
        public Keyboard keyboard;
        public Mouse mouse;
        public bool holding, mouseHeld, toggleMode;
        public Vector2 position;
        void Update()
        {
            if (keyboard != null)
            {
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, toggleMode ? holding ? new KeyboardState(Key.Space, Key.Tab) : new KeyboardState(Key.Tab) : holding ? new KeyboardState(Key.Space) : new KeyboardState());
            }
            if (mouse != null)
            {
                mouse.MakeCurrent();
                var input = new MouseState { position = position };
                if (mouseHeld) input = input.WithButton(MouseButton.Left);
                InputSystem.QueueStateEvent(mouse, input);
            }
            InputSystem.Update();
        }
    }
}
#endif
