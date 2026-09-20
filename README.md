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
| Stage 2 — Respawn | Knock down six; a downed chicken climbs back up ten seconds later |
| Boss stage | One oversized boss that only takes hits while it sleeps — see below |

Hearts, ammo, score and shields carry over between stages, so a clear reads as
progress rather than a fresh start, and every stage ramps faster than the last.

**The boss** cycles through three phases and is only vulnerable in the last:

| Phase | What happens | What you do |
|---|---|---|
| Flight | It chases your basket, stalls overhead, and drops a wave of feathers | Keep moving — a feather that lands on you costs half a heart |
| Bombs | Back on its perch, it drops a bomb on every jump; the fuse flashes red before it blows | Get clear of the blast |
| Sleep | It dozes off. Eggs thrown now land | Throw what you banked — two hits wake it, so make them count |

Eggs thrown at it while awake bounce off. It takes eight hits in total.

Single-player power eggs:

| Egg | What it does |
|---|---|
| Speed | A burst of speed for 5 seconds |
| Shield | Banks a shield (up to three) that absorbs one missed egg |
| Slow down | Halves the falling speed of every egg for 5 seconds |
| Golden | 50 points before the combo multiplier |

**Duo mode** — Two baskets, two players, one screen. Both sides get exactly
the same eggs at the same moment, mirrored across the divider, so the only
difference between the two halves is the people. You can't hit each other
directly; you outlast the other player and make their life difficult along
the way.

Power eggs aren't used the moment you catch them. They go into two slots, and
you pick when to fire them — freeze the other player as a cluster is about to
land on them, or hold your speed for the wave you can see coming. Catching a
third egg with both slots full replaces the one you have selected.

| Egg | What it does when used |
|---|---|
| Speed | Gives you a burst of speed |
| Freeze | Locks the other player in place for 2 seconds |
| Reverse | Flips the other player's controls for 3 seconds |
| Golden | Makes the other player's eggs fall faster for 5 seconds |

At **90 seconds** the match goes to sudden death: eggs fall faster and keep
speeding up until someone breaks. Last basket standing wins.

Catch ten in a row in either mode and you hit **Fever** — triple points until
you miss.

## Controls

| | |
|---|---|
| Menu | `W`/`S` or `↑`/`↓` to choose · `Enter` to start · `1`/`2` to jump straight in |
| Single | `A`/`D` to move · `Space` to throw · `R` restart · `Esc` back to menu |
| Duo — player 1 | `A`/`D` move · `W`/`S` pick a banked egg · `Space` use it |
| Duo — player 2 | `←`/`→` move · `↑`/`↓` pick a banked egg · `Right Shift` use it |
| Duo — both | `R` restart · `Esc` back to menu |
| Audio | `M` mute · `-`/`+` volume |
| Pause | `P` pause/resume · `W`/`S` or `↑`/`↓` select · `Enter` confirm · `Esc` resume |

**Controller (player 1)** — Left stick or D-pad moves and selects menu items;
in Duo, up/down on either picks a banked egg. South (`A` / Cross) confirms;
east (`B` / Circle) throws in single player, uses the banked egg in Duo, resumes
from pause, and returns to the menu from results. Start pauses/resumes, west
(`X` / Square) restarts, Select/View/Share toggles mute, and the left/right
bumpers lower/raise volume. The pause menu offers Resume, Restart, Main Menu,
and Quit Game; the main menu also offers Quit Game. Quitting stops Play mode
in the Unity Editor and closes the standalone game. Player 2 keeps the arrow
keys in Duo. Controller hints appear only after actual controller input—not
just connecting one.

## Running it

Open the project in Unity 6, open `Assets/Scenes/SampleScene.unity`, and press
Play. That's it — the game sets itself up.

A 16:9 Game view looks best. The whole game is drawn at 480×270 and scaled up
in whole steps, so the pixels stay crisp at any window size.

## Credits

Art and music from other creators are listed in [CREDITS.md](CREDITS.md).

Developers: Chanyuphyea Lorn, Ye Htet Aung.
