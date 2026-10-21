---
codex: 1
project: UnityEve
code: U.EVE
layer: stories
status: living
updated: 2026-10-11
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Unity Eve: User stories

> **Status:** ✅ verified (a named test or autoplay step shows it) · 🟡 partial · ⬜ planned.
> **Evidence:** `TestName` = NUnit in `Tests/Core.Tests`; `autoplay:NN_name` = screenshot step of
> `UnityEve.exe -batchmode -autoplay`.

**Definition of done (every story):**
1. `tools/test.ps1 -Compile` is green.
2. Each rule has a named test.
3. Anything visible has an autoplay step.
4. It is committed once.

## Battle core

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-001 | The ATB fills in real time at PE1's rate `(50+ActiveTime)×gun multiplier` | ✅ | `Atb_fills_in_base_time_at_base_rate`, `Atb_rate_follows_pe1_formula_and_floor` |
| US-002 | Opening the command menu stops the world; the gauge doesn't fill while paused | ✅ | `Atb_does_not_fill_while_world_is_paused`, autoplay:`encounter_1_street_command_menu` |
| US-003 | Attack raises a range dome; only enemies inside can be targeted | ✅ | autoplay:`encounter_1_street_range_dome` |
| US-013 | The range dome is a faint wireframe of hexagons (constant 1-2 px lines, no fill), faintly visible through walls | ✅ | autoplay:`encounter_1_street_range_dome`, `encounter_2_yard_range_dome`, `encounter_3_kitchen_range_dome` |
| US-014 | Creatures take turns through attack tokens: one engages (two in packs of 4+), with a breather between turns; the rest lurk, circle, power up or back off on a ring around Kat | ✅ | `Attack_tokens_let_one_creature_engage_at_a_time_with_a_breather_between_turns`, `Without_a_token_a_creature_never_engages_it_lurks_circles_buffs_or_backs_off`, `A_waiting_creature_that_crowds_the_player_backs_off_to_its_ring`, `Temperament_shapes_choices_aggressive_seize_turns_cautious_hang_back`, `Mind_replays_identically_from_its_seed`, autoplay log `enemy intents seen` |
| US-019 | The engaged creature stalks or flanks in, rears up (growl, red glow, crouch), charges at a burst of speed, strikes, then retreats and hands the turn on; a charge that never connects gives up | ✅ | `Token_holder_stalks_in_rears_up_charges_strikes_then_retreats_and_hands_the_token_back`, `A_charge_that_never_connects_gives_up_the_turn`, `Flanker_works_round_then_charges_from_the_blind_side`, autoplay:`encounter_*_enemy_rears_up`, `encounter_*_enemy_charges` |
| US-017 | Kat carries a katana with her gun; Blade > Slash cuts the target and anything beside it in a 110° arc | ✅ | `Slash_catches_the_front_arc_within_reach_and_nothing_behind`, autoplay:`encounter_3_kitchen_slash`, `encounter_3_kitchen_blade_menu` |
| US-018 | Blade > Rush (PE) dashes through a target up to 8 m away, cutting everything on the line, untouchable mid-dash | ✅ | `Rush_ends_past_the_target_by_the_overshoot`, `Rush_cuts_everything_along_the_line_but_not_off_to_the_side`, `Rush_trades_per_hit_power_for_reach_and_level_raises_both`, autoplay:`encounter_2_yard_rush` |
| US-016 | A creature can stand still and enrage itself (violet charge, sound, ENRAGED popup): harder hits and faster tempo for a while | ✅ | `Buff_cast_holds_still_then_raises_damage_and_tempo_until_it_wears_off`, autoplay:`encounter_*_enemy_buff` (when one occurs) |
| US-004 | Rate of fire splits damage per PE1's table; Burst hits every target in range | ✅ | `Rate_of_fire_trades_per_round_power_for_total_power`, `Burst_weapon_hits_every_target_in_range` |
| US-005 | Rounds per turn are limited by the clip; Reload takes a turn | ✅ | `Rounds_are_limited_by_ammo_in_the_clip` |
| US-006 | PE abilities (Heal, Energy Shot, Haste, Slow, Barrier); each cast slows PE regen | ✅ | `Each_pe_cast_slows_pe_regeneration`, `Pe_cannot_be_overspent`, `Haste_doubles_atb_fill` |
| US-007 | Grenade thrown on an arc damages everything in a radius | ✅ | autoplay:`encounter_1_street_grenade` |
| US-008 | Enemies close, telegraph (glow), strike where you were, recover; at most two commit at once | ✅ | `Enemy_runs_close_windup_strike_recover_and_respects_commit_limit`, `Interrupt_cancels_a_windup` |
| US-009 | Strikes resolve as hit, whiff or evade (i-frames) | ✅ | `Strike_resolution` |
| US-010 | Escape chance favours stronger players, clamped | ✅ | `Escape_chance_is_clamped_and_favours_stronger_players` |
| US-011 | Victory grants EXP; levels give Bonus Points that raise Active Time | ✅ | `Level_up_grants_bonus_points_and_bp_raise_active_time`, autoplay:`encounter_1_street_victory` |
| US-012 | Encounters materialize in place and seal the area | ✅ | autoplay:`encounter_1_street_encounter` |
| US-050 | Returning to a screen after being somewhere not adjacent to it can start a random encounter from that screen's pack table; bouncing between neighbouring screens never does; cooldown after any battle | ✅ | `ScreenEncounterRuleTests` (6), autoplay:`reentry_encounter` (and the bounce check) |
| US-051 | Alien creatures built in-engine with procedural animation: Mito Crawler, Drain Tendril, Spore Sac, Husk Brute | ✅ | autoplay:`encounter_4_crossing_encounter`, `encounter_5_square_encounter`, `encounter_6_dock_encounter` |
| US-052 | The animal models are mutated (sickly tint, glowing pustules) so nothing reads as a plain animal | ✅ | autoplay:`encounter_1_street_encounter`, `encounter_2_yard_encounter` |

