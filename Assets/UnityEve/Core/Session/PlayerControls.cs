namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// One seat's controls as the game reads them: the latest <see cref="IntentCommand"/> applied for that
    /// player, plus any pending scripted presses. Edges from a controller are frame-stable (every reader in
    /// a frame sees the press); a scripted <see cref="PressCommand"/> is seen by its first reader only, so
    /// a cancel that closes a menu is not also a dodge. The authority reads the seat once per fixed tick through
    /// <see cref="TakeTick"/>, which also holds every press over until the tick that takes it.
    /// </summary>
    public sealed class PlayerControls
    {
        /// <summary>The buttons that are presses (edges), not holds.</summary>
        public const IntentButtons Edges = IntentButtons.Confirm | IntentButtons.Cancel | IntentButtons.Interact |
            IntentButtons.Menu | IntentButtons.Shootdodge | IntentButtons.FocusHeal | IntentButtons.FocusVitals |
            IntentButtons.FocusRiposte;

        private IntentButtons _buttons;
        private IntentButtons _latched;
        private bool _scriptedConfirm, _scriptedDodge, _scriptedShootdodge;

        internal PlayerControls(PlayerId player) => Player = player;

        public PlayerId Player { get; }
        /// <summary>Frame of the latest applied intent; -1 before the first.</summary>
        public long Frame { get; private set; } = -1;

        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        /// <summary>Yaw of the camera the latest intent was made under (radians).</summary>
        public float CameraYaw { get; private set; }
        public int NavigateX { get; private set; }
        public int NavigateY { get; private set; }

        public bool ConfirmPressed => Has(IntentButtons.Confirm) || Consume(ref _scriptedConfirm, IntentButtons.Confirm);
        public bool ConfirmHeld => Has(IntentButtons.ConfirmHeld);
        public bool CancelPressed => Has(IntentButtons.Cancel) || Consume(ref _scriptedDodge, IntentButtons.Cancel);
        public bool InteractPressed => Has(IntentButtons.Interact);
        public bool InteractHeld => Has(IntentButtons.InteractHeld);
        public bool MenuPressed => Has(IntentButtons.Menu);
        public bool ShootdodgePressed => Has(IntentButtons.Shootdodge) || Consume(ref _scriptedShootdodge, IntentButtons.Shootdodge);
        public bool FocusHealPressed => Has(IntentButtons.FocusHeal);
        public bool FocusVitalsPressed => Has(IntentButtons.FocusVitals);
        public bool FocusRipostePressed => Has(IntentButtons.FocusRiposte);

        private bool Has(IntentButtons b) => (_buttons & b) != 0;

        /// <summary>A scripted press read here is used up: the tick does not see it again.</summary>
        private bool Consume(ref bool flag, IntentButtons latched)
        {
            if (!flag) return false;
            flag = false;
            if ((_buttons & latched) == 0) _latched &= ~latched;
            return true;
        }

        internal void Apply(IntentCommand intent)
        {
            Frame = intent.Frame;
            MoveX = intent.MoveX;
            MoveY = intent.MoveY;
            CameraYaw = intent.CameraYaw;
            NavigateX = intent.NavigateX;
            NavigateY = intent.NavigateY;
            _buttons = intent.Buttons;
            _latched |= intent.Buttons & Edges;
        }

        internal void Press(ScriptedButton button)
        {
            switch (button)
            {
                case ScriptedButton.Confirm: _scriptedConfirm = true; _latched |= IntentButtons.Confirm; break;
                case ScriptedButton.Dodge: _scriptedDodge = true; _latched |= IntentButtons.Cancel; break;
                case ScriptedButton.Shootdodge: _scriptedShootdodge = true; _latched |= IntentButtons.Shootdodge; break;
            }
        }

        /// <summary>
        /// Drops presses already used up by a menu (the cancel that closed it must not also dodge, the confirm
        /// that chose an action must not also count as a press inside that action), so the next tick never
        /// sees them.
        /// </summary>
        public void Suppress(IntentButtons presses)
        {
            _latched &= ~presses;
            if ((presses & IntentButtons.Confirm) != 0) _scriptedConfirm = false;
            if ((presses & IntentButtons.Cancel) != 0) _scriptedDodge = false;
            if ((presses & IntentButtons.Shootdodge) != 0) _scriptedShootdodge = false;
        }

        /// <summary>The seat's input for one authority tick: holds as they are now, plus every press since the
        /// last tick, which this clears. A scripted press the tick takes is used up (the tick is its reader).</summary>
        public TickInput TakeTick()
        {
            var input = new TickInput(MoveX, MoveY, CameraYaw, (_buttons & ~Edges) | _latched);
            _latched = IntentButtons.None;
            _scriptedConfirm = _scriptedDodge = _scriptedShootdodge = false;
            return input;
        }
    }
}
