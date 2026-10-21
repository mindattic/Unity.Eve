using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Eve.Input
{
    /// <summary>
    /// Gamepad-first input with keyboard fallback, built on the Input System. Bindings are declared
    /// here in one table so they are reviewable in code. All reads are per rendered frame and ignore
    /// <see cref="Time.timeScale"/>, so menus work while the world is paused.
    /// </summary>
    /// <remarks>
    /// | Action      | Gamepad          | Keyboard            |
    /// |-------------|------------------|---------------------|
    /// | Move        | Left stick       | WASD / arrows       |
    /// | Confirm     | A (South)        | Enter / Z           |
    /// | Cancel/Dodge| B (East)         | Space / X           |
    /// | Interact    | X (West)         | E                   |
    /// | Focus moves | X / Y / A        | 1 / 2 / 3           |
    /// | Shootdodge  | Right shoulder   | Left Ctrl           |
    /// | Menu        | Start            | Esc / Tab           |
    /// | Navigate    | D-pad / stick    | Arrows / WASD       |
    /// </remarks>
    public sealed class GameInput : MonoBehaviour
    {
        public const float StickDeadzone = 0.25f;

        private InputActionMap _map;
        private InputAction _move, _navigate, _confirm, _cancel, _interact, _menu, _walk, _shootdodge;
        private InputAction _focusHeal, _focusVitals, _focusRiposte;
        private Vector2 _lastNavigate;
        private Coroutine _rumble;

        public static GameInput Instance { get; private set; }

        /// <summary>Movement stick with a radial deadzone rescaled to 0..1.</summary>
        public Vector2 Move { get; private set; }
        public bool ConfirmPressed => _confirm.WasPressedThisFrame() || Consume(ref _confirmOverride);
        public bool ConfirmHeld => _confirm.IsPressed();
        public bool CancelPressed => _cancel.WasPressedThisFrame() || Consume(ref _dodgeOverride);
        public bool InteractPressed => _interact.WasPressedThisFrame();
        public bool InteractHeld => _interact.IsPressed();
        public bool MenuPressed => _menu.WasPressedThisFrame();
        /// <summary>Max Payne shootdodge: leap in the chosen direction, firing in bullet time. Unused
        /// shoulder button, distinct from the everyday Cancel/Dodge roll.</summary>
        public bool ShootdodgePressed => _shootdodge.WasPressedThisFrame() || Consume(ref _shootdodgeOverride);
        public bool FocusHealPressed => _focusHeal.WasPressedThisFrame();
        public bool FocusVitalsPressed => _focusVitals.WasPressedThisFrame();
        public bool FocusRipostePressed => _focusRiposte.WasPressedThisFrame();

        /// <summary>Menu navigation as discrete steps (-1, 0, +1) on each axis, edge-triggered.</summary>
        public Vector2Int NavigateStep { get; private set; }

        /// <summary>Scripted input for the autopilot / tests. When set, overrides the stick.</summary>
        public Vector2? MoveOverride { get; set; }

        private bool _dodgeOverride;
        private bool _confirmOverride;
        private bool _shootdodgeOverride;

        /// <summary>Scripted input for the autopilot: presses Cancel/Dodge for exactly one read, through
        /// the real input path so timing against an enemy strike can land a genuine perfect dodge.</summary>
        public void TriggerDodge() => _dodgeOverride = true;

        /// <summary>Scripted input for the autopilot: presses Confirm for exactly one read, through the
        /// real input path so timing an active reload's sweet spot works the same way a human's does.</summary>
        public void TriggerConfirm() => _confirmOverride = true;

        /// <summary>Scripted input for the autopilot: presses Shootdodge for exactly one read.</summary>
        public void TriggerShootdodge() => _shootdodgeOverride = true;

        private static bool Consume(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }

        private void Awake()
        {
            Instance = this;
            _map = new InputActionMap("Gameplay");

            _move = _map.AddAction("Move", InputActionType.Value);
            _move.AddBinding("<Gamepad>/leftStick");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");

            _navigate = _map.AddAction("Navigate", InputActionType.Value);
            _navigate.AddBinding("<Gamepad>/dpad");
            _navigate.AddBinding("<Gamepad>/leftStick");
            _navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _navigate.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");

            _confirm = Button("Confirm", "<Gamepad>/buttonSouth", "<Keyboard>/enter", "<Keyboard>/z");
            _cancel = Button("Cancel", "<Gamepad>/buttonEast", "<Keyboard>/space", "<Keyboard>/x", "<Keyboard>/backspace");
            _interact = Button("Interact", "<Gamepad>/buttonWest", "<Keyboard>/e");
            _menu = Button("Menu", "<Gamepad>/start", "<Keyboard>/escape", "<Keyboard>/tab");
            _walk = Button("Walk", "<Gamepad>/leftStickPress", "<Keyboard>/leftShift");
            _shootdodge = Button("Shootdodge", "<Gamepad>/rightShoulder", "<Keyboard>/leftCtrl");
            _focusHeal = Button("FocusHeal", "<Gamepad>/buttonWest", "<Keyboard>/1");
            _focusVitals = Button("FocusVitals", "<Gamepad>/buttonNorth", "<Keyboard>/2");
            _focusRiposte = Button("FocusRiposte", "<Gamepad>/buttonSouth", "<Keyboard>/3");

            _map.Enable();
        }

        private InputAction Button(string name, params string[] paths)
        {
            var action = _map.AddAction(name, InputActionType.Button);
            foreach (var p in paths) action.AddBinding(p);
            return action;
        }

        private void OnDestroy()
        {
            _map?.Disable();
            _map?.Dispose();
            StopRumble();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var raw = MoveOverride ?? _move.ReadValue<Vector2>();
            var mag = raw.magnitude;
            Move = mag < StickDeadzone ? Vector2.zero : raw / mag * Mathf.Min(1f, (mag - StickDeadzone) / (1f - StickDeadzone));
            if (_walk.IsPressed()) Move *= 0.45f;

            var nav = _navigate.ReadValue<Vector2>();
            var step = new Vector2Int(Axis(nav.x, _lastNavigate.x), Axis(nav.y, _lastNavigate.y));
            NavigateStep = step;
            _lastNavigate = nav;
        }

        private static int Axis(float now, float before)
        {
            const float on = 0.6f;
            if (now > on && before <= on) return 1;
            if (now < -on && before >= -on) return -1;
            return 0;
        }

        /// <summary>Dual-motor rumble on the current gamepad, timed in real seconds.</summary>
        public void Rumble(float low, float high, float seconds)
        {
            var pad = Gamepad.current;
            if (pad == null) return;
            if (_rumble != null) StopCoroutine(_rumble);
            _rumble = StartCoroutine(RumbleRoutine(pad, low, high, seconds));
        }

        private IEnumerator RumbleRoutine(Gamepad pad, float low, float high, float seconds)
        {
            pad.SetMotorSpeeds(low, high);
            yield return new WaitForSecondsRealtime(seconds);
            pad.SetMotorSpeeds(0f, 0f);
            _rumble = null;
        }

        private void StopRumble() => Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }
}
