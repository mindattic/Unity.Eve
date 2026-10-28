using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Eve.Cameras;
using Unity.Eve.Core.Session;
using Unity.Eve.Game;

namespace Unity.Eve.Input
{
    /// <summary>
    /// Gamepad-first input with keyboard fallback, built on the Input System. Bindings are declared
    /// here in one table so they are reviewable in code. This is the local seat's input device only: each
    /// rendered frame it samples the controls into an <see cref="IntentCommand"/> and submits it to the session,
    /// and the game reads the seat's <see cref="PlayerControls"/> back (U.EVE-LAW-6). Sampling ignores
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
    [DefaultExecutionOrder(-500)] // the frame's intent is in the session before anything reads it
    public sealed class GameInput : MonoBehaviour
    {
        public const float StickDeadzone = 0.25f;

        private InputActionMap _map;
        private InputAction _move, _navigate, _confirm, _cancel, _interact, _menu, _walk, _shootdodge;
        private InputAction _focusHeal, _focusVitals, _focusRiposte;
        private Vector2 _lastNavigate;
        private Coroutine _rumble;

        public static GameInput Instance { get; private set; }

        /// <summary>Scripted input for the autopilot / tests. When set, overrides the stick.</summary>
        public Vector2? MoveOverride { get; set; }

        /// <summary>The seat this device speaks for: the session's local player.</summary>
        private static ISession Session => GameSession.Instance != null ? GameSession.Instance.Session : null;

        /// <summary>Scripted input for the autopilot: presses Cancel/Dodge for exactly one read, through
        /// the real command path so timing against an enemy strike can land a genuine perfect dodge.</summary>
        public void TriggerDodge() => Press(ScriptedButton.Dodge);

        /// <summary>Scripted input for the autopilot: presses Confirm for exactly one read, through the
        /// real command path so timing an active reload's sweet spot works the same way a human's does.</summary>
        public void TriggerConfirm() => Press(ScriptedButton.Confirm);

        /// <summary>Scripted input for the autopilot: presses Shootdodge for exactly one read.</summary>
        public void TriggerShootdodge() => Press(ScriptedButton.Shootdodge);

        private static void Press(ScriptedButton button)
        {
            var session = Session;
            session?.Submit(new PressCommand(session.LocalPlayer, button));
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
            var move = mag < StickDeadzone ? Vector2.zero : raw / mag * Mathf.Min(1f, (mag - StickDeadzone) / (1f - StickDeadzone));
            if (_walk.IsPressed()) move *= 0.45f;

            var nav = _navigate.ReadValue<Vector2>();
            var stepX = Axis(nav.x, _lastNavigate.x);
            var stepY = Axis(nav.y, _lastNavigate.y);
            _lastNavigate = nav;

            var buttons = IntentButtons.None;
            if (_confirm.WasPressedThisFrame()) buttons |= IntentButtons.Confirm;
            if (_confirm.IsPressed()) buttons |= IntentButtons.ConfirmHeld;
            if (_cancel.WasPressedThisFrame()) buttons |= IntentButtons.Cancel;
            if (_interact.WasPressedThisFrame()) buttons |= IntentButtons.Interact;
            if (_interact.IsPressed()) buttons |= IntentButtons.InteractHeld;
            if (_menu.WasPressedThisFrame()) buttons |= IntentButtons.Menu;
            if (_shootdodge.WasPressedThisFrame()) buttons |= IntentButtons.Shootdodge;
            if (_focusHeal.WasPressedThisFrame()) buttons |= IntentButtons.FocusHeal;
            if (_focusVitals.WasPressedThisFrame()) buttons |= IntentButtons.FocusVitals;
            if (_focusRiposte.WasPressedThisFrame()) buttons |= IntentButtons.FocusRiposte;

            // This device never changes the game itself: it sends the frame's intent for the local seat
            // through the one command pipeline, and the game reads that seat's controls back. The stick is
            // camera-relative, so the intent carries the yaw of the camera it was pushed under.
            var session = Session;
            var cameraYaw = CameraDirector.Instance != null ? CameraDirector.Instance.Yaw : 0f;
            session?.Submit(new IntentCommand(session.LocalPlayer, Time.frameCount, move.x, move.y, stepX, stepY, buttons, cameraYaw));
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