## Focus (perfect dodge)

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-020 | A dodge started ≤0.3 s before a strike opens Focus: time ×0.15 | ✅ | `Perfect_dodge_window_opens_focus_and_slows_time`, autoplay:`encounter_1_street_focus` |
| US-021 | Focus offers one free Quick Heal / Target Vitals / Riposte, no ATB cost | ✅ | autoplay:`encounter_1_street_vitals` |
| US-022 | Focus is optional: the window expires on real time; mistimed dodges still evade | ✅ | `Focus_window_expires_on_real_time_so_it_is_optional` |
| US-023 | A perfect dodge triggered by a human on a gamepad, felt in play | 🟡 | Autoplay opens Focus through `DebugOpenFocus`; needs an owner playtest |

## Exploration

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-030 | Fixed camera zones hard-cut; priority then recency; doorway gaps hold the shot | ✅ | `Camera_selector_prefers_priority_then_most_recent_and_holds_in_gaps`, `Camera_selector_counts_cuts_only_on_change` |
| US-031 | CCTV cameras pan to follow the player within limits | ✅ | autoplay:`05_alley_cctv`, `03_street_cctv`, `08_house_kitchen` |
| US-032 | A rail camera slides along the street | ✅ | autoplay:`04_street_rail` |
| US-033 | Stick direction holds across a cut until released | ✅ | `Movement_basis_holds_direction_across_a_cut_while_stick_held`, `Movement_basis_adopts_new_camera_instantly_on_neutral_stick` |
| US-034 | Doors fade and move between the street and the house | ✅ | autoplay:`front_door`, `interior` |
| US-039 | The Market district east of the street: crossing, square with fountain, stalls and church, back lane, loading dock (five screens, three encounters) | ✅ | autoplay:`11_crossing` … `15_dock`, `encounter_4..6_*` |
| US-038 | The shed door stays locked until the key from the Mercer side table is in the bag | ✅ | autoplay:`shed_locked`, `search_container_house_sidetable`, `shed_interior`, `shed_shotgun` |
| US-035 | Hold Interact to rummage; each container rolls its seeded loot table once per stage, up to 3 stages | ✅ | `Loot_table_is_deterministic_and_can_roll_nothing`, autoplay:`rummage_street`, `search_container_house_fridge` |
| US-036 | The bag has limited slots; stacks spill over and overflow is reported | ✅ | `Inventory_stacks_then_spills_into_new_slots`, `Inventory_reports_overflow_when_full` |
| US-037 | Field menu: items, equip, controls | ✅ | autoplay:`field_menu` |

