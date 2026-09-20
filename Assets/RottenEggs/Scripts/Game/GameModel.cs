using System;
using System.Collections.Generic;
using UnityEngine;

namespace RottenEggs
{
    public enum Phase
    {
        Menu,
        Playing,
        Won,
        Lost
    }

    public enum Mode
    {
        Single,
        Duo
    }

    public enum EggKind
    {
        Normal,
        Speed,
        Freeze,
        Reverse,
        Golden,
        Shield,        // Power-up: absorb one hit
        SlowDown       // Power-up: slow all falling eggs
    }

    public enum Stage
    {
        Stage1,        // Original: 3 chickens, no respawn
        Stage2,        // Respawn chickens when knocked down
        BossStage      // One boss chicken with 8 HP
    }

    /// <summary>
    /// The boss's attack cycle. It is airborne and raining feathers, on its
    /// perch dropping bombs, or asleep — and only asleep can it be hit.
    /// </summary>
    public enum BossPhase
    {
        Fly,
        Bomb,
        Sleep
    }

    /// <summary>What the airborne boss is doing: cruising, chasing, or stalled overhead.</summary>
    public enum BossFlyState
    {
        Sweeping,
        Approaching,
        Hovering
    }

    /// <summary>
    /// Which chicken clip is on screen. Looping states keep replaying while
    /// their condition still holds; the rest run through once and hold their
    /// final frame until their action happens again.
    /// </summary>
    public enum AnimState
    {
        Idle,
        Walking,
        Jumping,
        Damage,
        Die,
        Fly,           // Boss: airborne while the feather attack winds up
        Sleeping       // Boss: dozing, the only time it can be hit
    }

    public enum EventType
    {
        Catch,
        Throw,
        Hit,
        Miss,
        Power,
        ChickenDown,
        Win,
        Lose,
        Explosion,     // a bomb went off
        Feather        // a feather wave was released
    }

    public static class AnimStates
    {
        public static readonly AnimState[] All =
        {
            AnimState.Idle,
            AnimState.Walking,
            AnimState.Jumping,
            AnimState.Damage,
            AnimState.Die,
            AnimState.Fly,
            AnimState.Sleeping
        };

        /// <summary>
        /// Idle, walking, flying and sleeping repeat because their condition
        /// keeps being true; the rest run once and hold their last frame.
        /// </summary>
        public static bool Loops(this AnimState state)
        {
            return state == AnimState.Idle || state == AnimState.Walking
                || state == AnimState.Fly || state == AnimState.Sleeping;
        }
    }

    /// <summary>
    /// Hardware-independent rules for both the single-player and local Duo modes.
    /// Everything uses a small 480 x 270 logical canvas for crisp pixel rendering.
    /// </summary>
    public sealed class GameModel
    {
        public const int WorldW = 480;
        public const int WorldH = 270;
        public const double GroundY = 238;
        // Sits the basket on top of the painted ground line rather than in it:
        // the backdrop's grass edge falls at canvas row ~227, so a 15-tall
        // basket starting here has its base at 224, just clear of the dirt.
        public const double BasketY = 209;
        public const double BasketW = 48;
        public const double BasketH = 15;
        public const double EggW = 14;
        public const double EggH = 18;
        // Keep the original basket bottom fixed while aligning catches to the new rim.
        public const double BasketSpriteY = BasketY + BasketH - 25;
        public const double BasketRimY = BasketSpriteY + 4;
        public const double BasketFrameSeconds = 0.025;
        public const double BasketAnimSeconds = 4 * BasketFrameSeconds;
        public const double ThrowReleaseSeconds = 2 * BasketFrameSeconds;

        /// <summary>Half-hearts each mode starts with. Single player gets five hearts.</summary>
        public const int SingleMaxLivesHalf = 10;
        public const int DuoMaxLivesHalf = 6;

        /// <summary>
        /// Basket speed in pixels per second. A lone player covers the whole
        /// 480px arena, so single player moves faster than a duo player who
        /// only has half of it; the boosted speeds keep the speed egg a clear
        /// step up in both modes.
        /// </summary>
        public const double SingleMoveSpeed = 180;
        public const double SingleBoostSpeed = 250;
        public const double DuoMoveSpeed = 145;
        public const double DuoBoostSpeed = 220;

        // ── Duo ───────────────────────────────────────────────────────────────
        // Power eggs are banked and used on a button rather than firing on catch,
        // so the timing is the player's. Both sides get the same eggs at mirrored
        // positions, and a clock ends stalemates.
        public const int DuoSlotCount = 2;
        public const double DuoSuddenDeathSeconds = 90;
        public const double DuoSuddenDeathStart = 1.4;    // fall-speed multiplier the moment it begins
        public const double DuoSuddenDeathRamp = 0.03;    // added per second after that
        public const double DuoSuddenDeathMax = 2.5;

        /// <summary>Stage 2: how long a downed chicken stays off its perch.</summary>
        public const double Stage2RespawnSeconds = 10.0;

        // ── Boss fight ────────────────────────────────────────────────────────
        // The boss cycles Fly → Bomb → Sleep and can only be hurt while asleep.
        public const int BossHp = 8;
        public const int BossHitsPerSleep = 2;             // it wakes after this many, so a sleep can't be spammed
        public const double BossFlySeconds = 8.0;
        public const double BossBombSeconds = 8.0;
        public const double BossSleepSeconds = 6.0;
        public const double BossFirstAttackDelay = 1.2;   // lets each phase announce itself
        public const double BossFlyLift = 34;             // px the boss rises above its perch
        public const double BossFlySpeed = 70;            // px/s sweep while airborne
        public const double BossLiftSpeed = 90;           // px/s take-off / landing
        public const double BossFlyMinX = 48;             // airborne, it ranges over the whole arena...
        public const double BossFlyMaxX = 432;
        public const double BossApproachSpeed = 120;      // ...and chases the basket slower than it can run
        public const double BossApproachMaxSeconds = 3.5; // long enough to cross the arena, so no corner is safe from a standstill
        public const double BossArriveDistance = 4;

        // Feather wave: the boss stalls overhead and a warning sits beneath it
        // while it hovers; on release a wave of tumbling feathers falls from
        // under its body. Each one is its own projectile, so the wave has width
        // and the feathers arrive a little apart, but a wave costs at most half
        // a heart. Feathers that miss drift on to the dirt and burst there.
        public const double FeatherStrikeInterval = 1.6;  // sweep time between attacks
        public const double FeatherWindupSeconds = 0.6;   // the hover; the warning shows for all of it
        public const double FeatherReleaseOffset = 70;    // wave origin below the boss's top edge
        public const double FeatherFallSpeed = 140;       // px/s before each feather's own 0.8-1.2 factor
        public const double FeatherWaveSpread = 22;       // half-width of the wave around the release point
        public const double FeatherBurstSeconds = 0.24;   // 4 burst frames at 60ms
        public const int FeathersPerWave = 5;
        public const int DownPerWave = 3;                 // small fluff mixed in so the wave is not uniform
        public const int FeatherW = 12;
        public const int FeatherH = 14;
        public const int DownW = 8;
        public const int DownH = 8;
        public const int FeatherBurstSize = 16;
        public const int FlockWarnW = 12;
        public const int FlockWarnH = 8;

        // Bomb: dropped on the lay frame of the jump onto the egg rect, so it falls
        // through the same path with the same hit box as everything else the
        // player judges. It sits with a burning fuse, flashes red, then blows.
        public const double BombDropInterval = 2.4;
        public const double BombDropFrameSeconds = 0.4;   // frame 5 of the 6-frame jump
        public const double BombFallSpeed = 160;
        public const double BombFuseSeconds = 1.0;
        public const double BombArmedSeconds = 0.5;       // red flash for the last half second
        public const double BombExplodeSeconds = 0.42;    // 7 blast frames at 60ms
        public const double BombHurtWindow = 0.24;        // the four fireball frames
        public const double BombBlastRadius = 40;         // fireball edge touching the basket edge
        public const int BombW = 14;
        public const int BombH = 18;
        public const int BlastSize = 32;
        /// <summary>The bomb settles on the same ground plane as the basket, clear of the dirt.</summary>
        public const double BombRestY = BasketY + BasketH;

        /// <summary>Chicken artwork is drawn from its top-left, with the feet 40px down.</summary>
        public const double ChickenH = 40;

        /// <summary>Wooden perch every chicken stands on, so no chicken floats in the air.</summary>
        public const double PerchH = 7;
        public const double PerchMargin = 26;

        /// <summary>One-shot clip lengths, matched to the bundled sheets at 0.1s per frame.</summary>
        public const double LayAnimSeconds = 0.60;
        public const double DamageAnimSeconds = 0.60;
        /// <summary>
        /// The damage sheet's first three frames show the chicken unhurt; the red
        /// flash starts on frame four. The clip is started here so a hit shows
        /// instantly instead of a beat later.
        /// </summary>
        public const double DamageLeadInSeconds = 0.30;
        public const double DieAnimSeconds = 0.40;

        /// <summary>Idle beat a chicken takes after turning around at the end of its lane.</summary>
        public const double TurnPauseSeconds = 0.35;

