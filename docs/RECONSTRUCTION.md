---
codex: 1
project: UnityEve
code: U.EVE
layer: reconstruction
status: living
updated: 2026-10-26
---

# Unity Eve (U.EVE): Reconstruction of ExperimentEve

## 0. What this is {#U.EVE-RECON-§0}
Unity.Eve's README calls it "the Unity port of ExperimentEve ... whose battle numbers, enemy rhythm,
camera rules and outfit painter it reuses". This file has three parts:
- §2 rebuilds the original (`mindattic/ExperimentEve`) system by system, in enough detail to rebuild it.
- §2 also gives each system's Unity.Eve status:
  - **Ported**, citing the Unity file.
  - **Replaced (owner)**, citing the BIBLE section or owner-request commit, with its date.
  - **Not carried over.**
- §3 checks the four claimed reuses value for value.

Original paths are relative to the ExperimentEve repo, at commit `74ffb77` (2026-10-03). Unity paths are
relative to `Assets/UnityEve/`. Line numbers are as of that commit and Unity.Eve `origin/main` on 2026-10-26.

## 1. The original at a glance {#U.EVE-RECON-§1}
ExperimentEve is one game built twice in TypeScript, with no engine and no physics library.

| Build | Scope | Look | Notes |
| --- | --- | --- | --- |
| `three/src` (~17k lines) | The full slice: Kingsport North End on Sun 21 June 1998, 8 PM to dawn. Kat Weiss, an ER doctor, washes ashore. Includes crafting, the pawnshop, the A-Life world, about 50 enemy types, cats and scares. | PS1: 640x480, Bayer dither, RGB555, vertex snap, affine UVs. | `main.ts` (1775 lines) is the glue. |
| `babylon/src` (~7k lines) | A rebuild of the core loop only: movement, fixed cameras, the ATB battle, 7 rigged species and a simple world AI. | Full resolution, PBR, ACES, IBL (`render/sceneSetup.ts:1-29`). | Its own comments name the fixes it makes to the Three build (camera blends, the rotating movement basis, the four-beat enemy attack). |

Supporting material:
- `docs/GAZETTEER.md`: real Newport, RI, mapped to in-game names (Kingsport, Bell Bridge, ...).
- `universe/eve.universe.json`: story canon.

**Unity.Eve's sources:** almost everything Unity.Eve reuses comes from the **Babylon** build, not the Three build:
- the four-beat attack, `MAX_COMMITTED`
- the 0.16 s movement-basis grace
- the outfit painter
- the Quaternius rig pairing
- the 0.85-1.15 strike roll

## 2. System inventory and Unity status {#U.EVE-RECON-§2}

### 2.1 Battle / ATB {#U.EVE-RECON-§2.1}
- **Phases (original):** `inactive / active / menu / aim / fire / won`, plus these extras:
  - Three: `gunfu`, `sweepH`, `sweepV` (`three/src/battle/battle.ts:11-20`).
  - Babylon: `sweepH`, `sweepV` (`babylon/src/battle/battleSystem.ts:32-40`).
- **Pause:** `wantsPause` is true in menu, aim and sweep (`battleSystem.ts:128-131`). `GameClock` then sets
  `gameDt = 0` while `realDt` runs on (`three/src/core/clock.ts:10-13`). Enemies, regen and the ATB use `gameDt`.
  Menus, the dome bloom and the fire timer use `realDt`.
- **ATB:** `atb += atbRatePerSec * gameDt`. When the gauge is full, Confirm opens the menu (`battleSystem.ts:247-248`).
  It plays a chirp once per fill (`:194-200`).
- **Menu (Babylon):** Attack, Precision Aim (Limit 100), Reload, Escape (`battleSystem.ts:342-379`).
- **Menu (Three) adds:**
  - Proliferation heal 30 for 25 PE.
  - Five infusions.
  - Taunt.
  - Rapid Fire.
  - Blind Fire (only in cover).
  - Smoke bomb, Molotov, Flamethrower, Fire Axe.
  (`battle.ts:418-696`)
- **Aim:**
  - Targets every active enemy *part*, cycled with left and right (`battleSystem.ts:418-468`).
  - Babylon filters out targets beyond `WEAPON_RANGE = 6`. Three does not filter (`battle.ts:778-793`).
- **Precision Aim:** a horizontal sweep (1.3 s ping-pong), then a vertical sweep (0.85 s), then a screen-space
  hit test with a forgiving radius `part.radius * 1.6 / viewH` (`battleSystem.ts:472-558`).
  - A weak point takes x6 damage and a 1.8 s stun.
  - A body hit takes x2.
- **Limit gauge:**
  - Filled by: damage dealt x0.6, damage taken x0.8, and +7 per evade (`battleSystem.ts:311,628`; `gameState.ts:50`).
- **Learned weak points:**
  - First blood reveals a species' weak points.
  - The species is then added to the bestiary, and its weak points glow on sight (`battleSystem.ts:645-652`).
- **End conditions:**
  - Disengage: more than 16 m (Babylon) or 18 m (Three) from every enemy (`battleSystem.ts:48,242`; `battle.ts:358`).
  - Escape: always succeeds.
  - Victory: shown for 1.6 s (Babylon) or 2.1 s (Three).
  - XP: the sum of the enemies' maxHp. Next level needs 100 x level. Each level gives 2 skill points (`three/src/gameplay/gameState.ts:48-66`).