## Presentation and platform

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-040 | Full-resolution 16:9 render with 4x MSAA (the 400x300 PS1 target is retired, owner 2026-10-11) | ✅ | every autoplay screenshot (1600x900) |
| US-041 | Kat wears a painted outfit on the CC0 base body | ✅ | autoplay:`house_living` |
| US-046 | Cel-shaded look: banded light pools and a 1 px ink outline on every opaque surface | ✅ | every autoplay screenshot, e.g. `07_house_living`, `encounter_3_kitchen_encounter` |
| US-048 | Clean ink lines: full-screen silhouettes (1/depth curvature, so floors never streak) and creases, even anti-aliased width, fading into fog; tiled textures flattened toward painted colour | ✅ | every autoplay screenshot, e.g. `03_02_mercer_porch`, `48_shed_interior` |
| US-049 | Comic HUD: Bangers lettering, ink-bordered panels | ✅ | autoplay:`encounter_1_street_command_menu`, `field_menu` |
| US-047 | Walking out of combat plays the walk/jog cycle | ✅ | autoplay:`explore_walk` (fails if no walk/jog clip is playing) |
| US-042 | Gamepad-first input with keyboard fallback and rumble | 🟡 | Bindings in `GameInput`; needs a physical-pad playtest |
| US-043 | Every event has a sound | ✅ | The content build warns on silent `SoundId`s (none) |
| US-045 | Save at payphones; Continue reloads the scene and restores level, HP/PE, bag, weapons, story facts, searched containers, taken items, unlocked doors and cleared fights, resuming at the payphone | ✅ | `Restore_puts_progression_health_pe_and_the_bag_back_exactly`, `A_save_needs_the_current_version_a_resume_point_and_a_living_heroine`, autoplay:`saved`, `loaded` (six restore checks) |

## Vertical slice

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-060 | Title screen: New Game, Continue (last save, with its summary), Quit | ✅ | autoplay:`title` |
| US-061 | The current objective is always shown; each new one is announced; facts can arrive out of order | ✅ | `Objectives_advance_as_facts_are_learned_and_announce_each_change`, `Objectives_can_be_completed_out_of_order_and_the_tracker_skips_ahead`, autoplay:`church_barred` |
| US-062 | The story: Mercer house -> shed shotgun -> barred church -> grocery register (storeroom key) -> manager's desk (church key) -> the nave | ✅ | autoplay story log, `church_barred`, `grocery_inside`, `storeroom_unlocked`, `church_key`, `nave` |
| US-063 | Boss: the Mito Mother, rooted in the nave, long tentacle reach, calls her brood at 66% and 33% health | ✅ | `Boss_calls_each_reinforcement_wave_once_as_its_health_falls`, autoplay:`boss_encounter`, `boss_victory` |
| US-064 | Ending: the night's tally (time, creatures, saves, level), then back to the title | ✅ | `Completing_the_last_objective_ends_the_story_once`, autoplay:`ending` |
| US-065 | Dying offers Continue from the last save or a retry at the bus stop | ✅ | autoplay:`game_over` (falls in a re-entry fight, chooses Continue), `continued` (back at the grocery payphone) |
| US-066 | Music: exploration, battle and boss loops crossfade with the fights | ✅ | autoplay checks the playing loop: battle in a fight, explore after Continue, boss against the Mother (by ear: owner) |
| US-067 | Ashgrove Heights: Willow Lane's seeded houses, cars, signs, benches facing the street; Kingsport Foods and its parking lot | ✅ | autoplay:`16_willow_junction` ... `22_storefront` |
| US-068 | Kingsport Foods interior: checkout, aisles, produce, freezers, locked staff storeroom, two fights | ✅ | autoplay:`23_grocery_checkout` ... `26_storeroom`, `encounter_9_grocery_*`, `encounter_10_storeroom_*` |

