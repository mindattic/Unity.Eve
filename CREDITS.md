# Credits and asset sources

Every third-party asset in this repo may be redistributed in source form. Nothing is from Mixamo,
since Adobe's terms forbid redistributing its raw files.

| Asset | Source | License | Where |
| --- | --- | --- | --- |
| Universal Base Characters (female body, long hair) | Quaternius, https://quaternius.com | CC0 1.0 | `Assets/UnityEve/Art/Models/kat/` |
| Universal Animation Library (43 clips, same 65-joint rig) | Quaternius | CC0 1.0 | `Assets/UnityEve/Art/Models/kat/UAL1_Standard.glb` |
| LowPoly Animated Easy Enemies / Monsters (rat, frog, spider, snake, wasp, bat, slime) | Quaternius | CC0 1.0 | `Assets/UnityEve/Art/Models/creatures/` (GLB exports made by ExperimentEve) |
| Retro Urban Kit (lamps, dumpsters, trucks, barriers, textures) | Kenney, https://kenney.nl/assets/retro-urban-kit | CC0 1.0 | `Assets/UnityEve/Art/Models/retro-urban/` |
| Furniture Kit (house interiors) | Kenney, https://kenney.nl/assets/furniture-kit | CC0 1.0 | `Assets/UnityEve/Art/Models/furniture/` |
| Survival Kit (crates, barrels, chest, workbench) | Kenney, https://kenney.nl/assets/survival-kit | CC0 1.0 | `Assets/UnityEve/Art/Models/survival/` |
| Car Kit (sedan, SUV, taxi, police, ambulance, van, delivery, firetruck) | Kenney, https://kenney.nl/assets/car-kit | CC0 1.0 | `Assets/UnityEve/Art/Models/car-kit/` |
| Impact Sounds, Interface Sounds | Kenney | CC0 1.0 | `Assets/UnityEve/Art/Audio/` |
| Bangers font | The Bangers Project Authors (Vernon Adams), Google Fonts | SIL OFL 1.1 | `Assets/UnityEve/Art/Fonts/` |

Generated in this repo, so original work with no third-party license:

- **Synthesized sounds.** Gunshots, the shotgun, explosions, the PE cast, the ATB-ready chime, encounter and victory stings, the enemy telegraph growl and the perfect-dodge whoosh all come from `Assets/UnityEve/Editor/WavSynth.cs`.
- **Kat's outfit texture,** painted by `Assets/UnityEve/Editor/OutfitPainter.cs`, a port of ExperimentEve's `outfit.ts`.
- **The level,** built from primitives and the kits above by `DemoContentBuilder`.
