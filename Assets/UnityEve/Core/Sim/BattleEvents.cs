using System;
using Unity.Eve.Core.Session;

namespace Unity.Eve.Core.Sim
{
    /// <summary>The kinds of thing a battle tells presentation about (sounds, effects, HUD; never rules).</summary>
    public enum BattleCue
    {
        /// <summary>The battle is set up and running (its creatures were each announced by <see cref="EnemySpawned"/>).</summary>
        Began,
        /// <summary><see cref="BattleEvent.Enemy"/> appeared: show it.</summary>
        EnemySpawned,
        /// <summary>The boss called up its brood.</summary>
        BroodCalled,
        AtbFilled,
        /// <summary>Time stopped for the command menu.</summary>
        MenuOpened,
        /// <summary>The command menu (and anything it raised: dome, aim line) is gone; time may run.</summary>
        MenuClosed,
        FocusOpened,
        FocusClosed,
        /// <summary>A strike landed inside a perfect dodge: the Focus window is open.
        /// <see cref="BattleEvent.Flag"/> is set when the self-play opened it without a dodge.</summary>
        PerfectDodge,
        /// <summary>A strike hit only i-frames ("MISS" over her).</summary>
        StrikeEvaded,
        /// <summary>A strike hit her for <see cref="BattleEvent.Amount"/>; <see cref="BattleEvent.Flag"/>: through cover.</summary>
        PlayerHit,
        /// <summary>A Focus Quick Heal restored <see cref="BattleEvent.Amount"/>.</summary>
        FocusHealed,
        /// <summary>She plays <see cref="BattleEvent.Motion"/>.</summary>
        Motion,
        /// <summary>A gunshot at <see cref="BattleEvent.Enemy"/> (tracer, report).</summary>
        Shot,
        /// <summary><see cref="BattleEvent.Enemy"/> took <see cref="BattleEvent.Amount"/>; <see cref="BattleEvent.Flag"/>:
        /// critical; <see cref="BattleEvent.Label"/>: a named move, or null.</summary>
        EnemyDamaged,
        /// <summary><see cref="BattleEvent.Enemy"/> died (it is already counted for EXP).</summary>
        EnemyKilled,
        /// <summary>A Burst Fire round went wide of <see cref="BattleEvent.Enemy"/>.</summary>
        ShotMissed,
        /// <summary>The free shove started (she plays it either way).</summary>
        Shove,
        /// <summary>The shove found nothing in front of her.</summary>
        ShoveMissed,
        /// <summary>The shove connected.</summary>
        ShoveLanded,
        /// <summary>Bullet time: the leap started.</summary>
        ShootdodgeBegan,
        /// <summary>The leap ended in a hard landing costing <see cref="BattleEvent.Amount"/> HP; bullet time ends.</summary>
        ShootdodgeLanded,
        /// <summary>A request the rules turned down (an empty clip, nothing in range).</summary>
        Denied,
        KatanaDrawn,
        KatanaSheathed,
        /// <summary>A blade cut connected with <see cref="BattleEvent.Enemy"/>; <see cref="BattleEvent.Flag"/>: critical.</summary>
        BladeHit,
        /// <summary>The Slash swing (before it lands).</summary>
        SlashSwing,
        /// <summary>The Slash landed.</summary>
        SlashLanded,
        /// <summary>The dash from <see cref="BattleEvent.At"/> to <see cref="BattleEvent.To"/> began.</summary>
        RushBegan,
        /// <summary>The dash arrived at <see cref="BattleEvent.To"/> having cut <see cref="BattleEvent.Amount"/> creatures.</summary>
        RushEnded,
        /// <summary>A PE ability (<see cref="BattleEvent.Index"/>) is being cast.</summary>
        Cast,
        /// <summary>An ability healed <see cref="BattleEvent.Amount"/>.</summary>
        AbilityHealed,
        /// <summary>Energy Shot (ability <see cref="BattleEvent.Index"/>) at <see cref="BattleEvent.Enemy"/>.</summary>
        EnergyShot,
        /// <summary>Haste for <see cref="BattleEvent.Value"/> seconds.</summary>
        Hasted,
        /// <summary>Barrier for <see cref="BattleEvent.Value"/> seconds.</summary>
        Barriered,
        /// <summary>Slow on every creature for <see cref="BattleEvent.Value"/> seconds.</summary>
        Slowed,
        /// <summary>A medicine healed <see cref="BattleEvent.Amount"/>.</summary>
        ItemHealed,
        PeRestored,
        /// <summary>She winds up a throw.</summary>
        ThrowStarted,
        /// <summary>The thrown item flies from <see cref="BattleEvent.At"/> to <see cref="BattleEvent.To"/> over
        /// <see cref="BattleEvent.Value"/> scaled seconds.</summary>
        Thrown,
        /// <summary>It went off at <see cref="BattleEvent.To"/>, radius <see cref="BattleEvent.Value"/>;
        /// <see cref="BattleEvent.Flag"/>: an explosion (otherwise a flash).</summary>
        Burst,
        /// <summary>A magazine went in; <see cref="BattleEvent.Flag"/>: an active (perfect) reload.</summary>
        Reloaded,
        Escaped,
        EscapeFailed,
        /// <summary>A toxic puddle (<see cref="BattleEvent.Index"/>) at <see cref="BattleEvent.At"/>.</summary>
        PuddleSpawned,
        /// <summary>A puddle burned her for <see cref="BattleEvent.Amount"/>.</summary>
        PuddleBurned,
        /// <summary>Puddle <see cref="BattleEvent.Index"/> dried up.</summary>
        PuddleGone,
        /// <summary>The last creature fell: the victory beat begins.</summary>
        VictoryBegan,
        /// <summary><see cref="BattleEvent.Amount"/> EXP was awarded.</summary>
        Victory,
        /// <summary>She reached level <see cref="BattleEvent.Amount"/>.</summary>
        LevelUp,
        /// <summary>She fell: the defeat beat begins.</summary>
        Defeated,
        /// <summary>The battle is over: <see cref="BattleEvent.Flag"/> won; <see cref="BattleEvent.Index"/> 1 when she was defeated.</summary>
        Ended,
    }

    /// <summary>An animation she plays for a battle beat.</summary>
    public enum PlayerMotion
    {
        Shoot,
        Slash,
        Rush,
        Cast,
        Throw,
        Shove,
        Die,
    }

    /// <summary>One thing that happened in a battle, for presentation to show. Only the fields its
    /// <see cref="Cue"/> names are set.</summary>
    public sealed class BattleEvent : IGameEvent
    {
        public BattleEvent(BattleCue cue) => Cue = cue;

        public BattleCue Cue { get; }
        public PlayerId Player { get; set; }
        public EnemyState Enemy { get; set; }
        public int Amount { get; set; }
        public bool Flag { get; set; }
        public int Index { get; set; }
        public float Value { get; set; }
        public string Label { get; set; }
        public Vec3 At { get; set; }
        public Vec3 To { get; set; }
        public PlayerMotion Motion { get; set; }

        /// <summary>One stable line per event, for the replay comparison.</summary>
        public override string ToString() => FormattableString.Invariant(
            $"{Cue} e{(Enemy != null ? Enemy.Id : -1)} n{Amount} {(Flag ? "T" : "F")} i{Index} v{Value:R} {Label} {At} {To} {(Cue == BattleCue.Motion ? Motion.ToString() : "")}");
    }
}
