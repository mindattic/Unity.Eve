namespace Unity.Eve.Core.Session
{
    /// <summary>
    /// One seat's controls as the game reads them: the latest <see cref="IntentCommand"/> applied for that
    /// player, plus any pending scripted presses. Edges from a controller are frame-stable (every reader in
    /// a frame sees the press); a scripted <see cref="PressCommand"/> is seen by the first reader only, so a
    /// cancel that closes a menu is not also a dodge.
    /// </summary>
    public sealed class PlayerControls
    {
        private IntentButtons _buttons;
        private bool _scriptedConfirm, _scriptedDodge, _scriptedShootdodge;

        internal PlayerControls(PlayerId player) => Player = player;

        public PlayerId Player { get; }
        /// <summary>Frame of the latest applied intent; -1 before the first.</summary>
        public long Frame { get; private set; } = -1;

        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        public int NavigateX { get; private set; }
        public int NavigateY { get; private set; }

        public bool ConfirmPressed => Has(IntentButtons.Confirm) || Consume(ref _scriptedConfirm);
        public bool ConfirmHeld => Has(IntentButtons.ConfirmHeld);
        public bool CancelPressed => Has(IntentButtons.Cancel) || Consume(ref _scriptedDodge);
        public bool InteractPressed => Has(IntentButtons.Interact);
        public bool InteractHeld => Has(IntentButtons.InteractHeld);
        public bool MenuPressed => Has(IntentButtons.Menu);
        public bool ShootdodgePressed => Has(IntentButtons.Shootdodge) || Consume(ref _scriptedShootdodge);
        public bool FocusHealPressed => Has(IntentButtons.FocusHeal);
        public bool FocusVitalsPressed => Has(IntentButtons.FocusVitals);
        public bool FocusRipostePressed => Has(IntentButtons.FocusRiposte);

        private bool Has(IntentButtons b) => (_buttons & b) != 0;

        private static bool Consume(ref bool flag)
        {
            if (!flag) return false;
            flag = false;
            return true;
        }

        internal void Apply(IntentCommand intent)
        {
            Frame = intent.Frame;
            MoveX = intent.MoveX;
            MoveY = intent.MoveY;
            NavigateX = intent.NavigateX;
            NavigateY = intent.NavigateY;
            _buttons = intent.Buttons;
        }

        internal void Press(ScriptedButton button)
        {
            switch (button)
            {
                case ScriptedButton.Confirm: _scriptedConfirm = true; break;
                case ScriptedButton.Dodge: _scriptedDodge = true; break;
                case ScriptedButton.Shootdodge: _scriptedShootdodge = true; break;
            }
        }
    }
}