        public static readonly Color32 Cream = new Color32(255, 244, 216, 255);
        public static readonly Color32 Cyan = new Color32(67, 198, 219, 255);
        public static readonly Color32 Ice = new Color32(158, 232, 255, 255);
        public static readonly Color32 Mint = new Color32(112, 232, 176, 255);
        public static readonly Color32 Purple = new Color32(171, 113, 255, 255);
        public static readonly Color32 Gold = new Color32(255, 222, 89, 255);
        public static readonly Color32 Pink = new Color32(232, 45, 119, 255);
        public static readonly Color32 Dark = new Color32(36, 49, 60, 255);

        public sealed class GameEvent
        {
            public readonly EventType Type;
            public readonly int Player;

            public GameEvent(EventType type, int player)
            {
                Type = type;
                Player = player;
            }
        }

        public sealed class PlayerState
        {
            public readonly int Index;
            public double ArenaMinX;
            public double ArenaMaxX;
            public double BasketX;
            public int LivesHalf;
            public int Ammo;
            public int Score;
            public int Combo;
            public double SpeedTime;
            public double FreezeTime;
            public double ReverseTime;
            public double SabotageTime;
            public double FireCooldown;
            public double CatchTime = -1;
            public double CatchX;
            public double ThrowTime = -1;
            public bool PendingThrow;
            public double StatusTimer;
            public string StatusText = "";
            public int ShieldCount;       // Number of shield power-ups
            public readonly List<EggKind> Slots = new List<EggKind>();   // Duo: banked power eggs
            public int SelectedSlot;      // Duo: which banked egg the use key fires
            public double SlowDownTimer;  // Slow-down power-up active timer

            public PlayerState(int index)
            {
                Index = index;
            }

            public Rect BasketBounds()
            {
                return new Rect((float)BasketX, (float)BasketRimY, (float)BasketW, 22);
            }

            public int Multiplier()
            {
                if (Combo >= 10)
                {
                    return 3;
                }

                if (Combo >= 5)
                {
                    return 2;
                }

                return 1;
            }

            /// <summary>
            /// Fever starts at the 10-catch streak, where the existing score
            /// multiplier reaches its maximum. Keeping it derived from Combo
            /// means a miss still ends Fever immediately.
            /// </summary>
            public bool InFever()
            {
                return Combo >= 10;
            }
        }

        public sealed class Chicken
        {
            public readonly int Owner;
            public readonly int Lane;
            public readonly double StartX;
            public readonly double MinX;
            public readonly double MaxX;
            public readonly double Y;
            public readonly double SpeedScale;
            public readonly int StartDirection;
            public readonly bool CanRespawn;         // Stage 2: respawn after death
            public readonly double RespawnTime;      // Seconds until respawn
            public readonly bool IsBoss;            // Boss stage: bigger HP/speed
            public double CenterX;
            public int Direction;
            public int Hp = 4;
            public int Facing;
            public AnimState Anim = AnimState.Walking;

            /// <summary>Seconds spent in the current clip, which drives frame selection.</summary>
            public double AnimTime;

            /// <summary>Seconds left before a one-shot clip releases the chicken.</summary>
            public double ActionTime;

            /// <summary>Seconds left standing still, which is what puts the idle clip up.</summary>
            public double StandTime;

            /// <summary>For respawn: counts down until chicken reappears</summary>
            public double RespawnTimer;

            // ── Boss only ─────────────────────────────────────────────────────
            public BossPhase BossPhase = BossPhase.Fly;
            public double BossPhaseTime;
            public double BossAttackTimer;
            public BossFlyState FlyState = BossFlyState.Sweeping;
            public double FlyStateTime;
            public int SleepHits;   // hits landed during the current sleep
            /// <summary>Set when a jump has started and its bomb has not yet left the boss.</summary>
            public bool BombPending;
            /// <summary>How far above the perch the boss currently hovers; 0 on the ground.</summary>
            public double FlyLift;

            /// <summary>Where the top of the artwork is this frame, perch height minus lift.</summary>
            public double DrawY
            {
                get { return Y - FlyLift; }
            }

            /// <summary>The boss drops its guard only while it sleeps.</summary>
            public bool BossVulnerable
            {
                get { return IsBoss && Alive() && BossPhase == BossPhase.Sleep; }
            }

            public Chicken(
                int owner,
                int lane,
                double centerX,
                double y,
                double minX,
                double maxX,
                double speedScale,
                int direction)
                : this(owner, lane, centerX, y, minX, maxX, speedScale, direction, false, 0.0, false)
            {
            }

            public Chicken(
                int owner,
                int lane,
                double centerX,
                double y,
                double minX,
                double maxX,
                double speedScale,
                int direction,
                bool canRespawn,
                double respawnTime)
                : this(owner, lane, centerX, y, minX, maxX, speedScale, direction, canRespawn, respawnTime, false)
            {
            }

            public Chicken(
                int owner,
                int lane,
                double centerX,
                double y,
                double minX,
                double maxX,
                double speedScale,
                int direction,
                bool canRespawn,
                double respawnTime,
                bool isBoss)
            {
                Owner = owner;
                Lane = lane;
                StartX = centerX;
                CenterX = centerX;
                Y = y;
                MinX = minX;
                MaxX = maxX;
                SpeedScale = speedScale;
                StartDirection = direction;
                Direction = direction;
                Facing = direction;
                CanRespawn = canRespawn;
                RespawnTime = respawnTime;
                IsBoss = isBoss;
                if (isBoss)
                {
                    Hp = 8;  // Boss has 8 HP
                }
            }

            public bool Alive()
            {
                return Hp > 0;
            }

            public bool IsRespawning()
            {
                return !Alive() && CanRespawn && RespawnTimer > 0;
            }

            public Rect Bounds()
            {
                // The boss is drawn at a larger scale, so its hit box grows with it,
                // and it follows the boss into the air.
                double size = IsBoss ? 54 : 36;
                return new Rect((float)(CenterX - size / 2), (float)(DrawY + 4), (float)size, (float)size);
            }

            /// <summary>Starts a clip that runs once, optionally rooting the chicken.</summary>
            public void PlayOnce(AnimState state, double seconds, bool stopWalking)
            {
                Anim = state;
                double leadIn = state == AnimState.Damage ? DamageLeadInSeconds : 0;
                AnimTime = leadIn;
                ActionTime = Math.Max(0, seconds - leadIn);
                if (stopWalking)
                {
                    StandTime = Math.Max(StandTime, seconds);
                }
            }

            /// <summary>Switches to a looping clip, keeping its timeline if it is already up.</summary>
            public void Loop(AnimState state)
            {
                if (Anim != state)
                {
                    Anim = state;
                    AnimTime = 0;
                }
            }

            /// <summary>Reset chicken to alive state for respawn</summary>
            public void Respawn()
            {
                Hp = IsBoss ? 8 : 4;
                CenterX = StartX;
                Direction = StartDirection;
                Facing = StartDirection;
                Anim = AnimState.Walking;
                AnimTime = 0;
                StandTime = 0;
                ActionTime = 0;
            }
        }

        public sealed class FallingEgg
        {
            public readonly EggKind Kind;
            public readonly int Owner;
            public readonly int SourceLane;
            public double X;
            public double Y;
            public double Age;

            public FallingEgg(EggKind kind, int owner, int sourceLane, double x, double y)
            {
                Kind = kind;
                Owner = owner;
                SourceLane = sourceLane;
                X = x;
                Y = y;
            }

            public Rect Bounds()
            {
                return new Rect((float)X, (float)Y, (float)EggW, (float)EggH);
            }
        }

        /// <summary>
        /// A shell left behind when a falling egg smashes on the ground. It is a
        /// short-lived visual only, so it replaces the old particle "explosion"
        /// with a readable crack while the miss penalty is still applied.
        /// </summary>
        public sealed class CrackedEgg
        {
            public readonly EggKind Kind;
            public readonly double X;
            public readonly double Y;
            public double Life;
            public readonly double MaxLife;

            public CrackedEgg(EggKind kind, double x, double y, double life)
            {
                Kind = kind;
                X = x;
                Y = y;
                Life = life;
                MaxLife = life;
            }
        }

        public sealed class Shot
        {
            public double X;
            public double Y;
            public double Age;

            public Shot(double x, double y)
            {
                X = x;
                Y = y;
            }

            public Rect Bounds()
            {
                return new Rect((float)X, (float)Y, (float)EggW, (float)EggH);
            }
        }

        public sealed class Particle
        {
            public double X;
            public double Y;
            public double Vx;
            public double Vy;
            public double Life;
            public readonly double MaxLife;
            public readonly int Size;
            public readonly Color32 Color;

            public Particle(double x, double y, double vx, double vy, double life, int size, Color32 color)
            {
                X = x;
                Y = y;
                Vx = vx;
                Vy = vy;
                Life = life;
                MaxLife = life;
                Size = size;
                Color = color;
            }
        }

        public readonly System.Random Random;
        public readonly PlayerState[] Players = { new PlayerState(0), new PlayerState(1) };
        public readonly List<Chicken> Chickens = new List<Chicken>();
        public readonly List<FallingEgg> FallingEggs = new List<FallingEgg>();
        public readonly List<Shot> Shots = new List<Shot>();
        public readonly List<CrackedEgg> CrackedEggs = new List<CrackedEgg>();
        public readonly List<Bomb> Bombs = new List<Bomb>();
        public readonly List<FeatherStrike> Feathers = new List<FeatherStrike>();

