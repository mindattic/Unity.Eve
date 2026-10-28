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
        public IntentCommand(PlayerId player, long frame, float moveX, float moveY, int navigateX, int navigateY, IntentButtons buttons,
            float cameraYaw = 0f)
        {
            Player = player;
            Frame = frame;
            MoveX = moveX;
            MoveY = moveY;
            NavigateX = Math.Sign(navigateX);
            NavigateY = Math.Sign(navigateY);
            Buttons = buttons;
            CameraYaw = cameraYaw;
        }

        public PlayerId Player { get; }
        /// <summary>The sender's frame number: frames arrive in order, and an older one is refused.</summary>
        public long Frame { get; }
        public float MoveX { get; }
        public float MoveY { get; }
        public int NavigateX { get; }
        public int NavigateY { get; }
        public IntentButtons Buttons { get; }
        /// <summary>Yaw (radians) of the camera the stick was pushed under, so the authority can turn
        /// camera-relative movement into a world direction without asking the camera.</summary>
        public float CameraYaw { get; }

        public override string ToString() => $"Intent {Player} f{Frame} move({MoveX:0.##},{MoveY:0.##}) yaw {CameraYaw:0.##} nav({NavigateX},{NavigateY}) {Buttons}";
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

    /// <summary>What a seat asks the battle authority to do from its command menu (or Focus window).</summary>
    public enum BattleOrder
    {
        /// <summary>Stop time and open the command menu (the gauge must be full).</summary>
        OpenMenu,
        /// <summary>Back out of the menu to real time without spending the turn.</summary>
        CancelMenu,
        Attack,
        BurstFire,
        Slash,
        Rush,
        /// <summary>The hand-aimed rush: <see cref="BattleCommand.X"/>/<see cref="BattleCommand.Z"/> is where it ends.</summary>
        LineRush,
        /// <summary>A PE ability: <see cref="BattleCommand.Index"/> into the seat's abilities.</summary>
        Ability,
        /// <summary>Use or throw <see cref="BattleCommand.Item"/>.</summary>
        Item,
        Reload,
        /// <summary>Swap to weapon <see cref="BattleCommand.Index"/>; takes no turn.</summary>
        Equip,
        Escape,
        /// <summary>Spend an open Focus window on move <see cref="BattleCommand.Index"/> (a FocusMove).</summary>
        FocusMove,
        /// <summary>Automation only (the self-play tour): open a Focus window as if a perfect dodge happened.</summary>
        DebugFocus,
    }

    /// <summary>
    /// One battle action, stamped with its seat. Targets are enemy ids the authority gave out; menus,
    /// the range dome and the aim line stay in presentation, which only ever submits the finished choice.
    /// </summary>
    public sealed class BattleCommand : IGameCommand
    {
        public BattleCommand(PlayerId player, BattleOrder order, int target = -1, int index = -1, string item = null, float x = 0f, float z = 0f)
        {
            Player = player;
            Order = order;
            Target = target;
            Index = index;
            Item = item ?? "";
            X = x;
            Z = z;
        }

        public PlayerId Player { get; }
        public BattleOrder Order { get; }
        /// <summary>Enemy id, or -1.</summary>
        public int Target { get; }
        /// <summary>Ability, weapon or Focus-move index, or -1.</summary>
        public int Index { get; }
        /// <summary>Item id, or empty.</summary>
        public string Item { get; }
        /// <summary>World point on the ground plane (Line Rush's end).</summary>
        public float X { get; }
        public float Z { get; }

        public override string ToString() => $"Battle {Player} {Order} target {Target} index {Index} item '{Item}' at ({X:0.##},{Z:0.##})";
    }

    /// <summary>
    /// Scripted placement of a seat's body (doors, respawn, the self-play tour stepping Kat up to a target):
    /// through the command pipeline so a replay puts her where the script did.
    /// </summary>
    public sealed class TeleportCommand : IGameCommand
    {
        public TeleportCommand(PlayerId player, float x, float y, float z, float yaw)
        {
            Player = player;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }

        public PlayerId Player { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        /// <summary>Facing, radians clockwise from +Z.</summary>
        public float Yaw { get; }

        public override string ToString() => $"Teleport {Player} ({X:0.##},{Y:0.##},{Z:0.##}) yaw {Yaw:0.##}";
    }
}
