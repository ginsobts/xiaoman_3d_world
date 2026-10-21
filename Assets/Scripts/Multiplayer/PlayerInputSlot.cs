using UnityEngine;

namespace VN.Multiplayer
{
    public enum LocalInputScheme
    {
        KeyboardWASD,
        KeyboardArrows,
        Gamepad1,
        Gamepad2,
        Gamepad3,
        Gamepad4,
        Remote
    }

    /// <summary>每个本地角色独立读取自己的键盘区或手柄，不依赖新 Input System 包。</summary>
    public class PlayerInputSlot : MonoBehaviour
    {
        public int localSlot;
        public LocalInputScheme scheme;
        public bool enabledForGameplay = true;

        public Vector2 ReadMove()
        {
            if (!enabledForGameplay || scheme == LocalInputScheme.Remote) return Vector2.zero;
            switch (scheme)
            {
                case LocalInputScheme.KeyboardWASD:
                    return Digital(KeyCode.A, KeyCode.D, KeyCode.S, KeyCode.W);
                case LocalInputScheme.KeyboardArrows:
                    return Digital(KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.UpArrow);
                default:
                    int joy = GamepadNumber;
                    return new Vector2(
                        Input.GetAxisRaw("Joy" + joy + "Horizontal"),
                        Input.GetAxisRaw("Joy" + joy + "Vertical"));
            }
        }

        public bool RunHeld
        {
            get
            {
                if (!enabledForGameplay) return false;
                if (scheme == LocalInputScheme.KeyboardWASD) return Input.GetKey(KeyCode.LeftShift);
                if (scheme == LocalInputScheme.KeyboardArrows) return Input.GetKey(KeyCode.RightShift);
                return IsGamepad && GetGamepadButton(GamepadNumber, 2);
            }
        }

        public bool JumpDown => enabledForGameplay && (
            scheme == LocalInputScheme.KeyboardWASD ? Input.GetKeyDown(KeyCode.Space) :
            scheme == LocalInputScheme.KeyboardArrows ? Input.GetKeyDown(KeyCode.RightControl) :
            IsGamepad && GetGamepadButtonDown(GamepadNumber, 1));

        public bool InteractDown => enabledForGameplay && (
            scheme == LocalInputScheme.KeyboardWASD ? Input.GetKeyDown(KeyCode.E) :
            scheme == LocalInputScheme.KeyboardArrows ? Input.GetKeyDown(KeyCode.Keypad0) :
            IsGamepad && GetGamepadButtonDown(GamepadNumber, 0));

        public bool InstrumentDown => enabledForGameplay && (
            scheme == LocalInputScheme.KeyboardWASD ? Input.GetKeyDown(KeyCode.Q) :
            scheme == LocalInputScheme.KeyboardArrows ? Input.GetKeyDown(KeyCode.Keypad1) :
            IsGamepad && GetGamepadButtonDown(GamepadNumber, 3));

        public bool BuildToggleDown => enabledForGameplay && (
            scheme == LocalInputScheme.KeyboardWASD ? Input.GetKeyDown(KeyCode.B) :
            scheme == LocalInputScheme.KeyboardArrows ? Input.GetKeyDown(KeyCode.Keypad2) :
            IsGamepad && GetGamepadButtonDown(GamepadNumber, 4));

        public string DisplayName
        {
            get
            {
                switch (scheme)
                {
                    case LocalInputScheme.KeyboardWASD: return "Keyboard WASD";
                    case LocalInputScheme.KeyboardArrows: return "Keyboard Arrows";
                    case LocalInputScheme.Remote: return "Remote";
                    default: return "Gamepad " + GamepadNumber;
                }
            }
        }

        public bool IsGamepad => scheme >= LocalInputScheme.Gamepad1 && scheme <= LocalInputScheme.Gamepad4;
        public int GamepadNumber => IsGamepad ? (int)scheme - (int)LocalInputScheme.Gamepad1 + 1 : 0;

        public static bool GetGamepadButtonDown(int gamepad, int button)
            => Input.GetKeyDown(GamepadKey(gamepad, button));

        public static bool GetGamepadButton(int gamepad, int button)
            => Input.GetKey(GamepadKey(gamepad, button));

        private static KeyCode GamepadKey(int gamepad, int button)
        {
            gamepad = Mathf.Clamp(gamepad, 1, 8);
            button = Mathf.Clamp(button, 0, 19);
            int baseValue = (int)KeyCode.Joystick1Button0 + (gamepad - 1) * 20;
            return (KeyCode)(baseValue + button);
        }

        private static Vector2 Digital(KeyCode left, KeyCode right, KeyCode down, KeyCode up)
        {
            float x = (Input.GetKey(right) ? 1f : 0f) - (Input.GetKey(left) ? 1f : 0f);
            float y = (Input.GetKey(up) ? 1f : 0f) - (Input.GetKey(down) ? 1f : 0f);
            Vector2 value = new Vector2(x, y);
            return value.sqrMagnitude > 1f ? value.normalized : value;
        }
    }
}