        /// <summary>
        /// A bomb the boss lets go of mid-jump. It falls on the egg rect, lands,
        /// burns its fuse, flashes red, then blows; the blast hurts a basket
        /// within <see cref="BombBlastRadius"/> of its centre during the
        /// fireball frames, once.
        /// </summary>
        public sealed class Bomb
        {
            public double X;          // top-left of the 14x18 art
            public double Y;
            public double Age;        // drives the falling wobble
            public bool Landed;
            public double FuseTime;   // seconds since landing
            public bool Exploded;
            public double BlastTime;  // seconds since detonation
            public bool Hurt;

            public Bomb(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double CenterX
            {
                get { return X + BombW / 2.0; }
            }

            public double CenterY
            {
                get { return Y + BombH / 2.0; }
            }

            /// <summary>The red flash in the last half second of the fuse.</summary>
            public bool Armed
            {
                get { return Landed && !Exploded && FuseTime >= BombFuseSeconds - BombArmedSeconds; }
            }
        }

        /// <summary>One feather (or a bit of down) tumbling out of a wave.</summary>
        public sealed class Feather
        {
            public double X;                    // top-left
            public double Y;
            public readonly double Vy;
            public readonly double FrameOffset; // seconds, so the flutters never sync up
            public readonly bool Down;          // the small fluff
            public bool Landed;
            public double BurstTime;

            public Feather(double x, double y, double vy, double frameOffset, bool down)
            {
                X = x;
                Y = y;
                Vy = vy;
                FrameOffset = frameOffset;
                Down = down;
            }

            public int Width
            {
                get { return Down ? DownW : FeatherW; }
            }

            public int Height
            {
                get { return Down ? DownH : FeatherH; }
            }

            public Rect Bounds()
            {
                return new Rect((float)X, (float)Y, Width, Height);
            }
        }

        /// <summary>
        /// A feather wave from the stalled boss. While it winds up, a warning
        /// sits under the bird; on release the feathers fall from there, each
        /// on its own path, and the first to reach the basket is the one that
        /// hurts. The wave is over when every feather has landed and burst.
        /// </summary>
        public sealed class FeatherStrike
        {
            public readonly double X;       // centre, fixed at launch: directly under the stalled boss
            public readonly double StartY;  // where the wave forms, just beneath the boss
            public readonly List<Feather> Feathers = new List<Feather>();
            public double Time;
            public bool Released;
            public bool Hurt;

            public FeatherStrike(double x, double startY)
            {
                X = x;
                StartY = startY;
            }

            public bool WindingUp
            {
                get { return !Released; }
            }

            public bool Finished
            {
                get { return Released && Feathers.Count == 0; }
            }
        }

        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly double[] SpawnTimers = new double[2];

        /// <summary>Single-player run length, shown on the menu option.</summary>
        public const int StageCount = 3;

        public static int StageIndex(Stage stage)
        {
            switch (stage)
            {
                case Stage.Stage2:
                    return 2;
                case Stage.BossStage:
                    return 3;
                default:
                    return 1;
            }
        }

        /// <summary>Row title the HUD and result screens show for a stage.</summary>
        public static string StageTitle(Stage stage)
        {
            switch (stage)
            {
                case Stage.Stage2:
                    return "STAGE 2  RESPAWN";
                case Stage.BossStage:
                    return "BOSS STAGE";
                default:
                    return "STAGE 1  CLASSIC";
            }
        }

        public Phase Phase = Phase.Menu;
        public Mode Mode = Mode.Single;
        public Stage CurrentStage = Stage.Stage1;
        public int StageLevel = 1;        // 1, 2, or 3 (boss)
        public int Defeated;
        public int WinnerPlayer;
        public double Elapsed;
        public double LastPowerSpawnP0;   // per-player for fair duo distribution
        public double LastPowerSpawnP1;
        public double ShakeTime;

        /// <summary>
        /// Set the moment a stage goal is reached and spent at the end of Update,
        /// so a stage swap never rewrites the egg or shot lists mid-iteration.
        /// </summary>
        private bool stageAdvancePending;

        public int StageNumber
        {
            get { return StageIndex(CurrentStage); }
        }

        /// <summary>Knockdowns needed to clear the stage that is running now.</summary>
        public int StageGoal
        {
            get
            {
                switch (CurrentStage)
                {
                    case Stage.Stage2:
                        return 6;
                    case Stage.BossStage:
                        return 1;
                    default:
                        return 3;
                }
            }
        }

        /// <summary>Short stage name for the HUD and the result screen.</summary>
        public string StageLabel
        {
            get { return CurrentStage == Stage.BossStage ? "BOSS" : "STAGE " + StageNumber; }
        }

        public GameModel() : this(new System.Random())
        {
        }

        public GameModel(System.Random random)
        {
            Random = random;
            ResetRoundData(Mode.Single);
            Phase = Phase.Menu;
        }

        public PlayerState Player(int index)
        {
            return Players[index];
        }

        public void StartRound(Mode selectedMode)
        {
            StartRound(selectedMode, CurrentStage);
        }

        public void StartRound(Mode selectedMode, Stage stage)
        {
            CurrentStage = stage;
            StageLevel = StageIndex(stage);
            stageAdvancePending = false;
            ResetRoundData(selectedMode);
            Phase = Phase.Playing;
            if (Mode == Mode.Single)
            {
                SetStatus(Players[0], StageLabel + "  •  CATCH. AIM AHEAD. THROW.", 2.0);
            }
            else
            {
                SetStatus(Players[0], "P1 READY", 1.2);
                SetStatus(Players[1], "P2 READY", 1.2);
            }
        }

        public void RestartCurrentMode()
        {
            StartRound(Mode, CurrentStage);
        }

        public void ReturnToMenu()
        {
            CurrentStage = Stage.Stage1;
            StageLevel = 1;
            stageAdvancePending = false;
            ResetRoundData(Mode.Single);
            Phase = Phase.Menu;
        }

        /// <summary>
        /// Swaps in the next single-player stage once the current one is cleared.
        /// Hearts, score, ammo and shields carry over, so a clear reads as progress
        /// rather than a fresh start. Called from Update so a stage swap never
        /// rewrites the egg or shot lists while they are being iterated.
        /// </summary>
        private void ApplyStageAdvance()
        {
            PlayerState player = Players[0];
            int lives = player.LivesHalf;
            int ammo = player.Ammo;
            int score = player.Score;
            int combo = player.Combo;
            int shields = player.ShieldCount;

            Stage next;
            string banner;
            switch (CurrentStage)
            {
                case Stage.Stage1:
                    next = Stage.Stage2;
                    banner = StageTitle(next) + "!";
                    break;
                case Stage.Stage2:
                    next = Stage.BossStage;
                    banner = StageTitle(next) + "!";
                    break;
                default:
                    Phase = Phase.Won;
                    WinnerPlayer = 1;
                    SetStatus(player, "ALL STAGES CLEARED!", 2.0);
                    Emit(EventType.Win, 0);
                    return;
            }

            CurrentStage = next;
            StageLevel = StageIndex(next);
            ResetRoundData(Mode.Single);
            player.LivesHalf = lives;
            player.Ammo = ammo;
            player.Score = score;
            player.Combo = combo;
            player.ShieldCount = shields;
            SetStatus(player, banner, 2.0);
            Emit(EventType.Power, 0);
        }

        private void ResetRoundData(Mode selectedMode)
        {
            Mode = selectedMode;
            Chickens.Clear();
            if (Mode == Mode.Single)
            {
                // Stage-based chicken setup
                if (CurrentStage == Stage.Stage1)
                {
                    // Stage 1: Original - 3 normal chickens, no respawn
                    Chickens.Add(new Chicken(0, 0, 98, 48, 48, 140, 0.86, 1));
                    Chickens.Add(new Chicken(0, 1, 240, 42, 194, 286, 1.05, -1));
                    Chickens.Add(new Chicken(0, 2, 382, 48, 340, 432, 0.94, 1));
                }
                else if (CurrentStage == Stage.Stage2)
                {
                    // Stage 2: the same three lanes, but a downed chicken climbs
                    // back onto its perch after a while until the goal is met.
                    Chickens.Add(new Chicken(0, 0, 98, 48, 48, 140, 0.86, 1, true, Stage2RespawnSeconds));
                    Chickens.Add(new Chicken(0, 1, 240, 42, 194, 286, 1.05, -1, true, Stage2RespawnSeconds));
                    Chickens.Add(new Chicken(0, 2, 382, 48, 340, 432, 0.94, 1, true, Stage2RespawnSeconds));
                }
                else // BossStage
                {
                    // Boss Stage: one oversized boss that cycles flight, bombs and
                    // sleep, and only takes its eight hits while it sleeps.
                    Chicken boss = new Chicken(0, 0, 240, 64, 120, 340, 0.55, 1, false, 0.0, true);
                    Chickens.Add(boss);
                    EnterBossPhase(boss, BossPhase.Fly);
                }
            }
            else
            {
                Chickens.Add(new Chicken(0, 0, 65, 49, 45, 95, 0.88, 1));
                Chickens.Add(new Chicken(0, 1, 176, 57, 150, 205, 1.04, -1));
                Chickens.Add(new Chicken(1, 0, 304, 57, 275, 330, 1.04, 1));
                Chickens.Add(new Chicken(1, 1, 415, 49, 385, 435, 0.88, -1));
            }

            FallingEggs.Clear();
            Shots.Clear();
            Particles.Clear();
            CrackedEggs.Clear();
            Bombs.Clear();
            Feathers.Clear();
            Events.Clear();

            ResetPlayer(Players[0], selectedMode);
            ResetPlayer(Players[1], selectedMode);
            if (Mode == Mode.Single)
            {
                Players[0].ArenaMinX = 4;
                Players[0].ArenaMaxX = WorldW - BasketW - 4;
                Players[0].BasketX = (WorldW - BasketW) / 2.0;
                Players[1].ArenaMinX = 244;
                Players[1].ArenaMaxX = WorldW - BasketW - 4;
                Players[1].BasketX = 336;
                SpawnTimers[0] = 0.85;
                SpawnTimers[1] = 999;
            }
            else
            {
                Players[0].ArenaMinX = 4;
                Players[0].ArenaMaxX = WorldW / 2.0 - BasketW - 4;
                Players[0].BasketX = 96;
                Players[1].ArenaMinX = WorldW / 2.0 + 4;
                Players[1].ArenaMaxX = WorldW - BasketW - 4;
                Players[1].BasketX = 336;
                SpawnTimers[0] = 0.72;
                SpawnTimers[1] = 1.08;
            }

            Defeated = 0;
            WinnerPlayer = 0;
            Elapsed = 0;
            LastPowerSpawnP0 = -999;
            LastPowerSpawnP1 = -999;
            ShakeTime = 0;
        }

