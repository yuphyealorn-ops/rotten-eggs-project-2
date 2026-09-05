# Rotten Eggs — the Unity build

This is the playable Unity version: real GameObjects, prefabs, sprites, an
Animator and a UI canvas. It is the one to keep building on.

## Run it

Open `Assets/Scenes/RottenEggs.unity` and press **Play**. Press `1` for single
player or `2` for duo.

| | |
|---|---|
| Menu | `1` single player · `2` duo versus |
| Player 1 | `A` / `D` move · `Space` throw a stacked egg |
| Player 2 | `←` / `→` move (duo only) |
| Any time | `R` restart · `Esc` back to the menu |

In single player the arrow keys move player one too, so you can play one handed.

## The scripts

All of them live in `Assets/Scripts/` and are plain `MonoBehaviour`s in the same
style as the Prototype and Challenge projects — public fields you tune in the
Inspector, `Start` and `Update`, `Instantiate` and `Destroy`.

| Script | What it owns |
|---|---|
| `GameManager` | Mode, phase, the clock, the difficulty tier, who won |
| `BasketController` | One player: moving, lives, score, stacked eggs, throwing, power up timers |
| `EggController` | One falling egg: how fast it drops, landing in a basket, cracking on the ground |
| `ProjectileController` | A thrown egg going back up, and hitting a chicken |
| `ChickenController` | Patrolling, hit points, and picking the animation clip |
| `SpawnManager` | When a chicken lays, and which kind of egg comes out |
| `HUDController` | The readouts and the message in the middle |

## Where the GDD rules live

Every number below is a public field, so you can retune it without touching code.

| GDD rule | Where |
|---|---|
| Defeat 3 chickens | `GameManager.chickensToDefeat` |
| Start with 3 lives | `BasketController.startingHalfLives` = 6 halves |
| Missing an egg costs half a life | `BasketController.MissEgg` |
| Each chicken takes 4 eggs | `ChickenController.maxHitPoints` |
| Eggs stack and get thrown back | `BasketController.ammo`, `Throw()` |
| Difficulty ramps by falling faster | `GameManager.baseFallSpeed` + `fallSpeedPerTier` |
| Freeze the opponent for 2s | `BasketController.freezeDuration` |
| Reverse their controls for 3s | `BasketController.reverseDuration` |
| Golden egg speeds their eggs for 5s | `BasketController.sabotageDuration`, `GameManager.sabotageFallMultiplier` |
| Speed boost | `BasketController.boostedMoveSpeed`, `speedDuration` |
| Duo players cannot attack directly | `BasketController.Update` only throws in single mode |

## Putting your own art in

The placeholder shapes are ordinary PNGs. Replace the file and everything that
uses it updates — no code, no rewiring.

| File | Used for |
|---|---|
| `Sprites/egg.png` | Every egg. It is white on purpose: `EggController` tints it per kind, so one file covers all five |
| `Sprites/basket.png` | Both baskets, tinted per player |
| `Sprites/pixel.png` | The flat blocks — sky, ground, perches, the duo divider |
| `Sprites/Chicken/*.png` | The 25 chicken frames, already your real artwork |

Keep the pixel size the same and it will line up. If you change the size, set
**Pixels Per Unit** to match: the world is 15 pixels to one unit, so a 48 px
wide basket uses 15. The chicken frames use 7.5 because that art is drawn at
double size.

To swap a chicken frame, drop a new 20 x 21 PNG over the matching file in
`Sprites/Chicken/`. The animation clips point at the files by name, so the
animation picks it up with no extra work.

## Menu commands

- **Rotten Eggs → Check Scene** — makes sure every reference is still wired and
  the GDD numbers still hold. Run it if something stops working.
- **Rotten Eggs → Build Unity Scene** — rebuilds the whole scene from scratch.
  ⚠️ This overwrites `Scenes/RottenEggs.unity`, so do not run it once you have
  started editing the scene by hand.

## Still to do

- Sound. The 12 WAVs are in `StreamingAssets/RottenEggs/Audio/`; the Unity build
  does not play them yet. An `AudioSource` plus `PlayOneShot` on catch, throw,
  hit and miss is the smallest thing that would work.
- Art for the background, baskets and eggs, replacing the placeholder shapes.
- A proper menu screen. Right now the menu is the message in the middle.

## The other folder

`Assets/RottenEggs/` is the older port from the Java version. It draws the whole
game as pixels into one texture, so it uses no sprites or prefabs and cannot
build to WebGL. It is kept because `Scripts/GameModel.cs` is a clean, complete
statement of the rules worth checking against — but the Unity build above is the
one to develop.
