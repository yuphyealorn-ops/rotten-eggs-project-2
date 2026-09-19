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
        Die
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
        Lose
    }

    public static class AnimStates
    {
        public static readonly AnimState[] All =
        {
            AnimState.Idle,
            AnimState.Walking,
            AnimState.Jumping,
            AnimState.Damage,
            AnimState.Die
        };

        /// <summary>Idle and walking repeat because their condition keeps being true.</summary>
        public static bool Loops(this AnimState state)
        {
            return state == AnimState.Idle || state == AnimState.Walking;
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

        /// <summary>Chicken artwork is drawn from its top-left, with the feet 40px down.</summary>
        public const double ChickenH = 40;

        /// <summary>Wooden perch every chicken stands on, so no chicken floats in the air.</summary>
        public const double PerchH = 7;
        public const double PerchMargin = 26;

        /// <summary>One-shot clip lengths, matched to the bundled sheets at 0.1s per frame.</summary>
        public const double LayAnimSeconds = 0.60;
        public const double DamageAnimSeconds = 0.60;
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
                // The boss is drawn at a larger scale, so its hit box grows with it.
                double size = IsBoss ? 54 : 36;
                return new Rect((float)(CenterX - size / 2), (float)(Y + 4), (float)size, (float)size);
            }

            /// <summary>Starts a clip that runs once, optionally rooting the chicken.</summary>
            public void PlayOnce(AnimState state, double seconds, bool stopWalking)
            {
                Anim = state;
                AnimTime = 0;
                ActionTime = seconds;
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
                    // back onto its perch after five seconds until the goal is met.
                    Chickens.Add(new Chicken(0, 0, 98, 48, 48, 140, 0.86, 1, true, 5.0));
                    Chickens.Add(new Chicken(0, 1, 240, 42, 194, 286, 1.05, -1, true, 5.0));
                    Chickens.Add(new Chicken(0, 2, 382, 48, 340, 432, 0.94, 1, true, 5.0));
                }
                else // BossStage
                {
                    // Boss Stage: one oversized boss, eight hits, slow heavy patrol.
                    Chickens.Add(new Chicken(0, 0, 240, 64, 120, 340, 0.55, 1, false, 0.0, true));
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

            Elapsed += dt;
            UpdateEffectTimers(dt);
            UpdateChickens(dt);
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
                player.FireCooldown = Math.Max(0, player.FireCooldown - dt);
            }
        }

        private static void MovePlayer(PlayerState player, int rawAxis, double dt)
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

            double speed = player.SpeedTime > 0 ? 220 : 145;
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

                chicken.StandTime = Math.Max(0, chicken.StandTime - dt);
                if (chicken.StandTime <= 0)
                {
                    chicken.CenterX += chicken.Direction * ChickenPatrolSpeed(chicken) * dt;
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

                if (chicken.ActionTime <= 0)
                {
                    chicken.Loop(chicken.StandTime > 0 ? AnimState.Idle : AnimState.Walking);
                }
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
            for (int owner = 0; owner < 2; owner++)
            {
                SpawnTimers[owner] -= dt;
                if (SpawnTimers[owner] <= 0)
                {
                    SpawnDuoEgg(owner);
                    SpawnTimers[owner] = SpawnInterval() * (0.88 + Random.NextDouble() * 0.22);
                }
            }
        }

        public void SpawnSingleEgg()
        {
            List<Chicken> alive = new List<Chicken>();
            foreach (Chicken chicken in Chickens)
            {
                if (chicken.Alive())
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

            source.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
            double jitter = -8 + Random.NextDouble() * 16;
            FallingEggs.Add(new FallingEgg(kind, 0, source.Lane,
                source.CenterX - EggW / 2 + jitter, source.Y + 25));
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
            return ((Mode == Mode.Single ? 52 : 50) + tier * (Mode == Mode.Single ? 8 : 7)) * StagePace();
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
                {
                    catcher.SpeedTime = Mode == Mode.Single ? 5.0 : 4.0;
                    points = AddComboScore(catcher, 25);
                    SetStatus(catcher, "SPEED UP!  +" + points, 1.1);
                    SpawnParticles(egg.X, egg.Y, Cyan, 9, 76);
                    Emit(EventType.Power, catcher.Index);
                    break;
                }

                case EggKind.Freeze:
                {
                    PlayerState opponent = Players[1 - catcher.Index];
                    opponent.FreezeTime = 2.0;
                    points = AddComboScore(catcher, 30);
                    SetStatus(catcher, "FREEZE!  +" + points, 1.0);
                    SetStatus(opponent, "FROZEN", 1.0);
                    SpawnParticles(egg.X, egg.Y, Ice, 10, 78);
                    Emit(EventType.Power, catcher.Index);
                    break;
                }

                case EggKind.Reverse:
                {
                    PlayerState opponent = Players[1 - catcher.Index];
                    opponent.ReverseTime = 3.0;
                    points = AddComboScore(catcher, 30);
                    SetStatus(catcher, "REVERSE!  +" + points, 1.0);
                    SetStatus(opponent, "CONTROLS REVERSED", 1.1);
                    SpawnParticles(egg.X, egg.Y, Purple, 10, 78);
                    Emit(EventType.Power, catcher.Index);
                    break;
                }

                case EggKind.Golden:
                {
                    PlayerState opponent = Players[1 - catcher.Index];
                    opponent.SabotageTime = 5.0;
                    opponent.Combo = 0;
                    points = AddComboScore(catcher, 50);
                    SetStatus(catcher, "GOLD RUSH!  +" + points, 1.0);
                    SetStatus(opponent, "EGG STORM!", 1.1);
                    SpawnParticles(egg.X, egg.Y, Gold, 14, 90);
                    Emit(EventType.Power, catcher.Index);
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

        private void MissEgg(FallingEgg egg, PlayerState target)
        {
            SpawnCrackedEgg(egg);
            ShakeTime = 0.12;

            if (egg.Kind != EggKind.Normal)
            {
                SetStatus(target, "POWER MISSED - SAFE", 0.8);
                return;
            }

            // Shield absorbs one hit
            if (target.ShieldCount > 0)
            {
                target.ShieldCount--;
                SetStatus(target, "SHIELD BLOCKED!", 1.0);
                SpawnParticles(target.BasketX + GameModel.BasketW / 2, GameModel.BasketY, Cyan, 16, 100);
                Emit(EventType.Power, target.Index);
                return;
            }

            target.LivesHalf--;
            target.Combo = 0;
            if (target.LivesHalf < 0)
            {
                target.LivesHalf = 0;
            }

            SetStatus(target, "CRACK!  -1/2 HEART", 1.0);

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
                foreach (Chicken chicken in Chickens)
                {
                    if (chicken.Alive() && shot.Bounds().Overlaps(chicken.Bounds()))
                    {
                        hitChicken = chicken;
                        break;
                    }
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
                SetStatus(player, "DIRECT HIT  +" + points, 0.7);
                Emit(EventType.Hit, 0);
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
