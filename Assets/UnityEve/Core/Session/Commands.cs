using System;

namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// A player's request to the game, stamped with the seat that made it. Commands hold plain data only
    /// (numbers, enums, ids), so they serialize (<see cref="CommandCodec"/>) and could cross a wire unchanged.
    /// Every command goes through one pipeline, <see cref="ISession.Submit"/>.
    /// </summary>
    public interface IGameCommand
    {
        PlayerId Player { get; }
    }

    /// <summary>The buttons of one frame of a player's controls: edges (pressed this frame) and holds.</summary>
    [Flags]
    public enum IntentButtons
    {
        None = 0,
        Confirm = 1 << 0,
        ConfirmHeld = 1 << 1,
        /// <summary>Cancel in menus, dodge in the field.</summary>
        Cancel = 1 << 2,
        Interact = 1 << 3,
        InteractHeld = 1 << 4,
        Menu = 1 << 5,
        Shootdodge = 1 << 6,
        FocusHeal = 1 << 7,
        FocusVitals = 1 << 8,
        FocusRiposte = 1 << 9,
    }

    /// <summary>
    /// One rendered frame of a player's controls: the movement stick (deadzoned, 0..1), the menu
    /// navigation step (-1, 0, +1 per axis, edge-triggered) and the buttons. Kat is a real-time action
    /// character, so her intent is sampled every frame rather than as discrete moves; the seat's
    /// <see cref="PlayerControls"/> holds the latest one for the game to read.
    /// </summary>
    public sealed class IntentCommand : IGameCommand
    {
        public IntentCommand(PlayerId player, long frame, float moveX, float moveY, int navigateX, int navigateY, IntentButtons buttons)
        {
            Player = player;
            Frame = frame;
            MoveX = moveX;
            MoveY = moveY;
            NavigateX = Math.Sign(navigateX);
            NavigateY = Math.Sign(navigateY);
            Buttons = buttons;
        }

        public PlayerId Player { get; }
        /// <summary>The sender's frame number: frames arrive in order, and an older one is refused.</summary>
        public long Frame { get; }
        public float MoveX { get; }
        public float MoveY { get; }
        public int NavigateX { get; }
        public int NavigateY { get; }
        public IntentButtons Buttons { get; }

        public override string ToString() => $"Intent {Player} f{Frame} move({MoveX:0.##},{MoveY:0.##}) nav({NavigateX},{NavigateY}) {Buttons}";
    }

    /// <summary>A single scripted button press (the autopilot, tests): seen by the first reader only.</summary>
    public enum ScriptedButton
    {
        Confirm,
        /// <summary>Cancel/Dodge.</summary>
        Dodge,
        Shootdodge,
    }

    /// <summary>
    /// Presses one button once, through the same pipeline as a held controller, so scripted play times a
    /// perfect dodge or an active reload exactly the way a human's press does.
    /// </summary>
    public sealed class PressCommand : IGameCommand
    {
        public PressCommand(PlayerId player, ScriptedButton button)
        {
            Player = player;
            Button = button;
        }

        public PlayerId Player { get; }
        public ScriptedButton Button { get; }

        public override string ToString() => $"Press {Player} {Button}";
    }
}