        private static void ResetPlayer(PlayerState player, Mode selectedMode)
        {
            // Single player runs on five hearts; Duo keeps its classic three.
            player.LivesHalf = selectedMode == Mode.Single ? SingleMaxLivesHalf : DuoMaxLivesHalf;
            player.Ammo = 0;
            player.Score = 0;
            player.Combo = 0;
            player.SpeedTime = 0;
            player.FreezeTime = 0;
            player.ReverseTime = 0;
            player.SabotageTime = 0;
            player.FireCooldown = 0;
            player.CatchTime = -1;
            player.CatchX = 0;
            player.ThrowTime = -1;
            player.PendingThrow = false;
            player.StatusTimer = 0;
            player.StatusText = "";
            player.ShieldCount = 0;
            player.SlowDownTimer = 0;
            player.Slots.Clear();
            player.SelectedSlot = 0;
        }

        public void Update(double rawDt, int playerOneAxis, int playerTwoAxis)
        {
            double dt = Math.Max(0, Math.Min(rawDt, 0.05));
            UpdateParticles(dt);
            UpdateCrackedEggs(dt);
            ShakeTime = Math.Max(0, ShakeTime - dt);
            foreach (PlayerState player in Players)
            {
                player.StatusTimer = Math.Max(0, player.StatusTimer - dt);
                if (player.CatchTime >= 0)
                {
                    player.CatchTime += dt;
                    if (player.CatchTime >= BasketAnimSeconds) player.CatchTime = -1;
                }
                if (player.ThrowTime >= 0) player.ThrowTime += dt;
            }

            if (Phase != Phase.Playing)
            {
                CancelBasketAnimations();
                return;
            }

            bool clockWasRunning = Mode == Mode.Duo && Elapsed < DuoSuddenDeathSeconds;
            Elapsed += dt;
            if (clockWasRunning && SuddenDeath)
            {
                SetStatus(Players[0], "SUDDEN DEATH!", 1.6);
                SetStatus(Players[1], "SUDDEN DEATH!", 1.6);
                ShakeTime = 0.25;
                Emit(EventType.Power, 0);
            }
            UpdateEffectTimers(dt);
            UpdateChickens(dt);
            UpdateBossHazards(dt);
            MovePlayer(Players[0], playerOneAxis, dt);
            if (Mode == Mode.Duo)
            {
                MovePlayer(Players[1], playerTwoAxis, dt);
            }

            if (Mode == Mode.Single)
            {
                UpdateSingleSpawning(dt);
            }
            else
            {
                UpdateDuoSpawning(dt);
            }

            UpdateFallingEggs(dt);
            if (Phase == Phase.Playing && Mode == Mode.Single)
            {
                UpdateShots(dt);
            }

            if (Phase == Phase.Playing)
                UpdateThrows();
            else
                CancelBasketAnimations();

            // A cleared stage is swapped in here, once every list walk is done. A
            // terminal tick (the last heart lost on the same frame) keeps its result.
            if (stageAdvancePending && Phase == Phase.Playing && Mode == Mode.Single)
            {
                stageAdvancePending = false;
                ApplyStageAdvance();
            }
        }

        public void Fire(int playerIndex)
        {
            if (Phase != Phase.Playing || Mode != Mode.Single || playerIndex != 0)
            {
                return;
            }

            PlayerState player = Players[0];
            if (player.Ammo <= 0 || player.FireCooldown > 0 || player.PendingThrow)
            {
                return;
            }

            player.Ammo--;
            player.FireCooldown = 0.22;
            player.ThrowTime = 0;
            player.PendingThrow = true;
        }

        private void UpdateThrows()
        {
            PlayerState player = Players[0];
            if (Mode == Mode.Single && player.PendingThrow && player.ThrowTime >= ThrowReleaseSeconds)
            {
                player.PendingThrow = false;
                Shots.Add(new Shot(player.BasketX + BasketW / 2 - EggW / 2, BasketRimY - EggH));
                SpawnParticles(player.BasketX + BasketW / 2, BasketRimY, Cream, 4, 42);
                SetStatus(player, "THROW!", 0.45);
                Emit(EventType.Throw, 0);
            }
            if (player.ThrowTime >= BasketAnimSeconds) player.ThrowTime = -1;
        }

        private void CancelBasketAnimations()
        {
            foreach (PlayerState player in Players)
            {
                player.CatchTime = -1;
                player.PendingThrow = false;
                player.ThrowTime = -1;
            }
        }

        private void UpdateEffectTimers(double dt)
        {
            int activePlayers = Mode == Mode.Single ? 1 : 2;
            for (int i = 0; i < activePlayers; i++)
            {
                PlayerState player = Players[i];
                player.SpeedTime = Math.Max(0, player.SpeedTime - dt);
                player.FreezeTime = Math.Max(0, player.FreezeTime - dt);
                player.ReverseTime = Math.Max(0, player.ReverseTime - dt);
                player.SabotageTime = Math.Max(0, player.SabotageTime - dt);
                player.SlowDownTimer = Math.Max(0, player.SlowDownTimer - dt);
                player.FireCooldown = Math.Max(0, player.FireCooldown - dt);
            }
        }

        private void MovePlayer(PlayerState player, int rawAxis, double dt)
        {
            int axis = rawAxis;
            if (player.FreezeTime > 0)
            {
                axis = 0;
            }
            else if (player.ReverseTime > 0)
            {
                axis = -axis;
            }

            // Single player has the full width to cover alone, so it moves faster
            // at rest and the speed egg still lands as a clear step up from that.
            double speed = Mode == Mode.Single
                ? (player.SpeedTime > 0 ? SingleBoostSpeed : SingleMoveSpeed)
                : (player.SpeedTime > 0 ? DuoBoostSpeed : DuoMoveSpeed);
            player.BasketX += axis * speed * dt;
            player.BasketX = Clamp(player.BasketX, player.ArenaMinX, player.ArenaMaxX);
        }

        private void UpdateChickens(double dt)
        {
            foreach (Chicken chicken in Chickens)
            {
                chicken.AnimTime += dt;
                chicken.ActionTime = Math.Max(0, chicken.ActionTime - dt);
                if (!chicken.Alive())
                {
                    // Stage 2: a downed chicken counts itself back in, then climbs
                    // back onto its perch at full health.
                    if (chicken.IsRespawning())
                    {
                        chicken.RespawnTimer -= dt;
                        if (chicken.RespawnTimer <= 0)
                        {
                            chicken.RespawnTimer = 0;
                            chicken.Respawn();
                            SetStatus(Players[0], "CHICKEN RESPAWNED!", 1.0);
                            SpawnParticles(chicken.CenterX, chicken.Y + 15, Cyan, 12, 90);
                            Emit(EventType.ChickenDown, 0);
                        }
                    }

                    // A defeated chicken keeps the death clip's last frame for good.
                    continue;
                }

                if (chicken.IsBoss)
                {
                    UpdateBoss(chicken, dt);
                    continue;
                }

                PatrolStep(chicken, dt, ChickenPatrolSpeed(chicken));
                if (chicken.ActionTime <= 0)
                {
                    chicken.Loop(chicken.StandTime > 0 ? AnimState.Idle : AnimState.Walking);
                }
            }
        }

