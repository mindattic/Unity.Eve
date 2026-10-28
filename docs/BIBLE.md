---
codex: 1
project: UnityEve
code: U.EVE
layer: bible
status: living
updated: 2026-10-11
---

# Unity Eve (UE): Bible

## 1. The one sentence {#U.EVE-§1}
Unity Eve is a Unity 6 proof of concept showing that a cel-shaded, hand-inked survival-horror RPG can be built from
free assets: walk a street under fixed and CCTV cameras, enter houses, rummage through containers, and
fight with Parasite Eve's real-time/pause Active Time Battle.

## 2. The product promise {#U.EVE-§2}
- It plays like Parasite Eve (1998):
  - The gauge fills while you move in real time.
  - The world stops when you act.
  - The weapon's range dome, a translucent shell of hexagons, decides what you can hit.
  - Creatures take turns like enemies in most action games (attack tokens): one engages at a time,
    stalking or flanking in, rearing up, charging, striking and retreating; the rest lurk, circle,
    enrage themselves or back off on a ring around Kat. It keeps the JRPG command-battle feel;
    Parasite Mutant is the reference for the action's pace, not its gauge.
  - Kat carries a katana with her gun: Slash cuts an arc in front, Rush dashes through a line of
    creatures. Both are menu commands, so the JRPG turn rhythm holds.
  - PE abilities and items round out the turn.
- It looks like Borderlands meets Parasite Mutant (owner direction 2026-10-11; the PS1 look is retired):
  - Every surface is cel-shaded (`UnityEve/Toon`): light arrives in flat, anti-aliased bands and pools;
    tiled textures are flattened toward painted colour. Clean is the bar (owner, 2026-10-11).
  - Ink lines are drawn like ink, not pixels: a full-screen pass (`UnityEve/InkOutline`) inks silhouettes
    (curvature of 1/depth, zero on any plane, so floors never streak) and creases, at an even width.
  - Full window resolution, 4x MSAA, 16:9; comic HUD panels in Bangers.
  - Fixed and CCTV cameras hard-cut between shots.
  - Nights are foggy.
- It is its own thing through one new idea: **Focus**, an optional perfect-dodge slow-motion window
  with free moves. Skilled players are rewarded; nobody is required to use it.
- It is a PC game for a controller first, with keyboard fallback.

## 3. What it is NOT {#U.EVE-§3}
- **Not a Parasite Mutant clone.** The owner directed (2026-10-10) that it follow PE1's single gauge, not
  Parasite Mutant's segmented "Active Time Chains".
- **Not a mobile game.** There are no touch controls and no portrait layout.
- **Not built on paid or non-redistributable assets.** That means no Mixamo, no Asset Store packs and no ripped game files.
- **Not a full game — yet.** It started as a vertical slice: one night with a beginning, a boss and an
  ending, every system working together, in a small town (Ashgrove Street, Ashgrove Heights, the Market
  district). The owner directed (2026-10-11) growing it into a fuller game from there — more districts
  (Ashgrove Park, a school, and beyond), more enemies and player moves, and the progression bones (keys,
  locked doors and gates, and eventually a plot and conversations) a bigger game needs — built the same
  deliberate way as the slice: generated, tested, and verified, not bolted on.

## 4. Architecture canon {#U.EVE-§4}

```text
 ┌───────────────────────────────┐   ┌──────────────────────────────────────┐
 │ UnityEve.Core  (no UnityEngine)│◄──│ UnityEve.Runtime  (MonoBehaviours)    │
 │ AtbGauge  ParasiteEnergy       │   │ BattleDirector  EnemyActor  RangeDome │
 │ DamageCalculator  FocusWindow  │   │ PlayerController PlayerCharacter      │
 │ EnemyAttackCycle  Progression  │   │ CameraDirector/Shot/Zone              │
 │ Inventory  LootTable  Rng      │   │ Door Container Pickup Readable        │
 │ CameraZoneSelector MovementBasis│  │ GameHud(UI Toolkit) WorldTime GameInput│
 └───────────────▲───────────────┘   └──────────────────▲───────────────────┘
                 │ dotnet test (Tests/Core.Tests)        │
                 │                     ┌─────────────────┴───────────────────┐
                 └─────────────────────│ UnityEve.Editor                      │
                                       │ DemoContentBuilder  WavSynth         │
                                       │ OutfitPainter  UnityEveBuild         │
                                       └─────────────────────────────────────┘
```

- **Nouns:** WeaponStats, ItemStats, AbilityStats, EnemyStats (Core data), each wrapped by a
  ScriptableObject `*Definition`.
