# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md, then `docs/BIBLE.md`. Common prompt
commands are implemented by the shared runner; do not add a second copy under a provider-specific
command folder.

## What this is

Unity Eve is a Unity 6 proof of concept for a PS1-style survival-horror RPG with Parasite Eve's
ATB battle, built from free assets. Read `docs/BIBLE.md` for what it is, what it is not, and its laws.

## Laws (do not break)

1. **Core is pure C#.** `Assets/UnityEve/Core` is `noEngineReferences: true` and must compile under
   plain `dotnet test` (`Tests/Core.Tests`). Randomness goes only through the seeded `Core/Util/Rng`.
2. **Presentation never decides a rule.** MonoBehaviours call Core and present the result.
3. **One owner of time.** Only `WorldTime` writes `Time.timeScale`; pause and slow motion are requests.
4. **Content is generated.** Data assets, prefabs, animator controllers, audio and `Demo.unity` come from
   `DemoContentBuilder`. Change the builder, not the generated asset, then re-run it.
5. **Done is verified by play.** `./tools/test.ps1 -Compile` green, the content build, the player
   build, then `UnityEve.exe -batchmode -autoplay` exits 0. Look at the screenshots in
   `artifacts/autoplay/`. Batch mode needs the Editor closed: ask the owner to quit it, never kill it.
6. **Every event has a sound** (`SoundId`). The content build warns about silent ids.
7. **Free assets only,** recorded in `CREDITS.md`. Never Mixamo or store assets.

## Map

| File | Role |
| --- | --- |
| `Core/Battle/AtbGauge.cs`, `BattleConfig.cs` | PE1 gauge formula; every tuning number |
| `Core/Battle/FocusWindow.cs` | Perfect-dodge slow-motion window |
| `Core/Battle/EnemyAttackCycle.cs` | close → windup → strike → recover |
| `Runtime/Battle/BattleDirector*.cs` | Battle phases, menus, range dome targeting, actions |
| `Runtime/Camera/*` | Fixed / CCTV / rail shots, zones, hard cuts, shake |
| `Runtime/Interaction/*` | Doors, rummage containers, pickups, readables |
| `Runtime/UI/GameHud.cs` + `UI/Hud.uss` | Whole screen on UI Toolkit, PS1 render target |
| `Runtime/Game/Autopilot.cs` | `-autoplay` self-play verification |
| `Editor/DemoContentBuilder*.cs` | Generates all content and the level |
