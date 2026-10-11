---
codex: 1
project: UnityEve
code: UE
layer: stories
status: living
updated: 2026-10-11
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
| US-039 | The Market district east of the street: crossing, square with fountain, stalls and church, back lane, loading dock (five screens, three encounters) | ✅ | autoplay:`11_crossing` … `15_dock`, `encounter_4..6_*` |
| US-038 | The shed door stays locked until the key from the Mercer side table is in the bag | ✅ | autoplay:`shed_locked`, `search_container_house_sidetable`, `shed_interior`, `shed_shotgun` |
| US-035 | Hold Interact to rummage; each container rolls its seeded loot table once | ✅ | `Loot_table_is_deterministic_and_can_roll_nothing`, autoplay:`rummage_street`, `search_container_house_fridge` |
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
| US-045 | Save and load | ⬜ | |

## Tally

| Section | ✅ | 🟡 | ⬜ |
| --- | --- | --- | --- |
| Battle core | 21 | 0 | 0 |
| Focus | 3 | 1 | 0 |
| Exploration | 10 | 0 | 0 |
| Presentation | 8 | 1 | 1 |