- **Verbs:**
  - `BattleDirector.Begin/OpenCommandMenu/Execute`
  - `EnemyAttackCycle.Tick/Resolve`
  - `EnemyMind.Tick/Decide/Steer` + `AttackTokens.TryTake/Release` (turn-taking tactics over the attack cycle)
  - `Objectives.Learn` (story facts -> current objective), `SaveGame` (+ `SaveSystem.Save/Load`),
    `BossPhases.Check` (brood waves), `ScreenEncounterRule.Enter`
  - `BladeArts.InSlashArc/RushEnd/OnRushPath` (katana)
  - `FocusWindow.Open/TryUse`
  - `Inventory.Add/TryRemove`
  - `LootTable.Roll`
  - `CameraZoneSelector.Enter/Exit`
- **Input:** `GameInput` (the device) → `IntentCommand`/`PressCommand` → `ISession.Submit` (`LocalSession`)
  → the seat's `PlayerControls`, which the game reads (U.EVE-LAW-6).
- **Time:**
  - `WorldTime` is the only writer of `Time.timeScale`.
  - Menus and targeting request a pause; Focus requests 0.15.
  - UI, camera servos and the Focus timer run on unscaled time.
- **Content:** `DemoContentBuilder` generates every data asset, material, clip, animator controller,
  prefab, sound and `Demo.unity`, deterministically.
- **Tuning:** every battle number is a `BattleConfig` knob. Its shipped value lives in the hand-edited data file
  `Assets/UnityEve/Resources/UnityEve/battle-tuning.json` (U.EVE-T39).
  - `TuningLoader` validates the whole file before the first scene and applies it only if every value passes.
  - The code's initializers hold the same numbers: they are the fallback and what `dotnet test` runs on.
  - Retune both. The test `The_shipped_file_lists_every_knob_and_matches_the_built_in_values` keeps them in step.

## 5. The Laws {#U.EVE-§5}
This project inherits the org-wide laws in [MindAttic.HouseRules.md](../../MindAttic.HouseRules.md),
including HOUSE-LAW-1 (whole-number versions, `version.txt`), HOUSE-LAW-8 (done is verified) and
HOUSE-LAW-10 (commit to main).

### {#U.EVE-LAW-1} Core has no UnityEngine
`UnityEve.Core` is `noEngineReferences: true` and compiles as netstandard2.1 under `dotnet test`.
All randomness goes through the seeded `Rng`. *(Source: asmdef; test `Damage_never_drops_below_one_and_replays_from_seed`.)*

### {#U.EVE-LAW-2} The pause freezes the world, never the menu
Enemy AI, telegraphs, the ATB, PE regeneration and player movement all run on scaled time. Menus, the
range dome bloom, camera servos and the Focus timer run on unscaled time. *(Test `Atb_does_not_fill_while_world_is_paused`,
`Enemy_runs_close_windup_strike_recover_and_respects_commit_limit`.)*

### {#U.EVE-LAW-3} Focus is optional
A mistimed dodge still evades through i-frames, and the Focus window expires on its own. No encounter
requires Focus. *(Tests `Focus_window_expires_on_real_time_so_it_is_optional`, `Strike_resolution`.)*