        /// <summary>Walks a chicken along its lane, pausing briefly at each end.</summary>
        private static void PatrolStep(Chicken chicken, double dt, double speed)
        {
            chicken.StandTime = Math.Max(0, chicken.StandTime - dt);
            if (chicken.StandTime > 0)
            {
                return;
            }

            chicken.CenterX += chicken.Direction * speed * dt;
            if (chicken.CenterX < chicken.MinX)
            {
                chicken.CenterX = chicken.MinX + (chicken.MinX - chicken.CenterX);
                chicken.Direction = 1;
                chicken.StandTime = TurnPauseSeconds;
            }
            else if (chicken.CenterX > chicken.MaxX)
            {
                chicken.CenterX = chicken.MaxX - (chicken.CenterX - chicken.MaxX);
                chicken.Direction = -1;
                chicken.StandTime = TurnPauseSeconds;
            }

            chicken.Facing = chicken.Direction;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Boss fight
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Runs the boss's Fly → Bomb → Sleep cycle. Flying, it sweeps its lane
        /// in the air and sends feather strikes at the player; bombing, it
        /// patrols the perch and drops a bomb on the lay frame of each jump;
        /// asleep, it is still and can finally be hit.
        /// </summary>
        private void UpdateBoss(Chicken boss, double dt)
        {
            PlayerState player = Players[0];
            boss.BossPhaseTime += dt;
            boss.BossAttackTimer -= dt;

            switch (boss.BossPhase)
            {
                case BossPhase.Fly:
                {
                    boss.FlyLift = Math.Min(BossFlyLift, boss.FlyLift + BossLiftSpeed * dt);
                    boss.FlyStateTime += dt;
                    if (boss.ActionTime <= 0)
                    {
                        boss.Loop(AnimState.Fly);
                    }

                    UpdateBossFlight(boss, player, dt);

                    if (boss.BossPhaseTime >= BossFlySeconds && boss.FlyState == BossFlyState.Sweeping)
                    {
                        EnterBossPhase(boss, BossPhase.Bomb);
                    }

                    break;
                }

                case BossPhase.Bomb:
                {
                    boss.FlyLift = Math.Max(0, boss.FlyLift - BossLiftSpeed * dt);

                    // Airborne it ranged past its perch; glide back over the lane
                    // before settling into the ground patrol.
                    if (boss.CenterX < boss.MinX)
                    {
                        boss.CenterX = Math.Min(boss.MinX, boss.CenterX + BossFlySpeed * dt);
                        boss.Facing = boss.Direction = 1;
                    }
                    else if (boss.CenterX > boss.MaxX)
                    {
                        boss.CenterX = Math.Max(boss.MaxX, boss.CenterX - BossFlySpeed * dt);
                        boss.Facing = boss.Direction = -1;
                    }
                    else
                    {
                        PatrolStep(boss, dt, ChickenPatrolSpeed(boss));
                    }

                    // Start a jump on the beat; the bomb leaves on the lay frame, so
                    // the drop reads off the animation exactly as a laid egg does.
                    if (boss.BossAttackTimer <= 0 && boss.ActionTime <= 0 && boss.FlyLift <= 0)
                    {
                        boss.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
                        boss.BombPending = true;
                        boss.BossAttackTimer = BombDropInterval;
                    }

                    if (boss.BombPending && boss.Anim == AnimState.Jumping && boss.AnimTime >= BombDropFrameSeconds)
                    {
                        boss.BombPending = false;
                        Bombs.Add(new Bomb(boss.CenterX - BombW / 2.0, boss.DrawY + 24));
                    }

                    if (boss.ActionTime <= 0)
                    {
                        boss.Loop(boss.StandTime > 0 ? AnimState.Idle : AnimState.Walking);
                    }

                    if (boss.BossPhaseTime >= BossBombSeconds)
                    {
                        EnterBossPhase(boss, BossPhase.Sleep);
                    }

                    break;
                }

                case BossPhase.Sleep:
                {
                    boss.FlyLift = Math.Max(0, boss.FlyLift - BossLiftSpeed * dt);
                    // A hit flashes the damage clip over the sleep, then it dozes off again.
                    if (boss.ActionTime <= 0)
                    {
                        boss.Loop(AnimState.Sleeping);
                    }

                    if (boss.BossPhaseTime >= BossSleepSeconds)
                    {
                        EnterBossPhase(boss, BossPhase.Fly);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// The airborne boss cruises the arena, breaks off to chase the basket,
        /// stalls directly over it, and only then lets the flock go — so the
        /// feathers fall straight from a bird that is no longer moving.
        /// </summary>
        private void UpdateBossFlight(Chicken boss, PlayerState player, double dt)
        {
            switch (boss.FlyState)
            {
                case BossFlyState.Sweeping:
                {
                    FlyStep(boss, dt, BossFlySpeed);
                    if (boss.BossAttackTimer <= 0)
                    {
                        boss.FlyState = BossFlyState.Approaching;
                        boss.FlyStateTime = 0;
                    }

                    break;
                }

                case BossFlyState.Approaching:
                {
                    double target = Clamp(player.BasketX + BasketW / 2, BossFlyMinX, BossFlyMaxX);
                    double gap = target - boss.CenterX;
                    double step = BossApproachSpeed * dt;
                    if (Math.Abs(gap) <= step)
                    {
                        boss.CenterX = target;
                    }
                    else
                    {
                        boss.CenterX += Math.Sign(gap) * step;
                        boss.Facing = boss.Direction = Math.Sign(gap);
                    }

                    bool overhead = Math.Abs(target - boss.CenterX) <= BossArriveDistance;
                    if (overhead || boss.FlyStateTime >= BossApproachMaxSeconds)
                    {
                        // Brake, and start gathering the flock right under the body.
                        boss.FlyState = BossFlyState.Hovering;
                        boss.FlyStateTime = 0;
                        Feathers.Add(new FeatherStrike(boss.CenterX, boss.DrawY + FeatherReleaseOffset));
                    }

                    break;
                }

                case BossFlyState.Hovering:
                {
                    // Wings beat double-time while it holds position.
                    boss.AnimTime += dt;
                    if (boss.FlyStateTime >= FeatherWindupSeconds)
                    {
                        // Release: the flock dives on its own now; peel away towards open sky.
                        boss.FlyState = BossFlyState.Sweeping;
                        boss.FlyStateTime = 0;
                        boss.BossAttackTimer = FeatherStrikeInterval;
                        boss.Facing = boss.Direction = boss.CenterX < WorldW / 2.0 ? 1 : -1;
                    }

                    break;
                }
            }
        }

        /// <summary>Cruises the full arena width, turning at the edges without pausing.</summary>
        private static void FlyStep(Chicken boss, double dt, double speed)
        {
            boss.CenterX += boss.Direction * speed * dt;
            if (boss.CenterX < BossFlyMinX)
            {
                boss.CenterX = BossFlyMinX + (BossFlyMinX - boss.CenterX);
                boss.Direction = 1;
            }
            else if (boss.CenterX > BossFlyMaxX)
            {
                boss.CenterX = BossFlyMaxX - (boss.CenterX - BossFlyMaxX);
                boss.Direction = -1;
            }

            boss.Facing = boss.Direction;
        }

        private void EnterBossPhase(Chicken boss, BossPhase phase)
        {
            boss.BossPhase = phase;
            boss.BossPhaseTime = 0;
            boss.BossAttackTimer = BossFirstAttackDelay;
            boss.BombPending = false;
            boss.StandTime = 0;
            boss.FlyState = BossFlyState.Sweeping;
            boss.FlyStateTime = 0;
            boss.SleepHits = 0;

            switch (phase)
            {
                case BossPhase.Fly:
                    SetStatus(Players[0], "IT TAKES FLIGHT!  WATCH ABOVE", 1.4);
                    break;
                case BossPhase.Bomb:
                    SetStatus(Players[0], "BOMBS AWAY!  KEEP YOUR DISTANCE", 1.4);
                    break;
                case BossPhase.Sleep:
                    SetStatus(Players[0], "IT'S ASLEEP  -  THROW NOW!", 1.4);
                    break;
            }
        }

        /// <summary>Advances every bomb and feather strike and applies their damage.</summary>
        private void UpdateBossHazards(double dt)
        {
            PlayerState player = Players[0];
            double basketCenter = player.BasketX + BasketW / 2;

            for (int i = Bombs.Count - 1; i >= 0; i--)
            {
                Bomb bomb = Bombs[i];
                bomb.Age += dt;
                if (!bomb.Landed)
                {
                    bomb.Y += BombFallSpeed * dt;
                    if (bomb.Y + BombH >= BombRestY)
                    {
                        bomb.Y = BombRestY - BombH;
                        bomb.Landed = true;
                    }
                }
                else if (!bomb.Exploded)
                {
                    bomb.FuseTime += dt;
                    if (bomb.FuseTime >= BombFuseSeconds)
                    {
                        bomb.Exploded = true;
                        ShakeTime = 0.2;
                        Emit(EventType.Explosion, 0);
                    }
                }
                else
                {
                    bomb.BlastTime += dt;
                    if (!bomb.Hurt && bomb.BlastTime < BombHurtWindow
                        && Math.Abs(bomb.CenterX - basketCenter) < BombBlastRadius)
                    {
                        bomb.Hurt = true;
                        HurtPlayer(player, "BOOM!  -1/2 HEART");
                    }

                    if (bomb.BlastTime >= BombExplodeSeconds)
                    {
                        Bombs.RemoveAt(i);
                    }
                }
            }

            Rect basket = player.BasketBounds();
            for (int i = Feathers.Count - 1; i >= 0; i--)
            {
                FeatherStrike strike = Feathers[i];
                strike.Time += dt;
                if (!strike.Released && strike.Time >= FeatherWindupSeconds)
                {
                    strike.Released = true;
                    SpawnFeatherWave(strike);
                    Emit(EventType.Feather, 0);
                }

                for (int f = strike.Feathers.Count - 1; f >= 0; f--)
                {
                    Feather feather = strike.Feathers[f];
                    if (!feather.Landed)
                    {
                        feather.Y += feather.Vy * dt;
                        if (!strike.Hurt && feather.Bounds().Overlaps(basket))
                        {
                            strike.Hurt = true;
                            feather.Landed = true;
                            HurtPlayer(player, "FEATHERED!  -1/2 HEART");
                        }
                        else if (feather.Y + feather.Height >= GroundY)
                        {
                            feather.Y = GroundY - feather.Height;
                            feather.Landed = true;
                        }
                    }
                    else
                    {
                        feather.BurstTime += dt;
                        if (feather.BurstTime >= FeatherBurstSeconds)
                        {
                            strike.Feathers.RemoveAt(f);
                        }
                    }
                }

                if (strike.Finished)
                {
                    Feathers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Fills a wave: feathers scattered across the wave's width, each with
        /// its own fall rate and flutter phase so they never move as one. The
        /// first feather is placed dead centre, so a basket that has not moved
        /// since the boss stalled over it is always reached.
        /// </summary>
        private void SpawnFeatherWave(FeatherStrike strike)
        {
            int total = FeathersPerWave + DownPerWave;
            for (int i = 0; i < total; i++)
            {
                bool down = i >= FeathersPerWave;
                int width = down ? DownW : FeatherW;
                double offset = i == 0 ? 0 : -FeatherWaveSpread + Random.NextDouble() * FeatherWaveSpread * 2;
                double x = strike.X - width / 2.0 + offset;
                double y = strike.StartY - 8 + Random.NextDouble() * 16;
                double vy = FeatherFallSpeed * (0.8 + Random.NextDouble() * 0.4);
                double phase = Random.NextDouble() * 0.8;
                strike.Feathers.Add(new Feather(x, y, vy, phase, down));
            }
        }

        /// <summary>
        /// Takes half a heart from a player, or a shield if they have one. Shared
        /// by missed eggs and the boss's attacks so every hit is judged alike.
        /// </summary>
        private void HurtPlayer(PlayerState target, string hitText)
        {
            ShakeTime = Math.Max(ShakeTime, 0.12);

            if (target.ShieldCount > 0)
            {
                target.ShieldCount--;
                SetStatus(target, "SHIELD BLOCKED!", 1.0);
                SpawnParticles(target.BasketX + BasketW / 2, BasketY, Cyan, 16, 100);
                Emit(EventType.Power, target.Index);
                return;
            }

            target.LivesHalf--;
            target.Combo = 0;
            if (target.LivesHalf < 0)
            {
                target.LivesHalf = 0;
            }

            SetStatus(target, hitText, 1.0);

            if (Mode == Mode.Single && target.LivesHalf == 0)
            {
                Phase = Phase.Lost;
                Emit(EventType.Lose, 0);
            }
            else
            {
                Emit(EventType.Miss, target.Index);
            }
        }

        /// <summary>How long a one-shot chicken clip is allowed to run.</summary>
        public static double AnimSeconds(AnimState state)
        {
            switch (state)
            {
                case AnimState.Jumping:
                    return LayAnimSeconds;
                case AnimState.Damage:
                    return DamageAnimSeconds;
                case AnimState.Die:
                    return DieAnimSeconds;
                default:
                    return 0;
            }
        }

        public double ChickenPatrolSpeed(Chicken chicken)
        {
            int tier = Math.Min(5, (int)(Elapsed / 20.0));
            double baseSpeed = Mode == Mode.Single ? 18 + tier * 3 : 14 + tier * 2;
            return baseSpeed * chicken.SpeedScale;
        }

        private void UpdateSingleSpawning(double dt)
        {
            SpawnTimers[0] -= dt;
            if (SpawnTimers[0] > 0)
            {
                return;
            }

            if (!AnyChickenAlive())
            {
                // Stage 2: hold the beat until a downed chicken is back on its perch.
                SpawnTimers[0] = 0.4;
                return;
            }

            SpawnSingleEgg();
            SpawnTimers[0] = SpawnInterval() * (0.88 + Random.NextDouble() * 0.25);
        }

        /// <summary>
        /// True while at least one chicken is on its perch. Stage 2 can have every
        /// chicken down at once, so the spawner asks rather than counting.
        /// </summary>
        private bool AnyChickenAlive()
        {
            foreach (Chicken chicken in Chickens)
            {
                if (chicken.Alive())
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateDuoSpawning(double dt)
        {
            // One clock and one roll for both sides: whatever falls for one
            // player falls for the other at the mirrored spot at the same moment,
            // so the only difference between the two halves is the people.
            SpawnTimers[0] -= dt;
            if (SpawnTimers[0] <= 0)
            {
                SpawnMirroredDuoEggs();
                SpawnTimers[0] = SpawnInterval() * (0.88 + Random.NextDouble() * 0.22);
            }
        }

        /// <summary>
        /// Lays the same egg on both sides. Player one's lane <c>L</c> mirrors
        /// player two's lane <c>1 - L</c>, and the jitter flips sign, so the two
        /// eggs are exact reflections of each other across the divider.
        /// </summary>
        private void SpawnMirroredDuoEggs()
        {
            int lane = Random.Next(2);
            Chicken left = ChickenIn(0, lane);
            Chicken right = ChickenIn(1, 1 - lane);
            if (left == null && right == null)
            {
                return;
            }

            EggKind kind = EggKind.Normal;
            if (Elapsed >= 6
                && Elapsed - LastPowerSpawnP0 >= 5
                && !PowerEggVisible(0) && !PowerEggVisible(1)
                && Random.NextDouble() < 0.30)
            {
                double roll = Random.NextDouble();
                kind = roll < 0.35 ? EggKind.Speed
                     : roll < 0.60 ? EggKind.Freeze
                     : roll < 0.82 ? EggKind.Reverse
                     : EggKind.Golden;
                LastPowerSpawnP0 = Elapsed;
            }

            double jitter = -7 + Random.NextDouble() * 14;
            if (left != null)
            {
                left.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
                FallingEggs.Add(new FallingEgg(kind, 0, left.Lane, left.CenterX - EggW / 2 + jitter, left.Y + 25));
            }

            if (right != null)
            {
                right.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
                FallingEggs.Add(new FallingEgg(kind, 1, right.Lane, right.CenterX - EggW / 2 - jitter, right.Y + 25));
            }
        }

        private Chicken ChickenIn(int owner, int lane)
        {
            foreach (Chicken chicken in Chickens)
            {
                if (chicken.Owner == owner && chicken.Lane == lane && chicken.Alive())
                {
                    return chicken;
                }
            }

            return null;
        }

        public void SpawnSingleEgg()
        {
            List<Chicken> alive = new List<Chicken>();
            foreach (Chicken chicken in Chickens)
            {
                // A sleeping boss lays nothing; the player's window to hit it is
                // also their window to throw what they have banked.
                if (chicken.Alive() && !chicken.BossVulnerable)
                {
                    alive.Add(chicken);
                }
            }

            if (alive.Count == 0)
            {
                return;
            }

            Chicken source = alive[Random.Next(alive.Count)];
            EggKind kind = EggKind.Normal;
            if (Elapsed >= 10
                && Elapsed - LastPowerSpawnP0 >= 12
                && !PowerEggVisible(0)
                && Random.NextDouble() < 0.12)
            {
                double roll = Random.NextDouble();
                if (roll < 0.40)
                {
                    kind = EggKind.Speed;
                }
                else if (roll < 0.70)
                {
                    kind = EggKind.Shield;
                }
                else if (roll < 0.90)
                {
                    kind = EggKind.SlowDown;
                }
                else
                {
                    kind = EggKind.Golden;
                }
                LastPowerSpawnP0 = Elapsed;
            }

            // A flying boss simply lets the egg go; a jump in mid-air would look wrong.
            if (!(source.IsBoss && source.BossPhase == BossPhase.Fly))
            {
                source.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
            }

            double jitter = -8 + Random.NextDouble() * 16;
            FallingEggs.Add(new FallingEgg(kind, 0, source.Lane,
                source.CenterX - EggW / 2 + jitter, source.DrawY + 25));
        }

        public void SpawnDuoEgg(int owner)
        {
            List<Chicken> ownedChickens = new List<Chicken>();
            foreach (Chicken chicken in Chickens)
            {
                if (chicken.Owner == owner && chicken.Alive())
                {
                    ownedChickens.Add(chicken);
                }
            }

            if (ownedChickens.Count == 0)
            {
                return;
            }

            Chicken source = ownedChickens[Random.Next(ownedChickens.Count)];

            EggKind kind = EggKind.Normal;
            // Each player has their own power-egg cooldown so neither side is
            // starved by the other player's spawn events.
            double lastSpawn = owner == 0 ? LastPowerSpawnP0 : LastPowerSpawnP1;
            if (Elapsed >= 6
                && Elapsed - lastSpawn >= 5
                && !PowerEggVisible(owner)
                && Random.NextDouble() < 0.30)
            {
                double roll = Random.NextDouble();
                if (roll < 0.35)
                {
                    kind = EggKind.Speed;
                }
                else if (roll < 0.60)
                {
                    kind = EggKind.Freeze;
                }
                else if (roll < 0.82)
                {
                    kind = EggKind.Reverse;
                }
                else
                {
                    kind = EggKind.Golden;
                }

                if (owner == 0) LastPowerSpawnP0 = Elapsed;
                else            LastPowerSpawnP1 = Elapsed;
            }

            source.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
            double jitter = -7 + Random.NextDouble() * 14;
            FallingEggs.Add(new FallingEgg(kind, owner, source.Lane,
                source.CenterX - EggW / 2 + jitter, source.Y + 25));
        }

        private bool PowerEggVisible(int owner)
        {
            foreach (FallingEgg egg in FallingEggs)
            {
                if (egg.Kind != EggKind.Normal && egg.Owner == owner)
                {
                    return true;
                }
            }

            return false;
        }

        public double BaseFallSpeed()
        {
            int tier = Math.Min(5, (int)(Elapsed / 20.0));
            double speed = ((Mode == Mode.Single ? 52 : 50) + tier * (Mode == Mode.Single ? 8 : 7)) * StagePace();
            return speed * SuddenDeathFactor();
        }

        /// <summary>True once a Duo match has run past its clock.</summary>
        public bool SuddenDeath
        {
            get { return Mode == Mode.Duo && Phase != Phase.Menu && Elapsed >= DuoSuddenDeathSeconds; }
        }

        /// <summary>Seconds until sudden death; zero once it has begun.</summary>
        public double SuddenDeathIn
        {
            get { return Math.Max(0, DuoSuddenDeathSeconds - Elapsed); }
        }

        /// <summary>Duo only: eggs fall harder and harder past the clock so someone breaks.</summary>
        public double SuddenDeathFactor()
        {
            if (!SuddenDeath)
            {
                return 1.0;
            }

            return Math.Min(DuoSuddenDeathMax, DuoSuddenDeathStart + (Elapsed - DuoSuddenDeathSeconds) * DuoSuddenDeathRamp);
        }

        /// <summary>
        /// Later stages lay eggs a little faster and drop them a little harder.
        /// Duo play keeps the original pace, since both players share one screen.
        /// </summary>
        private double StagePace()
        {
            if (Mode != Mode.Single)
            {
                return 1.0;
            }

            switch (CurrentStage)
            {
                case Stage.Stage2:
                    return 1.10;
                case Stage.BossStage:
                    return 1.20;
                default:
                    return 1.0;
            }
        }

        public double FallSpeedFor(FallingEgg egg)
        {
            double speed = BaseFallSpeed();
            if (Mode == Mode.Duo && Players[egg.Owner].SabotageTime > 0)
            {
                speed *= 1.65;
            }

            // Slow-down power-up
            PlayerState owner = Players[egg.Owner];
            if (owner.SlowDownTimer > 0)
            {
                speed *= 0.5;  // Slow eggs by 50%
            }

            return speed;
        }

        public double SpawnInterval()
        {
            int tier = Math.Min(5, (int)(Elapsed / 20.0));
            if (Mode == Mode.Single)
            {
                return Math.Max(0.86, 1.55 - tier * 0.13) / StagePace();
            }

            return Math.Max(0.82, 1.42 - tier * 0.11);
        }

        public int DifficultyTier()
        {
            return 1 + Math.Min(5, (int)(Elapsed / 20.0));
        }

        private void UpdateFallingEggs(double dt)
        {
            // Java iterates front-to-back and removes in place; mirror that order
            // exactly so a losing miss still short-circuits the same way.
            int index = 0;
            while (index < FallingEggs.Count)
            {
                FallingEgg egg = FallingEggs[index];
                egg.Age += dt;
                egg.Y += FallSpeedFor(egg) * dt;
                PlayerState target = Players[egg.Owner];

                if (egg.Bounds().Overlaps(target.BasketBounds()))
                {
                    CatchEgg(egg, target);
                    FallingEggs.RemoveAt(index);
                }
                else if (egg.Y + EggH >= GroundY)
                {
                    MissEgg(egg, target);
                    FallingEggs.RemoveAt(index);
                    if (Mode == Mode.Single && Phase == Phase.Lost)
                    {
                        break;
                    }
                }
                else
                {
                    index++;
                }
            }

            if (Mode == Mode.Duo && Phase == Phase.Playing)
            {
                ResolveDuoResult();
            }

            if (Phase != Phase.Playing)
            {
                FallingEggs.Clear();
                Shots.Clear();
                Bombs.Clear();
                Feathers.Clear();
            }
        }

        private void CatchEgg(FallingEgg egg, PlayerState catcher)
        {
            catcher.CatchTime = 0;
            catcher.CatchX = egg.X + EggW / 2;
            int points;
            switch (egg.Kind)
            {
                case EggKind.Normal:
                {
                    if (Mode == Mode.Single)
                    {
                        catcher.Ammo++;
                    }

                    points = AddComboScore(catcher, 10);
                    SetStatus(catcher, "CAUGHT  +" + points, 0.7);
                    SpawnParticles(egg.X, egg.Y, Cream, 6, 55);
                    Emit(EventType.Catch, catcher.Index);
                    break;
                }

                case EggKind.Speed:
                case EggKind.Freeze:
                case EggKind.Reverse:
                case EggKind.Golden:
                {
                    points = AddComboScore(catcher, PowerPoints(egg.Kind));
                    SpawnParticles(egg.X, egg.Y, ColorFor(egg.Kind), 10, 80);
                    Emit(EventType.Power, catcher.Index);
                    if (Mode == Mode.Duo)
                    {
                        // Banked, not fired: the player picks the moment.
                        BankPower(catcher, egg.Kind);
                        SetStatus(catcher, PowerName(egg.Kind) + " READY  +" + points, 1.0);
                    }
                    else
                    {
                        ApplyPower(egg.Kind, catcher);
                        SetStatus(catcher, PowerShout(egg.Kind) + "  +" + points, 1.0);
                    }

                    break;
                }

                case EggKind.Shield:
                {
                    catcher.ShieldCount = Math.Min(catcher.ShieldCount + 1, 3);
                    points = AddComboScore(catcher, 25);
                    SetStatus(catcher, "SHIELD!  +" + points, 1.0);
                    SpawnParticles(egg.X, egg.Y, Cyan, 12, 85);
                    Emit(EventType.Power, catcher.Index);
                    break;
                }

                case EggKind.SlowDown:
                {
                    catcher.SlowDownTimer = 5.0;
                    points = AddComboScore(catcher, 35);
                    SetStatus(catcher, "SLOW DOWN!  +" + points, 1.0);
                    SpawnParticles(egg.X, egg.Y, Ice, 14, 90);
                    Emit(EventType.Power, catcher.Index);
                    break;
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Duo power-ups
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Puts a caught power egg in a slot; a full bank swaps out the selected one.</summary>
        private static void BankPower(PlayerState player, EggKind kind)
        {
            if (player.Slots.Count < DuoSlotCount)
            {
                player.Slots.Add(kind);
                player.SelectedSlot = player.Slots.Count - 1;
            }
            else
            {
                player.Slots[player.SelectedSlot] = kind;
            }
        }

        /// <summary>Moves a Duo player's selection through their banked eggs.</summary>
        public void CycleSlot(int playerIndex, int direction)
        {
            if (Phase != Phase.Playing || Mode != Mode.Duo)
            {
                return;
            }

            PlayerState player = Players[playerIndex];
            if (player.Slots.Count > 1)
            {
                player.SelectedSlot = ((player.SelectedSlot + direction) % player.Slots.Count + player.Slots.Count) % player.Slots.Count;
            }
        }

        /// <summary>Fires a Duo player's selected banked egg right now.</summary>
        public void Deploy(int playerIndex)
        {
            if (Phase != Phase.Playing || Mode != Mode.Duo)
            {
                return;
            }

            PlayerState player = Players[playerIndex];
            if (player.Slots.Count == 0)
            {
                return;
            }

            EggKind kind = player.Slots[player.SelectedSlot];
            player.Slots.RemoveAt(player.SelectedSlot);
            player.SelectedSlot = Math.Min(player.SelectedSlot, Math.Max(0, player.Slots.Count - 1));
            ApplyPower(kind, player);
            SetStatus(player, PowerShout(kind), 1.0);
            SpawnParticles(player.BasketX + BasketW / 2, BasketRimY, ColorFor(kind), 12, 85);
            Emit(EventType.Power, playerIndex);
        }

        /// <summary>The effect of a power egg, whoever triggers it and whenever.</summary>
        private void ApplyPower(EggKind kind, PlayerState user)
        {
            PlayerState opponent = Players[1 - user.Index];
            switch (kind)
            {
                case EggKind.Speed:
                    user.SpeedTime = Mode == Mode.Single ? 5.0 : 4.0;
                    break;
                case EggKind.Freeze:
                    opponent.FreezeTime = 2.0;
                    SetStatus(opponent, "FROZEN", 1.0);
                    break;
                case EggKind.Reverse:
                    opponent.ReverseTime = 3.0;
                    SetStatus(opponent, "CONTROLS REVERSED", 1.1);
                    break;
                case EggKind.Golden:
                    opponent.SabotageTime = 5.0;
                    opponent.Combo = 0;
                    SetStatus(opponent, "EGG STORM!", 1.1);
                    break;
            }
        }

        private static int PowerPoints(EggKind kind)
        {
            return kind == EggKind.Golden ? 50 : kind == EggKind.Speed ? 25 : 30;
        }

        private static string PowerName(EggKind kind)
        {
            return kind == EggKind.Golden ? "GOLD" : kind.ToString().ToUpperInvariant();
        }

        private static string PowerShout(EggKind kind)
        {
            switch (kind)
            {
                case EggKind.Speed: return "SPEED UP!";
                case EggKind.Freeze: return "FREEZE!";
                case EggKind.Reverse: return "REVERSE!";
                default: return "GOLD RUSH!";
            }
        }

        private void MissEgg(FallingEgg egg, PlayerState target)
        {
            SpawnCrackedEgg(egg);
            ShakeTime = 0.12;

            if (egg.Kind != EggKind.Normal)
            {
                SetStatus(target, "POWER MISSED - SAFE", 0.8);
                return;
            }

            HurtPlayer(target, "CRACK!  -1/2 HEART");
        }

        private void ResolveDuoResult()
        {
            bool playerOneOut = Players[0].LivesHalf <= 0;
            bool playerTwoOut = Players[1].LivesHalf <= 0;
            if (!playerOneOut && !playerTwoOut)
            {
                return;
            }

            if (playerOneOut && playerTwoOut)
            {
                if (Players[0].Score > Players[1].Score)
                {
                    WinnerPlayer = 1;
                }
                else if (Players[1].Score > Players[0].Score)
                {
                    WinnerPlayer = 2;
                }
                else
                {
                    WinnerPlayer = 0;
                }
            }
            else
            {
                WinnerPlayer = playerOneOut ? 2 : 1;
            }

            Phase = Phase.Won;
            Emit(EventType.Win, WinnerPlayer == 0 ? -1 : WinnerPlayer - 1);
        }

        private void UpdateShots(double dt)
        {
            int index = 0;
            while (index < Shots.Count)
            {
                Shot shot = Shots[index];
                shot.Age += dt;
                shot.Y -= 220 * dt;
                Chicken hitChicken = null;
                Chicken shruggedBy = null;
                foreach (Chicken chicken in Chickens)
                {
                    if (!chicken.Alive() || !shot.Bounds().Overlaps(chicken.Bounds()))
                    {
                        continue;
                    }

                    // An awake boss is armoured: the egg breaks on it and does nothing.
                    if (chicken.IsBoss && !chicken.BossVulnerable)
                    {
                        shruggedBy = chicken;
                    }
                    else
                    {
                        hitChicken = chicken;
                    }

                    break;
                }

                if (hitChicken != null)
                {
                    ApplyChickenHit(hitChicken);
                    Shots.RemoveAt(index);
                    if (Phase == Phase.Won)
                    {
                        break;
                    }
                }
                else if (shruggedBy != null)
                {
                    SpawnParticles(shot.X + EggW / 2, shot.Y + EggH / 2, Cream, 6, 60);
                    SetStatus(Players[0], "NO EFFECT  -  WAIT FOR IT TO SLEEP", 0.9);
                    Shots.RemoveAt(index);
                }
                else if (shot.Y + EggH < 0)
                {
                    Players[0].Combo = 0;
                    SetStatus(Players[0], "THROW MISSED", 0.75);
                    Shots.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }

            if (Phase == Phase.Won)
            {
                FallingEggs.Clear();
                Shots.Clear();
                Bombs.Clear();
                Feathers.Clear();
            }
        }

        public void ApplyChickenHit(Chicken chicken)
        {
            PlayerState player = Players[0];
            chicken.Hp--;
            int points = AddComboScore(player, 50);
            ShakeTime = 0.1;
            SpawnParticles(chicken.CenterX, chicken.Y + 15, Gold, 10, 82);

            if (chicken.Hp <= 0)
            {
                chicken.Hp = 0;
                chicken.PlayOnce(AnimState.Die, DieAnimSeconds, true);
                Defeated++;
                player.Score += 200;
                SpawnParticles(chicken.CenterX, chicken.Y + 15, Pink, 18, 110);
                if (chicken.CanRespawn)
                {
                    chicken.RespawnTimer = chicken.RespawnTime;
                }

                if (Mode == Mode.Single && Defeated >= StageGoal)
                {
                    // Goal met: Update swaps the next stage in once every list
                    // walk for this tick has finished.
                    stageAdvancePending = true;
                    SetStatus(player, "STAGE CLEAR!", 1.2);
                    Emit(EventType.ChickenDown, 0);
                }
                else
                {
                    SetStatus(player, "CHICKEN DOWN!  +" + (points + 200), 1.1);
                    Emit(EventType.ChickenDown, 0);
                }
            }
            else
            {
                // The flash plays over the patrol so a hit never freezes the target.
                chicken.PlayOnce(AnimState.Damage, DamageAnimSeconds, false);
                Emit(EventType.Hit, 0);

                // The boss only takes so much in one nap: a second hit wakes it,
                // so stockpiled eggs can't finish it in a single sleep.
                if (chicken.IsBoss && chicken.BossPhase == BossPhase.Sleep)
                {
                    chicken.SleepHits++;
                    if (chicken.SleepHits >= BossHitsPerSleep)
                    {
                        EnterBossPhase(chicken, BossPhase.Fly);
                        SetStatus(player, "IT WOKE UP!  +" + points, 1.2);
                        return;
                    }
                }

                SetStatus(player, "DIRECT HIT  +" + points, 0.7);
            }
        }

        private int AddComboScore(PlayerState player, int basePoints)
        {
            player.Combo++;
            int points = basePoints * player.Multiplier();
            player.Score += points;

            if (player.Combo == 10)
            {
                SetStatus(player, "FEVER MODE!  x3 SCORE", 1.35);
                ShakeTime = 0.18;
                Emit(EventType.Power, player.Index);
            }

            return points;
        }

        private void UpdateParticles(double dt)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                Particle particle = Particles[i];
                particle.Life -= dt;
                if (particle.Life <= 0)
                {
                    Particles.RemoveAt(i);
                    continue;
                }

                particle.X += particle.Vx * dt;
                particle.Y += particle.Vy * dt;
                particle.Vy += 95 * dt;
            }
        }

        /// <summary>
        /// Ages and removes the cracked shells left by eggs that hit the ground.
        /// </summary>
        private void UpdateCrackedEggs(double dt)
        {
            for (int i = CrackedEggs.Count - 1; i >= 0; i--)
            {
                CrackedEgg egg = CrackedEggs[i];
                egg.Life -= dt;
                if (egg.Life <= 0)
                {
                    CrackedEggs.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Leaves a cracked shell on the ground where an egg smashed, keeping the
        /// color of the egg that broke so the source of the miss stays readable.
        /// </summary>
        private void SpawnCrackedEgg(FallingEgg egg)
        {
            CrackedEggs.Add(new CrackedEgg(egg.Kind, egg.X, GroundY - EggH, 1.1));
        }

        private void SpawnParticles(double x, double y, Color32 color, int count, double strength)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = Random.NextDouble() * Math.PI * 2;
                double speed = strength * (0.35 + Random.NextDouble() * 0.65);
                Particles.Add(new Particle(
                    x,
                    y,
                    Math.Cos(angle) * speed,
                    Math.Sin(angle) * speed - 18,
                    0.35 + Random.NextDouble() * 0.35,
                    1 + Random.Next(3),
                    color));
            }
        }

        private static void SetStatus(PlayerState player, string text, double seconds)
        {
            player.StatusText = text;
            player.StatusTimer = seconds;
        }

        private void Emit(EventType type, int player)
        {
            Events.Add(new GameEvent(type, player));
        }

        public List<GameEvent> DrainEvents()
        {
            List<GameEvent> drained = new List<GameEvent>(Events);
            Events.Clear();
            return drained;
        }

        public static Color32 ColorFor(EggKind kind)
        {
            switch (kind)
            {
                case EggKind.Speed:
                    return Cyan;
                case EggKind.Freeze:
                    return Ice;
                case EggKind.Reverse:
                    return Purple;
                case EggKind.Golden:
                    return Gold;
                case EggKind.Shield:
                    return Mint;
                case EggKind.SlowDown:
                    return Ice;
                default:
                    return Cream;
            }
        }

        private static double Clamp(double value, double min, double max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        public void ConfigurePreview(Mode previewMode)
        {
            StartRound(previewMode, Stage.Stage1);
            Elapsed = 26;
            SpawnTimers[0] = 99;
            SpawnTimers[1] = 99;
            if (previewMode == Mode.Single)
            {
                PlayerState player = Players[0];
                player.BasketX = 218;
                player.LivesHalf = 7;
                player.Ammo = 4;
                player.Score = 1840;
                player.Combo = 8;
                player.SpeedTime = 3.6;
                Chickens[0].Hp = 2;
                Chickens[1].Hp = 4;
                Chickens[2].Hp = 1;
                FallingEggs.Add(new FallingEgg(EggKind.Normal, 0, 0, 94, 124));
                FallingEggs.Add(new FallingEgg(EggKind.Speed, 0, 1, 237, 154));
                FallingEggs.Add(new FallingEgg(EggKind.Normal, 0, 2, 380, 102));
                Shots.Add(new Shot(330, 167));
                SetStatus(player, "SPEED BOOST!", 4.0);
                SpawnParticles(244, 205, Cyan, 8, 55);
            }
            else
            {
                PlayerState one = Players[0];
                PlayerState two = Players[1];
                one.LivesHalf = 5;
                one.Score = 390;
                one.Combo = 6;
                one.SpeedTime = 2.8;
                two.LivesHalf = 4;
                two.Score = 470;
                two.Combo = 3;
                two.ReverseTime = 2.2;
                FallingEggs.Add(new FallingEgg(EggKind.Normal, 0, 0, 62, 130));
                FallingEggs.Add(new FallingEgg(EggKind.Freeze, 0, 1, 174, 166));
                FallingEggs.Add(new FallingEgg(EggKind.Golden, 1, 0, 302, 116));
                FallingEggs.Add(new FallingEgg(EggKind.Reverse, 1, 1, 412, 151));
                SetStatus(one, "SPEED UP!", 4.0);
                SetStatus(two, "CONTROLS REVERSED", 4.0);
            }
        }
    }
}
