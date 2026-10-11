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
- **Not a full game.** It has one street, two interiors and three encounters, and no saves or music yet.

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
Every third-party file is CC0 or OFL and listed in CREDITS.md. Missing sounds are synthesized, not sourced.

### {#UE-LAW-5} Generated content is regenerated, not hand-edited
Edit `DemoContentBuilder`, then re-run it. A hand edit to `Generated/` or `Demo.unity` is lost on the next build.

## 6. Verified state {#UE-§6}
- `dotnet test Tests/Core.Tests`: 46 passing.
- Unity 6000.4.3f1 batch-mode compile: clean.
- `UnityEve.exe -batchmode -autoplay`: PASS, with 46-47 screenshots (the enemy-buff shot appears when a
  creature happens to power up) covering:
  - the camera tour
  - rummaging
  - 3 encounters won through the command menu, range dome, grenade and Focus
  - doors and searching the house interior
  - the padlocked shed refusing entry, the side-table key opening it, and the shotgun pickup
  - walking out of combat playing the walk/jog cycle
  - the Blade menu, a katana Rush and a Slash
  - the enemy intents each fight actually used (logged)
  - the field menu

## 7. Active frontier {#UE-§7}
- Navmesh enemy movement
- Bonus Point menu
- Saves at a phone or save point
- Music
- A boss with part targeting (ExperimentEve's bone-bound weak points)

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
