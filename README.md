# Rotten Eggs

A little arcade game about chickens with terrible aim.

Three chickens are perched above you, laying eggs as fast as they can. You've
got a basket. Catch what falls, throw it back, and keep the eggs off the dirt —
in single player every miss costs you half of your five hearts, and in Duo it
costs half of your three.

## How it plays

**Single player** — Take on three chickens at once, with five hearts in the
bank. Every egg you catch goes into your basket, and every egg in your basket
is ammo. Four clean hits knock a chicken off its perch. The run is three stages
long and they arrive one after the other, so the game is only won by clearing
every one of them:

| Stage | The job |
|---|---|
| Stage 1 — Classic | Knock down all three chickens |
| Stage 2 — Respawn | Knock down six; a downed chicken climbs back up five seconds later |
| Boss stage | One oversized boss with eight hits |

Hearts, ammo, score and shields carry over between stages, so a clear reads as
progress rather than a fresh start, and every stage ramps faster than the last.

Single-player power eggs:

| Egg | What it does |
|---|---|
| Speed | A burst of speed for 5 seconds |
| Shield | Banks a shield (up to three) that absorbs one missed egg |
| Slow down | Halves the falling speed of every egg for 5 seconds |
| Golden | 50 points before the combo multiplier |

**Duo mode** — Two baskets, two players, one screen. You can't hit each other
directly, so the game is about outlasting the other player and making their
life difficult along the way. Some eggs are sabotage:

| Egg | What it does |
|---|---|
| Speed | Gives you a burst of speed |
| Freeze | Locks the other player in place for 2 seconds |
| Reverse | Flips the other player's controls for 3 seconds |
| Golden | Makes the other player's eggs fall faster for 5 seconds |

Last basket standing wins.

Catch ten in a row in either mode and you hit **Fever** — triple points until
you miss.

## Controls

| | |
|---|---|
| Menu | `W`/`S` or `↑`/`↓` to choose · `Enter` to start · `1`/`2` to jump straight in |
| Single | `A`/`D` to move · `Space` to throw · `R` restart · `Esc` back to menu |
| Duo | Player 1 `A`/`D` · Player 2 `←`/`→` · `R` restart · `Esc` back to menu |
| Audio | `M` mute · `-`/`+` volume |

## Running it

Open the project in Unity 6, open `Assets/Scenes/SampleScene.unity`, and press
Play. That's it — the game sets itself up.

A 16:9 Game view looks best. The whole game is drawn at 480×270 and scaled up
in whole steps, so the pixels stay crisp at any window size.

## Credits

Art and music from other creators are listed in [CREDITS.md](CREDITS.md).

Developers: Chanyuphyea Lorn, Ye Htet Aung.
