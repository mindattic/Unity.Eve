using System.Globalization;

namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// One seat's controls as one authority tick sees them: the latest stick and camera yaw, the buttons held
    /// now, and every press since the previous tick (a press is held over to the next tick, so a frame that
    /// steps no tick never loses one). It is plain data, so the replay log stores it as is.
    /// </summary>
    public readonly struct TickInput
    {
        public TickInput(float moveX, float moveY, float cameraYaw, IntentButtons buttons)
        {
            MoveX = moveX;
            MoveY = moveY;
            CameraYaw = cameraYaw;
            Buttons = buttons;
        }

        public static readonly TickInput None = new TickInput(0f, 0f, 0f, IntentButtons.None);

        /// <summary>Camera-relative stick, deadzoned, 0..1.</summary>
        public float MoveX { get; }
        public float MoveY { get; }
        /// <summary>Yaw (radians) of the camera the stick was pushed under; the authority turns the stick into
        /// a world direction with it, so the result does not depend on which camera is live when it ticks.</summary>
        public float CameraYaw { get; }
        public IntentButtons Buttons { get; }

        public bool Has(IntentButtons b) => (Buttons & b) != 0;
        public bool ConfirmPressed => Has(IntentButtons.Confirm);
        public bool CancelPressed => Has(IntentButtons.Cancel);
        public bool InteractPressed => Has(IntentButtons.Interact);
        public bool ShootdodgePressed => Has(IntentButtons.Shootdodge);
        public bool FocusHealPressed => Has(IntentButtons.FocusHeal);
        public bool FocusVitalsPressed => Has(IntentButtons.FocusVitals);
        public bool FocusRipostePressed => Has(IntentButtons.FocusRiposte);

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary><c>moveX|moveY|yaw|buttons</c>, floats round-tripped exactly.</summary>
        public string Encode() => string.Join("|", MoveX.ToString("R", Inv), MoveY.ToString("R", Inv),
            CameraYaw.ToString("R", Inv), ((int)Buttons).ToString(Inv));

        public static TickInput Decode(string[] f, int at) => new TickInput(
            float.Parse(f[at], Inv), float.Parse(f[at + 1], Inv), float.Parse(f[at + 2], Inv), (IntentButtons)int.Parse(f[at + 3], Inv));

        public override string ToString() => $"tick-input move({MoveX:0.##},{MoveY:0.##}) yaw {CameraYaw:0.##} {Buttons}";
    }
}
