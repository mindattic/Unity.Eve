---
codex: 1
project: UnityEve
code: UE
layer: stories
status: living
updated: 2026-10-10
---

# Unity Eve: User stories

> **Status:** ✅ verified (a named test or autoplay step shows it) · 🟡 partial · ⬜ planned.
> **Evidence:** `TestName` = xUnit in `Tests/Core.Tests`; `autoplay:NN_name` = screenshot step of
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
| US-004 | Rate of fire splits damage per PE1's table; Burst hits every target in range | ✅ | `Rate_of_fire_trades_per_round_power_for_total_power`, `Burst_weapon_hits_every_target_in_range` |
| US-005 | Rounds per turn are limited by the clip; Reload takes a turn | ✅ | `Rounds_are_limited_by_ammo_in_the_clip` |
| US-006 | PE abilities (Heal, Energy Shot, Haste, Slow, Barrier); each cast slows PE regen | ✅ | `Each_pe_cast_slows_pe_regeneration`, `Pe_cannot_be_overspent`, `Haste_doubles_atb_fill` |
| US-007 | Grenade thrown on an arc damages everything in a radius | ✅ | autoplay:`encounter_1_street_grenade` |
| US-008 | Enemies close, telegraph (glow), strike where you were, recover; at most two commit at once | ✅ | `Enemy_runs_close_windup_strike_recover_and_respects_commit_limit`, `Interrupt_cancels_a_windup` |
| US-009 | Strikes resolve as hit, whiff or evade (i-frames) | ✅ | `Strike_resolution` |
| US-010 | Escape chance favours stronger players, clamped | ✅ | `Escape_chance_is_clamped_and_favours_stronger_players` |
| US-011 | Victory grants EXP; levels give Bonus Points that raise Active Time | ✅ | `Level_up_grants_bonus_points_and_bp_raise_active_time`, autoplay:`encounter_1_street_victory` |
| US-012 | Encounters materialize in place and seal the area | ✅ | autoplay:`encounter_1_street_encounter` |

## Focus (perfect dodge)

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-020 | A dodge started ≤0.3 s before a strike opens Focus: time ×0.15 | ✅ | `Perfect_dodge_window_opens_focus_and_slows_time`, autoplay:`encounter_1_street_focus` |
| US-021 | Focus offers one free Quick Heal / Target Vitals / Counter, no ATB cost | ✅ | autoplay:`encounter_1_street_vitals` |
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
| US-038 | The shed door stays locked until the key from the Mercer side table is in the bag | 🟡 | Built (`Door` + `key_shed`); autoplay does not exercise the lock yet |
| US-035 | Hold Interact to rummage; each container rolls its seeded loot table once | ✅ | `Loot_table_is_deterministic_and_can_roll_nothing`, autoplay:`rummage_street`, `search_container_house_fridge` |
| US-036 | The bag has limited slots; stacks spill over and overflow is reported | ✅ | `Inventory_stacks_then_spills_into_new_slots`, `Inventory_reports_overflow_when_full` |
| US-037 | Field menu: items, equip, controls | ✅ | autoplay:`field_menu` |

## Presentation and platform

| ID | Story | Status | Evidence |
| --- | --- | --- | --- |
| US-040 | 400x300 point-filtered render, letterboxed to 4:3 | ✅ | every autoplay screenshot |
| US-041 | Kat wears a painted outfit on the CC0 base body | ✅ | autoplay:`house_living` |
| US-042 | Gamepad-first input with keyboard fallback and rumble | 🟡 | Bindings in `GameInput`; needs a physical-pad playtest |
| US-043 | Every event has a sound | ✅ | The content build warns on silent `SoundId`s (none) |
| US-044 | PS1 vertex snap and affine texture warp | ⬜ | |
| US-045 | Save and load | ⬜ | |

## Tally

| Section | ✅ | 🟡 | ⬜ |
| --- | --- | --- | --- |
| Battle core | 12 | 0 | 0 |
| Focus | 3 | 1 | 0 |
| Exploration | 8 | 1 | 0 |
| Presentation | 4 | 1 | 2 |
