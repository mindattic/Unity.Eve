---
layer: release
status: living
---
# Unity.Eve — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
A Unity 6 proof of concept of the Eve survival-horror RPG: a cel-shaded vertical slice of one night in Kingsport
with Parasite Eve's Active Time Battle and a perfect-dodge Focus mechanic, built from free assets.

## 2. The milestone: none — superseded by Unreal.Eve {#milestone}
- **Milestone:** none. This repo is finished as a reference prototype.
- **Published means:** not shipped further. The last player built here is the one already deployed; no packages
  go to `R:\MindAttic\Apps\Unity.Eve`.
- **The natural stopping point:** it already reached it. Its rules core was ported number for number into
  [Unreal.Eve](../../Unreal.Eve/docs/RELEASE.md) (EveCore, UE-T3), whose BIBLE names Unity.Eve "reference only".
  All Eve work continues there, toward Unreal.Eve MS-30 "Night One, real art".

## 3. Done when (acceptance checks)
Done. Nothing more is built here.

## 4. Epics, in priority order
None. The repo holds no open tickets.

## 5. Out of scope (everything)
- **All new work on Unity.Eve** — file it against Unreal.Eve instead, if it still applies there.
- **Deploying to R:** (was U.EVE-T58/T59) — an archived prototype is not repackaged.
- **Fixing its Core tests** (was U.EVE-T50) — the Unreal port already asserts windups instead (UE-T48).
- **Recommended:** archive the GitHub repo (owner decision; not done by agents).

## 6. Current verified state
- The vertical slice plays: title, Ashgrove Street, Market Square, Willow Lane and Kingsport Foods, Saint Ansel's
  and the Mito Mother, the ending tally, plus Ashgrove Park and School (README).
- Battle and field state run on a Core authority at a fixed tick and replay from a seed and command log (e9de510).
- The last code change was the bughunt in 8d712ac and 83cd4a8 (2026-11-02); later commits are tooling only.
- `docs/RECONSTRUCTION.md` maps the original ExperimentEve system by system to what was ported here.

## 7. Decision record
- 2026-11-27 — Unreal.Eve chosen as the canonical Eve (UE-T79): its BIBLE calls Unity.Eve reference only, it ports
  Unity.Eve's rules, and all recent game work is there. Legion vote on the joint charter and wont-do list: Approve,
  7 of 7 answering voters, no dissent.
- Triage: 5 open → done 0, duplicate 0, wont-do 5 (U.EVE-T45, T50, T55, T58, T59), kept 0. None was still valid to
  re-file in Unreal.Eve.
