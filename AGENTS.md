# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md, then `docs/BIBLE.md`. Common prompt
commands are implemented by the shared runner; do not add a second copy under a provider-specific
command folder.

## What this is

Unity Eve is a Unity 6 proof of concept for a cel-shaded, inked (Borderlands / Parasite Mutant) survival-horror RPG with Parasite Eve's
ATB battle, built from free assets. Read `docs/BIBLE.md` for what it is, what it is not, and its laws.

## Laws (do not break)

1. **Core is pure C#.** `Assets/UnityEve/Core` is `noEngineReferences: true` and must compile under
   plain `dotnet test` (`Tests/Core.Tests`). Randomness goes only through the seeded `Core/Util/Rng`.
2. **Presentation never decides a rule.** MonoBehaviours call Core and present the result.
3. **One owner of time.** Only `WorldTime` writes `Time.timeScale`; pause and slow motion are requests.
4. **Content is generated.** Data assets, prefabs, animator controllers, audio and `Demo.unity` come from
   `DemoContentBuilder`. Change the builder, not the generated asset, then re-run it.
5. **Done is verified by play.** `./tools/test.ps1 -Compile` green, the content build, the player
   build, then `Unity.Eve.exe -batchmode -demo` exits 0. Look at the screenshots in
   `artifacts/demo/` (or the `-artifacts DIR` you pass). Batch mode needs the Editor closed: ask the owner to quit it, never kill it.
6. **Every event has a sound** (`SoundId`). The content build warns about silent ids.
7. **Free assets only** (CC0, OFL, or MIT with attribution), recorded in `CREDITS.md`. Never Mixamo or
   store assets. A new kit drops into `Art/Models/<kit-name>/` (glTF/`.glb` preferred — that's what
   `com.unity.cloud.gltfast` imports natively; `.fbx` also imports natively; raw `.obj` needs a converter
   this project doesn't have, so avoid obj-only sources) and is placed with the `Model(...)` helper in
   `DemoContentBuilder.Scene.cs`, which scales it to a target size, floor-centers it, runs
   `ConvertRenderers(...)` to re-skin every material onto the project's toon shader, and adds a collider —
   no per-kit code needed beyond a `$"{Art}/Models/<kit-name>/{n}.glb"` path helper alongside `Urban`/`Furn`/`Surv`.

## Map

| File | Role |
| --- | --- |
| `Core/Battle/AtbGauge.cs`, `BattleConfig.cs` | PE1 gauge formula; every tuning number, as named knobs whose shipped values are data in `Resources/UnityEve/battle-tuning.json` (validated whole and applied by `Runtime/Game/TuningLoader.cs` before the first scene; retune in the file and the matching initializer, a test keeps them equal so Core tests run on the shipped numbers) |
| `Core/Battle/FocusWindow.cs` | Perfect-dodge slow-motion window |
| `Core/Battle/EnemyAttackCycle.cs` | close → windup → strike → recover |
| `Runtime/Battle/BattleDirector*.cs` | Battle phases, menus, range dome targeting, actions |
| `Runtime/Camera/*` | Fixed / CCTV / rail shots, zones, hard cuts, shake |
| `Runtime/Interaction/*` | Doors, rummage containers, pickups, readables |
| `Runtime/UI/GameHud.cs` + `UI/Hud.uss` | Whole screen on UI Toolkit; owns the full-resolution world render target |
| `Shaders/Toon.shader`, `InkOutline.shader`, `HexDome.shader` | Cel shading + hatching, full-screen ink lines, hexagon range dome |
| `Core/Battle/EnemyMind.cs`, `BladeArts.cs` | Enemy tactics; katana Slash / Rush rules |
| `Core/Session/*` + `Runtime/Input/GameInput.cs` | Multiplayer-ready seam (BIBLE U.EVE-LAW-6): the device submits each seat's `IntentCommand` to `GameSession.Session` (`LocalSession`); game code reads `PlayerCharacter.Controls`, never the device |
| `Runtime/Game/Autopilot.cs` | `-demo [-artifacts DIR]` self-play verification (the family-wide flag): tours every system, screenshots each beat, quits PASS/FAIL |
| `Editor/DemoContentBuilder*.cs` | Generates all content and the level |
| `Editor/DemoContentBuilder.Scene.cs`'s `Model(...)`/`ConvertRenderers(...)` | The universal glTF/FBX import pipeline: any kit's model scales, floor-centers, re-skins to the toon shader, and gets a collider the same way |
| `Core/Story/LockGraph.cs` | Pure lock-and-key solvability check: every door/gate's key must be reachable without it, so content can never ship an unwinnable soft-lock |
