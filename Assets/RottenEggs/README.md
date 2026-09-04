# Rotten Eggs — Unity port

Ported from `Project/gamedev_project2_sample` (Java 17 / Swing / Java2D) into this
Unity 6 project. The game rules, artwork, audio and pixel rendering all came
across; nothing outside this folder and `Assets/StreamingAssets/RottenEggs` was
touched.

## Run it

1. Open the project and let Unity import the new files.
2. Menu bar → **Rotten Eggs → Add Game To Scene** (or drag the
   `Rotten Eggs Game` component onto any empty GameObject yourself).
3. Press **Play**. A 16:9 Game view looks best; the view only ever scales in
   whole-number steps, so the pixels stay sharp at any size.

Menu bar → **Rotten Eggs → Verify Ported Rules And Assets** runs the ported rule
checks and the asset checks and logs the results.

## Controls

Unchanged from the Java build.

| | |
|---|---|
| Menu | `W`/`S` or `↑`/`↓` select · `Enter` start · `1`/`2` quick-start |
| Single | `A`/`D` or `←`/`→` move · `Space` throw · `R` restart · `Esc` menu |
| Duo | P1 `A`/`D` · P2 `←`/`→` · `R` restart · `Esc` menu |
| Audio | `M` mute · `-`/`+` volume |

## What maps to what

| Java | Unity | Notes |
|---|---|---|
| `GameModel.java` | `Scripts/GameModel.cs` | Rules, 1:1. No Unity dependency beyond `Color32`/`Rect`. |
| `GamePanel.java` (drawing) | `Scripts/GameRenderer.cs` | Every draw call ported one for one. |
| `GamePanel.java` (input, loop, audio hooks) | `Scripts/RottenEggsGame.cs` | The `MonoBehaviour` driver. |
| Java2D `Graphics2D` | `Scripts/PixelCanvas.cs` | Software rasteriser: rects, ovals, polygons, arcs, sprite blits, clipping. |
| `PixelFont.java` | `Scripts/PixelFont.cs` | Same 5x7 EggByte glyphs. |
| `ChickenSprites.java` (GIF decoding) | `Scripts/ChickenSprites.cs` | Slices the sheet strips the GIFs were made from — same 25 frames at 0.1s. |
| `AudioManager.java` (`javax.sound`) | `Scripts/AudioManager.cs` | Parses the WAVs directly, so no import settings to get wrong. |
| `GameModel.runSelfTest()` | `Scripts/GameModelSelfTest.cs` | The same 44 checks. |
| `RottenEggsGame.java` `main` | `Editor/RottenEggsSetup.cs` | The `--self-test` / scene-setup entry points. |

### Why a pixel canvas instead of GameObjects

The Java game draws its whole world into one 480x270 image and scales that up.
Keeping that model meant the artwork, layout and font could be ported exactly
rather than re-approximated with sprites and a Canvas — and the scene needs no
prefabs, animators or colliders. If you later want the idiomatic Unity version,
`GameModel.cs` is already presentation-free: point `SpriteRenderer`s at it and
delete `GameRenderer.cs` / `PixelCanvas.cs`.

## Assets

`Assets/StreamingAssets/RottenEggs/` holds the 5 chicken sheets and the 12 WAVs,
loaded at runtime. They live there rather than in a normal asset folder so Unity
never has to be told about filter modes, Read/Write flags or sprite slicing.

One consequence: this uses `System.IO`, so it works in the Editor and in
desktop builds but **not in WebGL**. If you need WebGL, move the files into a
normal `Assets/` folder and swap the two loaders over to `Resources.Load`.

## Verified

- The ported rules pass all **44** gameplay checks — the same count the Java
  build reports, run against Unity's own `Rect.Overlaps`.
- Menu, Single and Duo frames were rendered headlessly and compared with the
  Java build's `--render-preview` output, pixel for pixel.

Remaining differences, all deliberate or invisible:

- The menu reads **UNITY EDITION** instead of **JAVA EDITION**.
- Alpha-blended areas (HUD bar, menu panel) can differ by 1/255 per channel —
  Java2D and this rasteriser round blends differently.
- The 2px basket-handle arc lands a pixel over in places.
- Particle scatter differs because .NET's RNG is not Java's. The rules that
  drive particles are identical; only the random offsets differ.

## Not brought over

- `build.sh`, the `.jar`, and the `--self-test` / `--render-preview` CLI — Unity
  builds and the **Rotten Eggs** menu replace these.
- `tools/AudioAssetGenerator.java`, which generated the WAVs. The 12 WAVs it
  produced are here; regenerating or retuning them is still a Java-side job.
- The unused sheets (`ChickenFly`, `ChickenPeck`, `ChickenSleeping`,
  `ChickenAttack`) and the 400 Sounds Pack. The Java game did not use them
  either; they are still in the Java project if you want them.
