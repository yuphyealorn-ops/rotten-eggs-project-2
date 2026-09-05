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
        Golden
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
        public const double BasketY = 218;
        public const double BasketW = 48;
        public const double BasketH = 15;
        public const double EggW = 7;
        public const double EggH = 9;

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
            public double StatusTimer;
            public string StatusText = "";

            public PlayerState(int index)
            {
                Index = index;
            }

            public Rect BasketBounds()
            {
                return new Rect((float)BasketX, (float)BasketY, (float)BasketW, (float)BasketH);
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

            public Chicken(
                int owner,
                int lane,
                double centerX,
                double y,
                double minX,
                double maxX,
                double speedScale,
                int direction)
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
            }

            public bool Alive()
            {
                return Hp > 0;
            }

            public Rect Bounds()
            {
                return new Rect((float)(CenterX - 18), (float)(Y + 4), 36f, 36f);
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
        }

        public sealed class FallingEgg
        {
            public readonly EggKind Kind;
            public readonly int Owner;
            public readonly int SourceLane;
            public double X;
            public double Y;

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

        public sealed class Shot
        {
            public double X;
            public double Y;

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
        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public readonly double[] SpawnTimers = new double[2];

        public Phase Phase = Phase.Menu;
        public Mode Mode = Mode.Single;
        public int Defeated;
        public int WinnerPlayer;
        public double Elapsed;
        public double LastPowerSpawn;
        public double ShakeTime;

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
            ResetRoundData(selectedMode);
            Phase = Phase.Playing;
            if (Mode == Mode.Single)
            {
                SetStatus(Players[0], "CATCH. AIM AHEAD. THROW.", 1.8);
            }
            else
            {
                SetStatus(Players[0], "P1 READY", 1.2);
                SetStatus(Players[1], "P2 READY", 1.2);
            }
        }

        public void RestartCurrentMode()
        {
            StartRound(Mode);
        }

        public void ReturnToMenu()
        {
            ResetRoundData(Mode.Single);
            Phase = Phase.Menu;
        }

        private void ResetRoundData(Mode selectedMode)
        {
            Mode = selectedMode;
            Chickens.Clear();
            if (Mode == Mode.Single)
            {
                Chickens.Add(new Chicken(0, 0, 98, 48, 48, 140, 0.86, 1));
                Chickens.Add(new Chicken(0, 1, 240, 42, 194, 286, 1.05, -1));
                Chickens.Add(new Chicken(0, 2, 382, 48, 340, 432, 0.94, 1));
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
            Events.Clear();

            ResetPlayer(Players[0]);
            ResetPlayer(Players[1]);
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
            LastPowerSpawn = -999;
            ShakeTime = 0;
        }

        private static void ResetPlayer(PlayerState player)
        {
            player.LivesHalf = 6;
            player.Ammo = 0;
            player.Score = 0;
            player.Combo = 0;
            player.SpeedTime = 0;
            player.FreezeTime = 0;
            player.ReverseTime = 0;
            player.SabotageTime = 0;
            player.FireCooldown = 0;
            player.StatusTimer = 0;
            player.StatusText = "";
        }

        public void Update(double rawDt, int playerOneAxis, int playerTwoAxis)
        {
            double dt = Math.Max(0, Math.Min(rawDt, 0.05));
            UpdateParticles(dt);
            ShakeTime = Math.Max(0, ShakeTime - dt);
            foreach (PlayerState player in Players)
            {
                player.StatusTimer = Math.Max(0, player.StatusTimer - dt);
            }

            if (Phase != Phase.Playing)
            {
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
        }

        public void Fire(int playerIndex)
        {
            if (Phase != Phase.Playing || Mode != Mode.Single || playerIndex != 0)
            {
                return;
            }

            PlayerState player = Players[0];
            if (player.Ammo <= 0 || player.FireCooldown > 0)
            {
                return;
            }

            player.Ammo--;
            player.FireCooldown = 0.22;
            Shots.Add(new Shot(player.BasketX + BasketW / 2 - EggW / 2, BasketY - EggH));
            SpawnParticles(player.BasketX + BasketW / 2, BasketY, Cream, 4, 42);
            SetStatus(player, "THROW!", 0.45);
            Emit(EventType.Throw, 0);
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
            if (SpawnTimers[0] <= 0 && Defeated < Chickens.Count)
            {
                SpawnSingleEgg();
                SpawnTimers[0] = SpawnInterval() * (0.88 + Random.NextDouble() * 0.25);
            }
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
                && Elapsed - LastPowerSpawn >= 12
                && !PowerEggVisible()
                && Random.NextDouble() < 0.12)
            {
                kind = EggKind.Speed;
                LastPowerSpawn = Elapsed;
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
            if (Elapsed >= 8
                && Elapsed - LastPowerSpawn >= 7
                && !PowerEggVisible()
                && Random.NextDouble() < 0.22)
            {
                double roll = Random.NextDouble();
                if (roll < 0.40)
                {
                    kind = EggKind.Speed;
                }
                else if (roll < 0.65)
                {
                    kind = EggKind.Freeze;
                }
                else if (roll < 0.87)
                {
                    kind = EggKind.Reverse;
                }
                else
                {
                    kind = EggKind.Golden;
                }

                LastPowerSpawn = Elapsed;
            }

            source.PlayOnce(AnimState.Jumping, LayAnimSeconds, true);
            double jitter = -7 + Random.NextDouble() * 14;
            FallingEggs.Add(new FallingEgg(kind, owner, source.Lane,
                source.CenterX - EggW / 2 + jitter, source.Y + 25));
        }

        private bool PowerEggVisible()
        {
            foreach (FallingEgg egg in FallingEggs)
            {
                if (egg.Kind != EggKind.Normal)
                {
                    return true;
                }
            }

            return false;
        }

        public double BaseFallSpeed()
        {
            int tier = Math.Min(5, (int)(Elapsed / 20.0));
            return (Mode == Mode.Single ? 52 : 50) + tier * (Mode == Mode.Single ? 8 : 7);
        }

        public double FallSpeedFor(FallingEgg egg)
        {
            double speed = BaseFallSpeed();
            if (Mode == Mode.Duo && Players[egg.Owner].SabotageTime > 0)
            {
                speed *= 1.65;
            }

            return speed;
        }

        public double SpawnInterval()
        {
            int tier = Math.Min(5, (int)(Elapsed / 20.0));
            if (Mode == Mode.Single)
            {
                return Math.Max(0.86, 1.55 - tier * 0.13);
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
                egg.Y += FallSpeedFor(egg) * dt;
                PlayerState target = Players[egg.Owner];

                if (egg.Bounds().Overlaps(target.BasketBounds()))
                {
                    CatchEgg(egg, target);
                    FallingEggs.RemoveAt(index);
                }
                else if (egg.Y > GroundY + 4)
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
            }
        }

        private void MissEgg(FallingEgg egg, PlayerState target)
        {
            Color32 color = ColorFor(egg.Kind);
            SpawnParticles(egg.X, GroundY - 2, color, 10, 95);
            ShakeTime = 0.12;

            if (egg.Kind != EggKind.Normal)
            {
                SetStatus(target, "POWER MISSED - SAFE", 0.8);
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
                if (Defeated == Chickens.Count)
                {
                    Phase = Phase.Won;
                    WinnerPlayer = 1;
                    SetStatus(player, "COOP CLEARED!", 2.0);
                    Emit(EventType.Win, 0);
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

        private static int AddComboScore(PlayerState player, int basePoints)
        {
            player.Combo++;
            int points = basePoints * player.Multiplier();
            player.Score += points;
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
            StartRound(previewMode);
            Elapsed = 26;
            SpawnTimers[0] = 99;
            SpawnTimers[1] = 99;
            if (previewMode == Mode.Single)
            {
                PlayerState player = Players[0];
                player.BasketX = 218;
                player.LivesHalf = 5;
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