## Tally

| Section | ✅ | 🟡 | ⬜ |
| --- | --- | --- | --- |
| Battle core | 21 | 0 | 0 |
| Focus | 3 | 1 | 0 |
| Exploration | 10 | 0 | 0 |
| Presentation | 9 | 1 | 0 |
| Vertical slice | 9 | 0 | 0 |

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| U.EVE-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| U.EVE-T2 | 2026-10-12 | chore | closed | Add deploy.ps1 to rebuild and publish a fresh standalone copy to C:\Apps\UnityEve | tools/deploy.ps1 |
| U.EVE-T3 | 2026-10-14 | chore | open | NUnit tests for Core Rng (Assets/UnityEve/Core/Util/Rng.cs): same seed replays the same NextUInt sequence, different seeds diverge, seed 0 still yields a usable non-stuck stream, NextFloat stays in [0,1), Range(float) stays in [min,max), Range(int) stays in [min,maxExclusive) and returns min when maxExclusive<=min, Chance(0) never and Chance(1) always fires; every battle replay depends on this generator but it is only exercised indirectly today (note Tests/Core.Tests currently uses xUnit) | — |
| U.EVE-T4 | 2026-10-14 | chore | open | NUnit tests for DamageCalculator edge cases (Assets/UnityEve/Core/Battle/DamageCalculator.cs): RoundShare full PE1 table for 0/1..7 rounds and the 1.3/n fallback for 8+, Roll subtracts Defense*DefensePerPoint and floors at 1, canCrit false never crits and a crit applies CriticalMultiplier, variance stays within 0.9..1.1; PlanRounds returns an empty plan for zero targets or zero ammo, melee ignores ammo, burst with more targets than rounds wraps from the chosen index modulo targetsInRange; these numbers drive every hit but only the happy paths are covered | — |
| U.EVE-T5 | 2026-10-14 | chore | open | NUnit tests for Health, Progression and Inventory edge cases (Core/Battle/Health.cs, Core/Battle/Progression.cs, Core/Items/Inventory.cs): Health.SetMax refill vs clamp-down, Max never below 1, dead units cannot be healed, Revive refills, Restore clamps to 1..Max; Progression.AddExp gaining several levels in one call, ExpForLevel curve values, TrySpendOnActiveTime rejecting 0/negative/over-budget, Restore clamping negatives, MaxHpForLevel/MaxPeForLevel growth; Inventory.TryRemove failing without mutating when short, draining from the last slot first and dropping empty slots, SetCapacity never shrinking below used slots, Add ignoring count<=0 and treating maxStack<=0 as 1, Changed firing on Add/TryRemove/Clear; these feed save restore and level-up so silent clamping bugs corrupt saves | — |
| U.EVE-T6 | 2026-10-14 | chore | open | NUnit tests for AttackTokens in isolation (Assets/UnityEve/Core/Battle/AttackTokens.cs): capacity below 1 clamps to 1, CapacityFor boundary at pack size 3 vs 4, firstDelay blocks TryTake until enough scaled time is ticked, negative/zero Tick is ignored, TryTake is idempotent for an existing holder, Release of a non-holder does not start a breather, a real Release blocks the next taker for a seeded breather within TokenBreatherMin..MaxSeconds; today it is only covered through EnemyMind scenarios, so a regression here would surface as vague mob behaviour | — |
| U.EVE-T7 | 2026-10-14 | chore | open | NUnit EditMode tests for PlayerCharacter weapon, ammo and damage rules (Assets/UnityEve/Runtime/Player/PlayerCharacter.cs; needs a new Unity Test Framework EditMode asmdef referencing UnityEve.Runtime, none exists yet): Equip keeps each gun its own magazine so swapping is never a free reload, melee weapons report 0 rounds and SpendRounds is a no-op for them, SetRounds clamps to 0..ClipSize, CanReload/Reload require ammo in the bag and consume exactly one ammo item, AddWeapon ignores null and duplicates, TakeDamage under Barrier halves damage with a floor of 1 and raises Died at 0 HP, GainExp auto-spends bonus points and refills HP/PE on level-up | — |
| U.EVE-T8 | 2026-10-14 | chore | open | NUnit EditMode tests for WorldTime and MenuModel (Assets/UnityEve/Runtime/Game/WorldTime.cs, Assets/UnityEve/Runtime/UI/MenuModel.cs): any pause key forces Time.timeScale 0 until every key resumes, the slowest SetScale request wins, scale>=1 removes the key, negative scales clamp to 0, ClearAll restores 1; MenuModel.Move wraps in both directions including deltas larger than the entry count, Move/Select on empty or out-of-range is a no-op, Confirm on a disabled entry returns false without invoking OnSelect, Version bumps on Add/Move/Select; WorldTime is the single owner of timeScale so overlapping pause/bullet-time bugs freeze or speed up the game | — |
| U.EVE-T9 | 2026-10-14 | chore | open | NUnit EditMode tests for SaveSystem file handling (Assets/UnityEve/Runtime/Game/SaveSystem.cs) using PathOverride to a temp folder: Read returns null for a missing file, unparseable JSON and a JsonUtility file that fails SaveGame.IsValid (wrong version, nulled lists, Hp 0); a SaveGame with bag, weapons, facts and world lists survives a JsonUtility write/read round trip intact; Exists mirrors Read; the .tmp swap leaves the previous save intact if writing fails; Pending is cleared after ApplyPending; a broken save path silently loses a whole night of progress | — |
| U.EVE-T10 | 2026-10-14 | chore | open | NUnit EditMode/PlayMode tests for Cover.Exposure (Assets/UnityEve/Runtime/Battle/CoverProp.cs): returns 0 with no CoverProp registered or attacker on top of the defender, 1 when a CoverProp box fully blocks the 9-point silhouette, a partial value for a low prop that only hides the lower rows, and ignores colliders that are not CoverProps; this fraction scales incoming damage by CoverMaxReduction in BattleDirector.OnEnemyStrike so a wrong value makes cover useless or invulnerable | — |
| U.EVE-T11 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | M.FLOW-T14 |
| U.EVE-T12 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 15 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| U.EVE-T13 | 2026-10-16 | chore | closed | Core tests use NUnit instead of xUnit (the MindAttic global test rule): Tests/Core.Tests packages and every test converted, same test count | 2c44915 / Tests/Core.Tests |
| U.EVE-T14 | 2026-10-16 | chore | open | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | user request 2026-10-16: approved repo sweep |
| U.EVE-T15 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| U.EVE-T16 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| U.EVE-T17 | 2026-10-16 | docs | open | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | user request 2026-10-16: approved repo sweep |
| U.EVE-T18 | 2026-10-19 | chore | open | Convert tracked source files to LF line endings via .gitattributes, per HOUSE-LAW-13 (low priority) | — |
| U.EVE-T19 | 2026-10-20 | docs | open | Publish UnityEve docs to GitHub Pages via the shared Toolkit DocFX pipeline: run adopt-docs-site.ps1 (README.md as home page, docs/*.md as articles, API reference only where the code builds in CI), enable Pages with build_type=workflow, set the repo homepage URL, retire tools/build-readme.ps1 and README.htm, confirm the docs Actions run is green and https://mindattic.github.io/UnityEve/ returns 200. Depends on MAT-T8. | — |
