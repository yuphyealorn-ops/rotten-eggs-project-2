#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RottenEggs
{
    /// <summary>One controller snapshot per game tick with edge-triggered UI navigation.</summary>
    public sealed class GamepadControls
    {
        public struct Frame
        {
            public int Move;
            public int Navigate;
            public int Volume;
            public bool Confirm;
            public bool Back;
            public bool Pause;
            public bool Restart;
            public bool Mute;
            public bool Fire;
        }

        public bool Used { get; private set; }

#if ENABLE_INPUT_SYSTEM
        private const float DeadZone = 0.4f;
        private int deviceId = -1;
        private int heldDirection;
        private bool confirmHeld;
        private bool backHeld;
        private bool pauseHeld;
        private bool restartHeld;
        private bool muteHeld;
        private bool leftShoulderHeld;
        private bool rightShoulderHeld;
#endif

        public Frame Read(bool navigating)
        {
            Frame frame = new Frame();
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Gamepad.current;
            if (pad == null || !pad.added || !pad.enabled)
            {
                deviceId = -1;
                heldDirection = 0;
                ResetButtonStates();
                return frame;
            }
            if (deviceId != pad.deviceId)
            {
                deviceId = pad.deviceId;
                heldDirection = 0;
                ResetButtonStates();
            }

            UnityEngine.Vector2 stick = pad.leftStick.ReadValue();
            UnityEngine.Vector2 dpad = pad.dpad.ReadValue();
            frame.Move = Direction(dpad.x != 0 ? dpad.x : stick.x);
            frame.Confirm = Pressed(pad.buttonSouth.isPressed, ref confirmHeld);
            frame.Back = Pressed(pad.buttonEast.isPressed, ref backHeld);
            frame.Fire = frame.Back; // The existing throw button remains unchanged.
            frame.Pause = Pressed(pad.startButton.isPressed, ref pauseHeld);
            frame.Restart = Pressed(pad.buttonWest.isPressed, ref restartHeld);
            frame.Mute = Pressed(pad.selectButton.isPressed, ref muteHeld);
            frame.Volume = (Pressed(pad.rightShoulder.isPressed, ref rightShoulderHeld) ? 1 : 0)
                           - (Pressed(pad.leftShoulder.isPressed, ref leftShoulderHeld) ? 1 : 0);

            // Plugging in a pad or a little stick drift must not change the UI hints.
            if (stick.sqrMagnitude > DeadZone * DeadZone || dpad.sqrMagnitude > 0
                || pad.rightStick.ReadValue().sqrMagnitude > DeadZone * DeadZone
                || frame.Confirm || frame.Back || frame.Pause || frame.Restart || frame.Mute
                || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
                || pad.buttonNorth.isPressed || pad.leftTrigger.isPressed
                || pad.rightTrigger.isPressed || pad.leftStickButton.isPressed
                || pad.rightStickButton.isPressed)
                Used = true;

            int direction = navigating ? -Direction(dpad.y != 0 ? dpad.y : stick.y) : 0;
            if (direction != heldDirection)
            {
                heldDirection = direction;
                if (direction != 0)
                    frame.Navigate = direction;
            }
#endif
            return frame;
        }

#if ENABLE_INPUT_SYSTEM
        private static int Direction(float value)
        {
            return value > DeadZone ? 1 : value < -DeadZone ? -1 : 0;
        }

        private static bool Pressed(bool isPressed, ref bool wasHeld)
        {
            bool wasPressed = isPressed && !wasHeld;
            wasHeld = isPressed;
            return wasPressed;
        }

        private void ResetButtonStates()
        {
            confirmHeld = false;
            backHeld = false;
            pauseHeld = false;
            restartHeld = false;
            muteHeld = false;
            leftShoulderHeld = false;
            rightShoulderHeld = false;
        }
#endif
    }
}