### {#U.EVE-LAW-4} Free assets only
Every third-party file is CC0, OFL, or MIT and listed in CREDITS.md. Missing sounds are synthesized, not
sourced. *(Owner widened this from CC0/OFL-only to also allow MIT 2026-10-11, to pull from more
low-poly model sources while building out the town; MIT assets carry an attribution requirement CC0/OFL
don't, so their CREDITS.md row must say so.)*

### {#U.EVE-LAW-5} Generated content is regenerated, not hand-edited
Edit `DemoContentBuilder`, then re-run it. A hand edit to `Generated/` or `Demo.unity` is lost on the next build.

### {#U.EVE-LAW-6} Multiplayer-ready
This game is built so co-op or PvP can be added without re-architecting, per Unity.Multiplayer's
[Multiplayer-ready standard](https://mindattic.github.io/Unity.Multiplayer/articles/multiplayer-ready.html):
every player intent is a serializable command stamped with a player id and submitted through `ISession`; one
authority owns game state and presentation only reads state and events; there is no global "the player", only
player ids and `ISession.LocalPlayer`; rules are deterministic (seeded RNG, fixed tick, no `UnityEngine.Random` or
frame time); entities have stable ids; state and commands serialize; the game runs on a `LocalSession` behind
`ISession`; and a test plays commands from two player ids. A change that breaks any of these is a bug.

Where Unity.Eve stands (U.EVE-T26, 2026-10-26):
- **Commands and the seam: in place.** `Core/Session`: `GameInput` is only the local seat's device. Each frame
  it submits an `IntentCommand` (stick, navigation step, buttons) to `GameSession.Session`, a `LocalSession`.
  The autopilot's scripted presses go in as `PressCommand`s. Kat's controller, the battle director, the
  interactor and the menus read their seat's `PlayerControls` back (`PlayerCharacter.Seat`/`.Controls`); nothing
  reads the device directly. `CommandCodec` writes and reads commands as text. *(Tests `SessionTests`: two seats,
  a refused unseated or stale command, codec round trip, log replay.)*
- **Per player: in place.** `PlayerCharacter.Seat` owns its controls. `GameSession.PlayerOf(seat)` resolves a
  character, and `GameSession.Player` is just the local seat's.
- **State serializes: in place.** `SaveGame` (Core) is the plain-data snapshot.
- **Stable ids: in place.** World objects are keyed by the deterministic scene names `DemoContentBuilder`
  gives them.
- **One authority, fixed tick: in place** (U.EVE-T41). `Core/Sim/GameAuthority` owns the game state the rules
  touch: each seat's `PlayerBody` (movement, dodge and i-frames, roots and locks, shootdodge, backpedal, cover
  lean), its `PlayerState` (HP, PE, level, bag, magazines, haste/barrier) and the `BattleAuthority` (gauge, Focus,
  attack tokens, boss phases, every creature's `EnemyState`, puddles, and each turn as a plain C# routine).
  `GameSession.LateUpdate` steps it at a fixed 1/60 s of real time, each tick at the time scale `WorldTime` set
  (still its only writer), from the seat's `TickInput` (a press is held over to the next tick, so a frame that
  steps no tick never loses one). The MonoBehaviours only present: `PlayerController` is the body's
  CharacterController (`IBodyPhysics`) and animator, `EnemyActor` places its model between the last two ticks,
  `BattleDirector` shows the `BattleEvent`s and runs the paused menus, dome and aim line, which submit one
  `BattleCommand` through `ISession.Submit`; scripted placements go in as `TeleportCommand`s.
- **Determinism: in place for a battle.** A battle replays from its setup and seed plus its `ReplayLog` (each
  tick's input and time scale, each applied command): `BattleDirector.LastBattleLog` keeps the current one.
  The self-play's direct cheats (its heals) are not commands and are not in the log. *(Tests
  `AuthorityReplayTests`: a recorded battle replays to the same events and state, twice; a press held over a
  tickless frame; a fixed tick at any frame rate.)*

## 6. Verified state {#U.EVE-§6}
- `dotnet test Tests/Core.Tests` (Core rules, the session seam) and the Unity EditMode suite in `Assets/UnityEve/Tests/EditMode` (PlayerCharacter, WorldTime/MenuModel, SaveSystem, Cover), added 2026-10-26.
- Unity 6000.4.3f1 batch-mode compile: clean.
- `Unity.Eve.exe -batchmode -demo`: PASS, last run 2026-10-26 with 239 screenshots (the count varies with the enemy-buff
  shot and which random encounters fire) covering everything in the original slice (above) plus, as of
  the 2026-10-11 town-expansion pass:
  - Ashgrove Park (cel-shaded fountain water) and Ashgrove School (two new alien enemies, the warden and
    the spitter)
  - the self-play playing its own perfect dodges (the looping attract mode it once had was retired on
    2026-10-13, when `-demo` became the family-wide self-play flag that runs once and quits PASS/FAIL)
  - the active-reload minigame, raycast-measured cover, backpedal, and obstacle-aware movement
  - the free Shove and the Focus Riposte (upgraded from Counter); the hand-aimed Line Rush alongside
    the original lock-on Rush; the ranged Spitter's DOT puddle; the Wasp's dodgeable ranged sting
  - multi-stage "Rummaging..." containers (up to 3 items) with a glint marking anything still searchable
  - the lock-and-key solvability check (`Core/Story/LockGraph.cs`) passing for every door in the game

## 7. Active frontier {#U.EVE-§7}
- Navmesh enemy movement
- Bonus Point menu
- Boss part targeting (ExperimentEve's bone-bound weak points)
- More interiors (the houses on Willow Lane are exteriors only)
- A recorded score in place of the synthesized loops
- Online: add a `NetGameSession : ISession` on Unity.Multiplayer over the Core authority (U.EVE-LAW-6), and
  a player snapshot in the replay log so a recording made mid-game replays on its own

## 8. Quality bar {#U.EVE-§8}
A change is done when all of these hold:
1. `tools/test.ps1 -Compile` is green.
2. The content builder has been re-run.
3. The player builds.
4. Autoplay passes and its screenshots have been looked at.
5. USER_STORIES is updated.
6. It is committed once.

## 9. Glossary {#U.EVE-§9}
- **ATB / AT gauge:** Active Time Bar. Fills in real time; full means you may act.
- **Active Time:** PE1 stat that speeds the ATB. Bonus Points raise it.
- **Range dome:** the translucent hemisphere (radius = weapon range) shown while targeting.
- **PE:** Parasite Energy, spent on abilities; regenerates more slowly with each cast in a fight.
- **Focus:** slow-motion window opened by a perfect dodge; offers Quick Heal, Target Vitals, Counter.
- **Encounter zone:** trigger volume where creatures materialize and the arena seals.
- **Shot / zone:** an authored camera pose, and the trigger volume that selects it (Fixed, CCTV, Rail).
- **Rummage:** hold-to-search a container; rolls its loot table once.