- **Three's style layer:**
  - Gun-fu from a dodge: x1.5 damage.
  - Knee slide: x2.
  - High ground: x1.25.
  - Taunt: enemies run x1.3 speed, and your shots have a 25% chance of x3 (`battle.ts:90-92,1001-1015`).
  - Letterbox: 7% bars over 0.35 s (`battle.ts:186-204`).

**Unity status.**
- **Ported (re-implemented):**
  - ATB pause loop: `Runtime/Battle/BattleDirector.cs`, `Core/Battle/AtbGauge.cs`, `Runtime/Game/WorldTime.cs`.
  - Letterbox, 7% over 0.35 s: `Runtime/UI/GameHud.cs:353-355`.
  - ATB-ready chirp: `BattleDirector.cs:109-113`.
- **Replaced (owner):**
  - The ATB formula: PE1's `(50+AT)×mult`, owner 2026-10-10 (BIBLE §2-§3).
  - Menu: Attack/PE/Item/Equip/Escape plus Blade (BIBLE §2, katana commit `c6a35bd`, 2026-10-11).
  - Range dome: hexagon dome, `Runtime/Battle/RangeDome.cs`, `Shaders/HexDome.shader` (BIBLE §2, commit `ec8901d`, 2026-10-11).
  - Precision Aim, the Limit gauge, gun-fu, knee slide and taunt: replaced by **Focus** (BIBLE §2 "its own thing
    through one new idea") and the shootdodge (commit `05b21cd`, 2026-10-11).
  - Disengage-by-distance: replaced by sealed arena walls (`Runtime/Battle/EncounterZone.cs`).
  - Escape: now chance-based (`Core/Battle/Progression.cs:58-66`).
- **Not carried over:**
  - Part targeting, weak points and the bestiary. These are listed as frontier in BIBLE §7. `BodyPartStats`
    (`Core/Battle/Definitions.cs:89-94`) exists but nothing reads it.

### 2.2 Damage {#U.EVE-RECON-§2.2}
- **Player shot:** `takeHit(gunDamage, part)` gives `round(part.flatDamage ?? amount × part.damageMultiplier)`
  (`babylon/src/enemies/enemyActor.ts:461-477`).
  - No random variance, no random crits, no defence.
  - Weak-point parts are x2.5-x3.5. Armoured parts deal flat 1 or 2.
- **Gun damage:** `gunDamage = weapon.damage + skills.damage×1.5 + mods.damage×3` (`three/src/gameplay/gameState.ts:128-130`).
  Babylon uses a flat 10 (`babylon/src/gameplay/gameState.ts:17`).
- **Enemy strike:** `round(attackDamage × (0.85 + rand×0.3))` (`battleSystem.ts:315`).
  - Three's player modifiers:
    - Cover halves it (`battle.ts:328`).
    - Worn gear absorbs up to 30% (`three/src/gameplay/gameState.ts:36-42`).
- **Stagger:** when `recentDamage ≥ max(20, 0.35×maxHp)`, stun for 1.8 s. `recentDamage` decays at 14/s and
  resets when the stun ends (`enemyActor.ts:467-470,481,486`). Three uses 30 and 2.2 s (`three/src/enemies/enemyBase.ts:57-59,97,102`).
- **Three extras:**
  - Rapid Fire: each round has a 30% miss chance and a 12% chance of a x3 burst round (`battle.ts:1042-1083`).
  - Blind Fire: x0.6, 35% miss.
  - Molotov: 35 within 3 m.
  - Axe: 25 at 1.9 m.

**Unity status.** **Replaced:**
- Rolls: PE1-style rolls with variance, random crits, defence and the rate-of-fire table
  (`Core/Battle/DamageCalculator.cs`, owner 2026-10-10, BIBLE §2).
- Strike roll: kept at 0.85-1.15 (`BattleDirector.cs:354`).
- Cover: now raycast exposure (`Runtime/Battle/CoverProp.cs`, BIBLE §6, 2026-10-11).
- Stagger: ported (`Core/Battle/StaggerRule.cs`, `StaggerMeter`); bug B1 fixed by U.EVE-T34.

### 2.3 Parasite Energy {#U.EVE-RECON-§2.3}
- **Original (Three):**
  - PE 40/100, regenerating at a flat 2.5/s, plus 1.5 with the lab coat (`three/src/gameplay/gameState.ts:86-88,149-163`).
  - Spending: Proliferation (25), then infusions at 20-30 PE each (`three/src/gameplay/infusions.ts:21-47`).
- **Original (Babylon):** has no PE.

**Unity status.** **Replaced:**
- PE1 regen falloff per cast (`Core/Battle/ParasiteEnergy.cs`).
- Five PE1 abilities: Heal, Energy Shot, Haste, Slow, Barrier (`Editor/DemoContentBuilder.cs:216-220`, BIBLE §2).

### 2.4 Items / inventory {#U.EVE-RECON-§2.4}
- **Item list:** `three/src/gameplay/inventory.ts` has ~45 items: components, crafted items, consumables, ammo,
  injectors, baubles, gear, guns, gun parts, tapes and keepsakes.
- **Inventory:** an unbounded count map (`:311-354`).
- **Crafting:** RE-style Combine recipes (`:276-309`). Some need a blueprint.
- **Gun tinkering:** wield, strip and install parts (`:243-262`).
- **Gear:** armour from clothing.
- **Inventory menu:** mark 2-3 items to combine (`three/src/ui/inventoryMenu.ts`).
- **Loot:** keyed by room and container (`three/src/gameplay/interactables.ts:14-62`).
- **Salvage:** cars, couches, trash piles (`:69-101`).

**Unity status.**
- **Ported in spirit:**
  - Contextual loot: `Core/Items/LootTable.cs` and `Runtime/Interaction/Container.cs` (multi-dig rummage, BIBLE §6).
- **Replaced:**
  - Bag: a slot-limited PE1 bag, 12 slots (`Core/Items/Inventory.cs`, `Runtime/Player/PlayerCharacter.cs:19`), with
    ammo taking slots (README).
  - Items: medicine, cola, grenades, flash grenades, tonic and keys (`DemoContentBuilder.cs:163-174`).
- **Not carried over:** crafting, gun tinkering, gear armour, baubles, salvage.

### 2.5 Enemies and roster {#U.EVE-RECON-§2.5}
**Three build: 53 registry ids** (`three/src/enemies/registry.ts:47-117`):
- 23 animal-chimera ids (21 classes; the Crow Spider has 3 variants), each with a hand-coded state machine and its own telegraph. Examples:
  - The frog boss.
  - The Crow Spider: wing-flutter telegraph.
  - Drain Coil.
- 20 Afflicted human chimeras (`afflictedBase.ts`): shamble, and topple for double damage.
- 10 segmented Grafted minibosses (`grafted.ts:41-97`).
- The Erasure trooper, spawned outside the registry.
- Hand-built `Enemy` parts with damage multipliers (`enemyBase.ts:5-18`).

**Babylon build: 7 rigged species.** These are rat, frog, spider, snake, wasp, bat and slime, with a stats table
(`babylon/src/enemies/enemyActor.ts:66-166`). Parts are bound to real bones.

**Unity status.**
- **Ported, re-tuned:** the 7 Babylon species (`DemoContentBuilder.cs:244-252`). See §3.2.
- **Replaced (owner):** 6 procedural aliens (`DemoContentBuilder.Aliens.cs`) and the Mito Mother boss (`:264-267`).
  - The spitter and warden came from owner direction on 2026-10-11 (`DemoContentBuilder.cs:258-262`).
  - The town roster grew per BIBLE §3 (2026-10-11).
- **Not carried over:** the Three roster, the Afflicted and the Grafted.

### 2.6 World AI {#U.EVE-RECON-§2.6}
- **Three, "A-Life":**
  - Wanderers with `wander/fight/flee/chase/eat` modes.
  - Packs and factions; morale breaks at 30% (`three/src/enemies/worldAI.ts:12-83`).
  - Nests that restock over time; burning a nest destroys it.
  - Corpses get eaten, which grows the eater.
  - Ambient rats.
  - Contact with Kat pulls her into battle.
- **Babylon:** only per-species `flee/stalk/hold` reactions (`babylon/src/enemies/worldAI.ts:1-40`).

**Unity status.** **Not carried over.** In its place:
- Fixed encounter zones that materialize packs and seal walls (`Runtime/Battle/EncounterZone.cs`).
- Re-entry random encounters (`Core/Battle/ScreenEncounterRule.cs`, `Runtime/Battle/ScreenEncounters.cs`).

### 2.7 Enemy attack rhythm {#U.EVE-RECON-§2.7}
Babylon `combatTick` (`enemyActor.ts:292-394`) runs four beats.

| Beat | What happens | Lines |
| --- | --- | --- |
| **close** | Turns toward Kat (rate 9) and moves at `speed`. In reach but not allowed to commit: prowls at reach×0.92, speed×0.55. In reach, allowed, and off cooldown: starts the windup, anchors Kat's position, plays its attack clip and voice. | `:310-338`, `:406-425` |
| **windup** | Tracks weakly (rate 2.5) and backs off at speed×0.18. The emissive heat ramps to ≤0.52 red, kept under bloom. | `:340-354`, `:514-523` |
| **strike** | 0.14 s lunge to the anchor at speed×3.2, then the hit. | `:356-373` |
| **recover** | Waits `recover`, then sets `cooldown`. | `:375-381` |

Supporting rules:
- **Pack limit:** `MAX_COMMITTED = 2` counted live (`battleSystem.ts:50,217-230`).
- **Connect and evade:** distance ≤ reach+0.35 connects. i-frames are checked before distance (`:301-313`).
- **Stun:** cancels the swing and sets cooldown to at least 0.35 (`enemyActor.ts:264-271`).
- **Start offset:** each enemy starts with a random cooldown of 0-0.6 s (`:224`).
- **Hover:** fliers hover and dive to 0.75 (`:384-390`).

**Unity status.**
- **Ported:**
  - The four beats: `Core/Battle/EnemyAttackCycle.cs` and `Runtime/Enemies/EnemyActor.cs:284-325`.
  - Commit limit: `BattleDirector.cs:210`.
- **Replaced (owner):**
  - Turn-taking: attack tokens plus `EnemyMind` (stalk, flank, rear up, charge, retreat, buff). This came from
    owner feedback in commit `bbcd6d8` (2026-10-11) and BIBLE §2. The original prowl is gone; `MAX_COMMITTED`
    stays as a "safety net" (`EnemyActor.cs:247`).
  - Ranged strikes (wasp, spitter): BIBLE §6, 2026-10-11.

### 2.8 Cameras, input latch, movement basis {#U.EVE-RECON-§2.8}
- **Zones:** each zone is a convex XZ polygon with:
  - an authored camera position, look-at and FOV (default 55);
  - an authored input `forward/right`, so top-down shots still have a basis;
  - `hysteresisMargin` (default 0.5);
  - a mode, `fixed` or `cctv`, and fisheye (`three/src/camera/cameraZone.ts:6-54`).
- **Two-threshold hysteresis:**
  - The active zone holds until Kat leaves its polygon inflated by the margin.
  - A new zone takes over only when its core polygon contains her.
  - In a gap between zones, the shot holds (`three/src/camera/cameraManager.ts:41-59`).
- **Cuts and tracking (Three):**
  - Hard cut.
  - CCTV pans with servo `lerp min(1, dt×2.2)` (`:76-82`).
  - Trauma shake: amplitude trauma²×0.14, decay 1.6/s, phase ×40 (`:61-73`).
- **Babylon changes** (`babylon/src/camera/fixedCamera.ts:4-176`):
  - A 0.28 s eased cut blend.
  - Fixed shots re-aim 42% toward Kat, with velocity lead 0.55 and drift 0.012.
- **Input:**
  - Three: hard-latches the basis until the stick returns to neutral (`three/src/camera/inputLatch.ts:10-42`).
  - Babylon: holds a cut for a 0.16 s grace, then rotates at 0.9π rad/s. Neutral adopts the new basis at once
    (`babylon/src/camera/movementBasis.ts:19-87`).

**Unity status.**
- **Ported:**
  - Zones and selection: `Core/Camera/CameraZoneSelector.cs` (box triggers with 1 m overlap and priority).
  - Shots: `Runtime/Camera/CameraShot.cs`, with Fixed, CCTV (yaw and pitch clamped) and a new Rail mode.
  - Director and shake: `Runtime/Camera/CameraDirector.cs`.
  - Movement basis: `Core/Camera/MovementBasis.cs`.
- **Hard cuts:** Unity keeps them, per BIBLE §2 ("hard-cut between shots"). It does not use Babylon's blend, pull
  or drift. The rail mode is new.
- See §3.3 and bug B2.

### 2.9 Player controller, rig, outfit {#U.EVE-RECON-§2.9}
- **Controller:**
  - Run speed 4.0 (Three scales it by +3% per speed point). Collision radius 0.35, a circle against AABBs and
    segments in XZ (`three/src/physics/colliders.ts`).
  - Dodge: 0.34 s, i-frames 0.5 s, cooldown 0.9 s.
  - Knee slide: 0.6 s, i-frames 0.45 s, cooldown 1.1 s (`three/src/player/playerController.ts:4-6,64-75`;
    `babylon/src/player/playerController.ts:5,20,106-114`).
- **Rig:**
  - Three: a procedural primitive rig (`three/src/player/playerRig.ts`).
  - Babylon: the Quaternius Universal Base Character on the Universal Animation Library skeleton, a matched rig
    so no retargeting is needed (`babylon/src/player/playerCharacter.ts:13-25`).
- **Outfit painter:** `babylon/src/player/outfit.ts`. See §3.4.

**Unity status.**
- **Ported:**
  - Dodge values: `Core/Battle/BattleConfig.cs:78-80`.
  - Run speed 4: `:84`.
  - Controller: `Runtime/Player/PlayerController.cs`, using a CharacterController in place of the 2D resolver.
  - Quaternius pairing: `Editor/DemoContentBuilder.Characters.cs`.
  - Outfit painter: `Editor/OutfitPainter.cs`.
- **Replaced (owner, 2026-10-11):**
  - Knee slide: replaced by the shootdodge (commit `05b21cd`).
  - Obstacle awareness and backpedal (BIBLE §6).

### 2.10 Level and props {#U.EVE-RECON-§2.10}
- **The level** (`three/src/level/level01.ts`) is data-driven: walls, boxes, fences, ground patches and triggers
  (`levelTypes.ts`). It is loaded by `levelLoader.ts`.
- **Route:** tracks, then Street A (house1 is enterable), then the cross street (the pawnshop and the
  SOAK/REN graffiti duel), then the south street, the blockade semi, the chain-link corridor and the garage.
- **Props:**
  - Kenney CC0 GLBs, converted to the PS1 material (`level/props/modelLoader.ts`).
  - Procedural facades and canvas textures (`facades.ts`, `textures.ts`).
  - The shore and rowboat (`shore.ts`) and the protest field (`protestField.ts`).
- **Babylon:** keeps the same layout, with PBR procedural surfaces (`babylon/src/level/materials.ts`).

**Unity status.** **Replaced (owner):**
- New districts: Ashgrove Street, Willow Lane, Kingsport Foods, the Market, Saint Ansel's, the Park and the School
  (BIBLE §3, 2026-10-11).
- Built by `Editor/DemoContentBuilder.*.cs`, with a glTF-to-toon `Model(...)` pipeline (AGENTS.md).
- Only the town name, Kingsport, survives.

### 2.11 Audio / SFX {#U.EVE-RECON-§2.11}
- Everything is synthesized with WebAudio at runtime.
- The master lowpass is ducked during the ATB pause. There are sfx and ambience buses (`three/src/audio/audioEngine.ts`).
- About 30 named one-shots, plus an ambience bed of wind, crickets and fire (`three/src/audio/sfx.ts:4-218`).
- No music.

**Unity status.** **Ported (re-implemented):**
- Clips are synthesized to WAV at content build (`Editor/WavSynth.cs`), plus Kenney CC0 UI and impact clips
  (`DemoContentBuilder.cs:296-358`, LAW-4).
- Added: synthesized music loops (`Editor/WavSynth.Music.cs`, `Runtime/Audio/MusicDirector.cs`).
- The pause duck is not carried over.

### 2.12 UI screens {#U.EVE-RECON-§2.12}
| Screen | Original | Unity status |
| --- | --- | --- |
| Title / death | Black screen, two options (`three/src/ui/titleScreen.ts`). | Ported: New Game / Continue, Continue on death (`Runtime/Game/GameSession.cs`, `GameHud.cs`). |
| HUD | Gauge cluster drawn on a tiny canvas and upscaled nearest-neighbour (`three/src/ui/hud.ts`); Babylon uses DOM (`babylon/src/ui/battleHud.ts`). | Replaced (owner 2026-10-11): UI Toolkit comic panels in Bangers (BIBLE §2, `UI/Hud.uss`). |
| Inventory | Combine and use (`inventoryMenu.ts`). | Replaced: ITEMS / Equip / Controls pause menu (`GameSession.cs:172-214`). |
| Lockpick | Skyrim-style pin and torque. The world keeps running (`lockpick.ts`, tolerance 0.3). | Not carried over. Keys and gates instead (`Core/Story/LockGraph.cs`). |
| Pawn | Trade baubles for 9mm at the mail slot (`pawnMenu.ts`). | Not carried over. |
| Stat menu | Spend skill points at the lighthouse lamp, then Save & Rest (`statMenu.ts`). | Replaced: Bonus Points are spent automatically on Active Time (`PlayerCharacter.cs:145`; BIBLE §7 lists the BP menu as frontier). |
| ApertureOS 98 | Save-point desktop with documents and an undeletable Trash (`apertureOS.ts`). | Not carried over. |
| Subtitles | Bark and dialog queue (`subtitles.ts`). | Replaced: one-line Readables and `GameHud.ShowMessage` (`Runtime/Interaction/Readable.cs`). |
| Damage numbers | White normal, yellow crit floaters (`damageNumbers.ts`). | Ported: `GameHud.Popup` (`GameHud.cs:322`; crit colour `BattleDirector.cs:521`). |

### 2.13 Save system {#U.EVE-RECON-§2.13}
- **Original:** `localStorage['eve-save']` (`three/src/gameplay/saveSystem.ts:4-102`).
  - Saves skills, level and XP, abilities, infusions, gear, weapon and mods, known weaknesses.
  - Saves HP, PE, clip and reserve, limit, flags, items, position and clock time.
  - Written at the lighthouse lamp and computer (`three/src/main.ts:877`).

**Unity status.** **Ported (re-implemented):**
- A versioned JSON file at `persistentDataPath/unity_eve_save.json`, written atomically (`Runtime/Game/SaveSystem.cs:27`, `Core/Story/SaveGame.cs`).
- Saved at payphones (`Runtime/Interaction/SavePoint.cs`).

### 2.14 Gameplay extras {#U.EVE-RECON-§2.14}
| Extra | Original | Unity status |
| --- | --- | --- |
| Cats | Five housecats that cannot be reached. A petted cat pays a debt with one guaranteed crit (`three/src/gameplay/cats.ts`). | Not carried over. |
| Scares | Timeline fake-outs that never deal damage (`scares.ts`). | Not carried over. |
| Infusions | Five injector "schools": Combustion 25, Cryostasis 20, Neuroelectric 30, Mitosis 25, Metabolic Burn 20 (`infusions.ts:21-47`; effects at `battle.ts:451-526`). | Replaced by the PE1 abilities (§2.3). |
| Erasure squad | View cone plus noise detection. An undetected execution is a free kill; being detected alerts the whole squad (`erasureSquad.ts`). | Not carried over. |
| Bike, ladders, VCR tapes, car doors, alarm, graffiti, 11 PM freight, world clock | `main.ts:490-1260`, `core/worldClock.ts`. | Not carried over. Unity tracks only `PlaySeconds`. |

### 2.15 Render pipeline {#U.EVE-RECON-§2.15}
- **Three:**
  - 640x480 internal target, letterboxed to 4:3 (`three/src/render/ps1/lowResPipeline.ts:4-12`).
  - Blit with a 4x4 Bayer matrix and RGB555 (`ditherQuantize.ts`).
  - Lambert shading with clip-space vertex snap and affine UV (`ps1Material.ts`).
  - Fog from 14 to 60 m (`main.ts:54`).
  - Pooled point particles (`render/particles.ts`).
- **Babylon:** PBR, ACES, IBL and exp2 fog (`babylon/src/render/sceneSetup.ts`).

**Unity status.** **Replaced (owner 2026-10-11):**
- Cel shading and an ink pass (`Shaders/Toon.shader`, `InkOutline.shader`).
- Full resolution and 4x MSAA. The PS1 target is retired (BIBLE §2; USER_STORIES US-040).

## 3. The four reused areas, value for value {#U.EVE-RECON-§3}
"Babylon" and "Three" mean the original files cited.

### 3.1 Battle numbers: ATB, gun multipliers, damage rolls {#U.EVE-RECON-§3.1}
The README's ATB formula is Parasite Eve 1998's. It is not ExperimentEve's. ExperimentEve never had a
`50 + ActiveTime` term, a shotgun, or a 0.8 multiplier.

| # | Value | Original (file:line) | Unity (file:line) |
| --- | --- | --- | --- |
| A1 | ATB formula | `0.16/s × (1+speed×0.06) × haste × atbMult × (1+grip×0.1)` (`three/src/gameplay/gameState.ts:119-122`); flat `0.42/s` (`babylon/src/gameplay/gameState.ts:21`) | `(50+AT−2·pen)×mult / 50 / 3.2 s` (`Core/Battle/AtbGauge.cs:20-28`, `BattleConfig.cs:12-13`) |
| A2 | Base fill time | 6.25 s (Three), 2.38 s (Babylon) | 3.2 s (`BattleConfig.cs:13`) |
| A3 | Minimum rate | none | 19 (`BattleConfig.cs:14`) |
| A4 | Haste ATB multiplier | 1.7 (Metabolic Burn, `three/.../gameState.ts:120`) | 2.0 (`BattleConfig.cs:15`) |
| A5 | Growth per level | +2 skill points, +6%/pt speed (`gameState.ts:62,120`) | +3 BP → +6 AT, about +12% (`Progression.cs:40,50`; `BattleConfig.cs:144`) |
| A6 | Gun ATB multipliers | Duty Pistol 1.0, .38 Snub 1.08, Erasure 0.95, Longslide 0.88 (`three/src/gameplay/inventory.ts:256-262`) | Pistol 1.0, Shotgun 0.8, Pipe 2.0, SMG 1.3, Rifle 0.6 (`Editor/DemoContentBuilder.cs:193-201`). Only "pistol 1.0" coincides. |
| A7 | Starting gun damage / clip | 10 / 8 (`inventory.ts:262`; `babylon/.../gameState.ts:13,17`) | Pistol 12 + level / 10 (`DemoContentBuilder.cs:193`; `BattleDirector.cs:515`) |
| A8 | Weapon range | 6 m for every gun (`battleSystem.ts:47`) | 7 / 4.5 / 1.9 / 7 / 14 m per weapon (`DemoContentBuilder.cs:193-201`) |
| A9 | Reload | 1.6 s × reloadMult (0.85-1.25) (`three/.../gameState.ts:132-135`) | 1.2 s bar, active reload (`BattleConfig.cs:126`) |
| A10 | Player shot variance | none: `round(amount × part mult)` (`enemyActor.ts:462`) | 0.9-1.1 (`BattleConfig.cs:24-25`, `DamageCalculator.cs:43`) |
| A11 | Random crit | none; crit means weak-point part or Precision (x6 weak, x2 body) (`battleSystem.ts:604,609,613`) | 8% × 1.75 (`BattleConfig.cs:26-27`) |
| A12 | Defence | none; armoured parts give flat 1/2 damage (`enemyActor.ts:94,163`) | `−defense×0.5`, floor 1 (`BattleConfig.cs:28`, `DamageCalculator.cs:46-47`) |
| A13 | Multi-round | Rapid Fire: whole clip, 30% miss, 12% x3 (`three/src/battle/battle.ts:1058-1062`) | PE1 share table 2→0.6 ... 7→0.182 (`DamageCalculator.cs:26-38`); SMG burst 8 @ 55% (`DemoContentBuilder.cs:198-199`) |
| A14 | Stagger decay | `recentDamage −14/s`, reset when the stun ends (`enemyActor.ts:481,486`) | Matches since U.EVE-T34 (`StaggerMeter`, `BattleConfig.StaggerDecayPerSecond`). Was bug B1. |
| A15 | PE pool / regen | 40 of 100, 2.5/s flat (`three/.../gameState.ts:86-88`) | max 60+10/level, 4/s × (1−0.14·casts), floor 0.05 (`BattleConfig.cs:18-21`, `Progression.cs:55`) |
| A16 | Player HP growth | 80 + 12 per health point (`three/.../gameState.ts:115`) | 80 + 14 per level (`Progression.cs:54`) |
| A17 | XP curve | 100 × level, XP = Σ enemy maxHp (`three/.../gameState.ts:48-50`; `battle.ts:1092`) | 60 × 1.45^(L−1), per-species `Exp` (`BattleConfig.cs:142-143`) |

**Values that match:**

| Value | Original | Unity |
| --- | --- | --- |
| Enemy strike roll | 0.85-1.15 (`battleSystem.ts:315`) | `BattleDirector.cs:354` |
| Hurt severity | dealt/14 (`battleSystem.ts:318`) | `BattleDirector.cs:357` |
| Stagger threshold | max(20, 0.35×maxHp), stun 1.8 (`enemyActor.ts:468-469`) | `BattleConfig.cs:137-139` |
| Dodge | 0.34 / 0.5 / 0.9 | `BattleConfig.cs:78-80` |
| Run speed | 4.0 | `BattleConfig.cs:84` |
| Base HP | 80 | 80 |
| Separation | radius sum + 0.14 (`battleSystem.ts:334`) | `EnemyActor.cs:195` |

### 3.2 Enemy rhythm {#U.EVE-RECON-§3.2}
These rules match the original:

| Rule | Original | Unity |
| --- | --- | --- |
| Beat order, close → windup → strike → recover | `enemyActor.ts:309-381` | `EnemyAttackCycle.cs:60-96` |
| Strike length 0.14 s | `enemyActor.ts:176` | `BattleConfig.cs:33` |
| Connect slack 0.35 | `enemyActor.ts:402` | `BattleConfig.cs:34`, `EnemyAttackCycle.cs:109` |
| `MAX_COMMITTED = 2`, counted live | `battleSystem.ts:50` | `BattleConfig.cs:32`, `BattleDirector.cs:210` |
| Stun cancels the swing, cooldown ≥ 0.35 | `enemyActor.ts:268-270` | `EnemyAttackCycle.cs:100-104` |
| Windup turn 2.5, back-off speed×0.18 | `enemyActor.ts:345-347` | `EnemyActor.cs:311-312` |
| Close turn rate 9 | `enemyActor.ts:313` | `EnemyActor.cs:291` |
| Lunge speed×3.2 | `enemyActor.ts:365` | `EnemyActor.cs:321` |
| Dive height 0.75 | `enemyActor.ts:388` | `EnemyActor.cs:275` |

Species values that are not listed below match. Original values are from `babylon/src/enemies/enemyActor.ts`;
Unity values are from `Editor/DemoContentBuilder.cs`.

| # | Species / value | Original (line) | Unity (line) |
| --- | --- | --- | --- |
| R1 | rat maxHp / reach / windup / cooldown | 24 / 0.85 / 0.34 / 0.8 (`:69,73-74`) | 26 / 0.95 / 0.36 / 0.9 (`:244`) |
| R2 | frog maxHp / reach / cooldown | 46 / 1.0 / 1.1 (`:84,88-89`) | 52 / 1.1 / 1.2 (`:246`) |
| R3 | spider reach / cooldown | 0.95 / 0.9 (`:102-103`) | 1.05 / 1.0 (`:245`) |
| R4 | snake maxHp / reach / windup | 42 / 1.3 / 0.66 (`:113,117-118`) | 46 / 1.35 / 0.62 (`:247`) |
| R5 | wasp maxHp / reach | 24 / 0.95, melee dive (`:128,131`) | 26 / 5.0, ranged (`:251`; owner 2026-10-11, BIBLE §6) |
| R6 | bat reach / windup / cooldown / hover | 0.9 / 0.28 / 0.85 / 1.7 (`:144-146`) | 0.95 / 0.30 / 0.9 / 1.6 (`:252`) |
| R7 | slime maxHp / speed / reach / windup | 56 / 1.0 / 1.05 / 0.75 (`:155,158-159`) | 64 / 1.1 / 1.15 / 0.72 (`:248`) |
| R8 | Initial cooldown | `rand × 0.6` (`:224`) | `0.6 + rand×0.8 + i×0.25` (`Runtime/Battle/BattleDirector.cs:145`) |
| R9 | Telegraph glow peak | (0.52, 0.09, 0.02), kept under bloom, and also lit during the strike (`:514-523`) | (1.0, 0.17, 0.02), windup only (`Runtime/Enemies/EnemyActor.cs:347-351`) |
| R10 | Lunge stop | when ≤ 0.05 m from the anchor (`:364`) | when ≤ reach×0.5 (`EnemyActor.cs:320`) |
| R11 | Hover follow rate | 9 diving, 3 otherwise (`:389`) | 8 always (`EnemyActor.cs:278`) |
| R12 | Waiting behaviour | prowl at reach×0.92, speed×0.55 (`:420-424`) | ring 3.6-5.4 m from `EnemyMind` (`BattleConfig.cs:40-41`). Replaced by owner, `bbcd6d8`. |
| R13 | Hurt-flash blink | 15 Hz (`floor(t×30)%2`, `:512`) | 30 Hz (`Repeat(t×30,1)<0.5`, `EnemyActor.cs:345`) |

### 3.3 Camera rules {#U.EVE-RECON-§3.3}
These values match:

| Rule | Original | Unity |
| --- | --- | --- |
| Movement-basis grace 0.16 s | `babylon/src/camera/movementBasis.ts:19` | `Core/Camera/MovementBasis.cs:13` |
| Turn rate 0.9π rad/s | `movementBasis.ts:20` | `MovementBasis.cs:14` |
| Neutral stick adopts the new basis | `movementBasis.ts:55-61` | `MovementBasis.cs:24-30` |
| CCTV servo 2.2 | `three/src/camera/cameraManager.ts:79` | `Runtime/Camera/CameraShot.cs:161` |
| Shake: trauma²×0.14, decay 1.6, phase ×40, same sine set | `cameraManager.ts:61-73` | `CameraDirector.cs:119-128` |
| Hold the shot in gaps | `cameraManager.ts:56` | `CameraZoneSelector.cs:44` |

These do not:

| # | Value | Original (file:line) | Unity (file:line) |
| --- | --- | --- | --- |
| C1 | Hysteresis model | Inflate the active polygon by `hysteresisMargin` 0.5; a new zone's core must contain Kat (`three/src/camera/cameraZone.ts:51`, `cameraManager.ts:43-49`) | Overlapping box triggers; highest priority, then most recently entered, wins (`Core/Camera/CameraZoneSelector.cs:42-52`). Overlap 1 m (`Editor/DemoContentBuilder.Scene.cs:450`). Same band width, different rule: Unity cuts on *entering* the overlap. |
| C2 | Basis source | Hand-authored zone `forward/right` (`cameraZone.ts:41-49`; Babylon `forwardYaw`, `babylon/src/camera/cameraZone.ts:54`) | Live camera `transform.forward` yaw (`Runtime/Camera/CameraDirector.cs:34-41`, `Runtime/Player/PlayerController.cs:124`). Bug B2. |
| C3 | Retarget trigger | Grace restarts only when the *zone* changes (`movementBasis.ts:50-53`) | Grace restarts on any yaw change > 1e-4 rad (`MovementBasis.cs:32-36`) |
| C4 | Default FOV | 55 (`cameraZone.ts:50`) | 50 (`CameraShot.cs:154`); every shot is authored explicitly at 50-70 |
| C5 | CCTV servo curve | `lerp(min(1, dt×2.2))` on the look-at point (`cameraManager.ts:79-81`) | `1−exp(−2.2·dt)` slerp on rotation, yaw and pitch clamped (`CameraShot.cs:195-196,213-222`) |
| C6 | Cut | Hard cut (Three, `cameraManager.ts:86-96`); 0.28 s blend, 0.42 pull, 0.55 lead, 0.012 drift (Babylon, `fixedCamera.ts:18-20`, `cameraZone.ts:68-69`) | Hard cut, no pull, lead or drift (`CameraDirector.cs:99-103`), per BIBLE §2 |
| C7 | Shake while paused | Decays on `dt` | Frozen while `timeScale == 0` (`CameraDirector.cs:119`) |
| C8 | Fisheye | Per-zone barrel distortion (`cameraZone.ts:21`) | none |

### 3.4 Outfit painter {#U.EVE-RECON-§3.4}
Original: `babylon/src/player/outfit.ts`. Unity: `Editor/OutfitPainter.cs`.

**Values that match:**
- Texture size 1024 (`:23` / `:17`).
- All five regions: boots, cuff, jeans, belt and tank, with tank `maxSpread 0.19` (`:38-46` / `:36-43`).
- Every band, colour and grain.
- Skin (176, 132, 104) (`:48` / `:45`).
- Seed 90210 and the LCG constants 1103515245 / 12345.
- Height and spread classification (`:86-98` / `:69-78`).
- UV orientation: the canvas `1−v` and Unity's bottom-up rows are equivalent.

| # | Value | Original (line) | Unity (line) |
| --- | --- | --- | --- |
| O1 | RNG output | `seed = (seed×a+c) & 0x7fffffff`; `rand = seed/0x7fffffff` (`:76-78`) | `uint` wraps mod 2^32; `rand = (seed>>8)/2^24` (`:81-82`). The jitter sequence differs, so the texture is not bit-identical. |
| O2 | Seam closing | Fill plus a 2 px stroke in the same colour (`:114-117`) | Barycentric fill with ε = −0.08 of the triangle and a +1 px bbox (`:100-120`). Overreach scales with triangle size, not pixels. |
| O3 | Material after painting | PBR: bump removed, metallic 0, roughness 0.78 (`:121-127`) | Re-skinned onto the toon shader. Not applicable. |
| O4 | `minSpread` field | Supported (`:31,97`) | Absent (unused by the original's table, so no visible change) |

## 4. Bugs found {#U.EVE-RECON-§4}
These are defects against what the original or the BIBLE intends. Features the owner deliberately dropped are not listed.

- **B1: Stagger never decays.** *Fixed by U.EVE-T34: `Core/Battle/StaggerRule.cs` `StaggerMeter` drains 14/s, skips a creature already stunned and clears when the stun ends.*
  - `EnemyActor._recentDamage` only grows, and is cleared only when a stagger fires (`Runtime/Enemies/EnemyActor.cs:39,164-169`).
  - The original decays it at 14/s and clears it when the stun ends (`babylon/src/enemies/enemyActor.ts:481,486`).
  - `StaggerRule` and `BattleConfig` both describe "burst damage in a short window" (`Core/Battle/StaggerRule.cs:3-4`, `BattleConfig.cs:136`).
  - As written, chip damage spread across a whole fight still staggers a creature once its total reaches
    max(20, 0.35×maxHp), however slowly it arrived.
    The boss (Mito Mother) loses a swing to a 1.8 s stun every 147 damage (0.35×420).
- **B2: The movement basis never realigns under CCTV and Rail shots.**
  - `PlayerController` feeds `MovementBasis` the camera's live yaw (`PlayerController.cs:124`).
  - CCTV servos and rails change that yaw every frame while Kat moves, so each frame restarts the 0.16 s grace (`MovementBasis.cs:32-36`).
  - Result: while the stick is held, "up" stays latched to the yaw from the moment of the cut, Three's hard
    latch, instead of rotating to the new shot after the grace as README and §2.8 promise.
  - It snaps when the stick is released.
  - It also inherits pitch-degenerate yaw on any steep shot. The original avoided this with an authored basis.
- **Likely unintended (presentation):** R9. The telegraph glow runs about twice the original's brightness. The
  original kept it under the bloom threshold on purpose, "two lanterns would wash out the street"
  (`babylon/src/enemies/enemyActor.ts:520-523`). Unity also drops the glow during the strike beat.

## 5. Verdict on the reuse claim {#U.EVE-RECON-§5}
- **Enemy rhythm:** reused faithfully in structure and in every shared constant. Species tunings drift by
  5-15% (R1-R7), and the waiting behaviour was replaced by owner direction.
- **Camera rules:** the movement-basis constants and the CCTV and shake numbers are reused. The hysteresis
  mechanism is different, and the basis source differs (B2).
- **Outfit painter:** a faithful port, apart from its random number generator.
- **Battle numbers:** mostly *not* ExperimentEve's. The ATB formula, gun multipliers, damage variance, crits,
  defence and PE are Parasite Eve 1998's, by the owner's 2026-10-10 direction. Only these are ExperimentEve's:
  the strike roll, the stagger threshold, the dodge timings, the run speed and base HP.
- **Suggested README fix:** say it reuses "enemy rhythm, camera rules, the outfit painter and its dodge,
  stagger and strike numbers".
