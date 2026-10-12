---
codex: 1
project: UnityEve
code: UE
layer: bible
status: living
updated: 2026-10-11
---

# Unity Eve (UE): Bible

## 1. The one sentence {#UE-§1}
Unity Eve is a Unity 6 proof of concept showing that a cel-shaded, hand-inked survival-horror RPG can be built from
free assets: walk a street under fixed and CCTV cameras, enter houses, rummage through containers, and
fight with Parasite Eve's real-time/pause Active Time Battle.

## 2. The product promise {#UE-§2}
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

## 3. What it is NOT {#UE-§3}
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

## 4. Architecture canon {#UE-§4}

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
- **Time:**
  - `WorldTime` is the only writer of `Time.timeScale`.
  - Menus and targeting request a pause; Focus requests 0.15.
  - UI, camera servos and the Focus timer run on unscaled time.
- **Content:** `DemoContentBuilder` generates every data asset, material, clip, animator controller,
  prefab, sound and `Demo.unity`, deterministically.

## 5. The Laws {#UE-§5}
This project inherits the org-wide laws in [MindAttic.HouseRules.md](../../MindAttic.HouseRules.md),
including HOUSE-LAW-1 (whole-number versions, `version.txt`), HOUSE-LAW-8 (done is verified) and
HOUSE-LAW-10 (commit to main).

### {#UE-LAW-1} Core has no UnityEngine
`UnityEve.Core` is `noEngineReferences: true` and compiles as netstandard2.1 under `dotnet test`.
All randomness goes through the seeded `Rng`. *(Source: asmdef; test `Damage_never_drops_below_one_and_replays_from_seed`.)*

### {#UE-LAW-2} The pause freezes the world, never the menu
Enemy AI, telegraphs, the ATB, PE regeneration and player movement all run on scaled time. Menus, the
range dome bloom, camera servos and the Focus timer run on unscaled time. *(Test `Atb_does_not_fill_while_world_is_paused`,
`Enemy_runs_close_windup_strike_recover_and_respects_commit_limit`.)*

### {#UE-LAW-3} Focus is optional
A mistimed dodge still evades through i-frames, and the Focus window expires on its own. No encounter
requires Focus. *(Tests `Focus_window_expires_on_real_time_so_it_is_optional`, `Strike_resolution`.)*

### {#UE-LAW-4} Free assets only
Every third-party file is CC0, OFL, or MIT and listed in CREDITS.md. Missing sounds are synthesized, not
sourced. *(Owner widened this from CC0/OFL-only to also allow MIT 2026-10-11, to pull from more
low-poly model sources while building out the town; MIT assets carry an attribution requirement CC0/OFL
don't, so their CREDITS.md row must say so.)*

### {#UE-LAW-5} Generated content is regenerated, not hand-edited
Edit `DemoContentBuilder`, then re-run it. A hand edit to `Generated/` or `Demo.unity` is lost on the next build.

## 6. Verified state {#UE-§6}
- `dotnet test Tests/Core.Tests`: 66 passing.
- Unity 6000.4.3f1 batch-mode compile: clean.
- `UnityEve.exe -batchmode -autoplay`: PASS, with ~230 screenshots (count varies with the enemy-buff
  shot and which random encounters fire) covering everything in the original slice (above) plus, as of
  the 2026-10-11 town-expansion pass:
  - Ashgrove Park (cel-shaded fountain water) and Ashgrove School (two new alien enemies, the warden and
    the spitter)
  - the demo attract-mode loop (`-demo`) surviving a failed ending and playing its own perfect dodges
  - the active-reload minigame, raycast-measured cover, backpedal, and obstacle-aware movement
  - the free Shove and the Focus Riposte (upgraded from Counter); the hand-aimed Line Rush alongside
    the original lock-on Rush; the ranged Spitter's DOT puddle; the Wasp's dodgeable ranged sting
  - multi-stage "Rummaging..." containers (up to 3 items) with a glint marking anything still searchable
  - the lock-and-key solvability check (`Core/Story/LockGraph.cs`) passing for every door in the game
  - `-demo` smoke-checked separately (continuous loop, no batch exit — spot-checked, not run to a full lap
    every pass)

## 7. Active frontier {#UE-§7}
- Navmesh enemy movement
- Bonus Point menu
- Boss part targeting (ExperimentEve's bone-bound weak points)
- More interiors (the houses on Willow Lane are exteriors only)
- A recorded score in place of the synthesized loops

## 8. Quality bar {#UE-§8}
A change is done when all of these hold:
1. `tools/test.ps1 -Compile` is green.
2. The content builder has been re-run.
3. The player builds.
4. Autoplay passes and its screenshots have been looked at.
5. USER_STORIES is updated.
6. It is committed once.

## 9. Glossary {#UE-§9}
- **ATB / AT gauge:** Active Time Bar. Fills in real time; full means you may act.
- **Active Time:** PE1 stat that speeds the ATB. Bonus Points raise it.
- **Range dome:** the translucent hemisphere (radius = weapon range) shown while targeting.
- **PE:** Parasite Energy, spent on abilities; regenerates more slowly with each cast in a fight.
- **Focus:** slow-motion window opened by a perfect dodge; offers Quick Heal, Target Vitals, Counter.
- **Encounter zone:** trigger volume where creatures materialize and the arena seals.
- **Shot / zone:** an authored camera pose, and the trigger volume that selects it (Fixed, CCTV, Rail).
- **Rummage:** hold-to-search a container; rolls its loot table once.
