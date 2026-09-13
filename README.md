# Rotten Eggs

A little arcade game about chickens with terrible aim.

Three chickens are perched above you, laying eggs as fast as they can. You've
got a basket. Catch what falls, throw it back, and don't let a single egg hit
the dirt — every one you miss costs you half a heart.

## How it plays

**Single player** — Take on three chickens at once. Every egg you catch goes
into your basket, and every egg in your basket is ammo. Four clean hits knock a
chicken off its perch; clear all three and you win. The longer you last, the
faster the eggs come down. Catch a **speed egg** and you'll move like lightning
for a few seconds, which you will need.

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
