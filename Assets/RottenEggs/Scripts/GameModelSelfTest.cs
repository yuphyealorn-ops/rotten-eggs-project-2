using System;
using System.Linq;

namespace RottenEggs
{
    /// <summary>
    /// The Java prototype's deterministic rule checks, ported alongside the
    /// rules themselves. Running these is how the port is proved faithful:
    /// every assertion here is the same assertion the Java build ran, so a
    /// passing run means the Unity model behaves like the original.
    /// </summary>
    public static class GameModelSelfTest
    {
        public static string Run()
        {
            int checks = 0;
            GameModel model = new GameModel(new Random(7));
            model.StartRound(Mode.Single);
            model.SpawnTimers[0] = 999;

            Require(model.Mode == Mode.Single && model.Chickens.Count == 3,
                "single mode must start with three chickens");
            Require(model.Chickens.All(chicken => chicken.Hp == 4),
                "each chicken must start with four HP");
            Require(model.Player(0).LivesHalf == 6 && model.Player(0).Ammo == 0,
                "single mode must start with three hearts and empty ammo");
            checks += 3;

            GameModel.Chicken movingFirst = model.Chickens[0];
            GameModel.Chicken movingSecond = model.Chickens[1];
            double firstStartX = movingFirst.CenterX;
            double secondStartX = movingSecond.CenterX;
            model.Update(0.05, 0, 0);
            Require(movingFirst.CenterX > firstStartX && movingSecond.CenterX < secondStartX,
                "alive Single chickens must patrol in their configured directions");
            checks++;

            double bounceSpeed = model.ChickenPatrolSpeed(movingFirst);
            movingFirst.CenterX = movingFirst.MaxX - 0.20;
            movingFirst.Direction = 1;
            double expectedBounceX = movingFirst.MaxX - (bounceSpeed * 0.05 - 0.20);
            model.Update(0.05, 0, 0);
            Require(movingFirst.Direction == -1 && Math.Abs(movingFirst.CenterX - expectedBounceX) < 0.000001,
                "chickens must reflect smoothly when they reach a patrol edge");
            Require(movingFirst.Anim == AnimState.Idle && movingFirst.Facing == -1,
                "a chicken that turns around must stand on the idle clip facing its new way");
            checks += 2;

            PlaceCatch(model, 0, EggKind.Normal);
            model.Update(0.01, 0, 0);
            Require(model.Player(0).Ammo == 1, "single normal catch must add one ammo");
            Require(model.Player(0).Score == 10 && model.Player(0).Combo == 1,
                "single normal catch must update score and combo");
            checks += 2;

            model.Player(0).FireCooldown = 0;
            model.Fire(0);
            Require(model.Player(0).Ammo == 0 && model.Shots.Count == 1,
                "single throw must consume exactly one ammo");
            checks++;

            model.Shots.Clear();
            GameModel.Chicken first = model.Chickens[0];
            for (int i = 0; i < 4; i++)
            {
                model.Shots.Add(new GameModel.Shot(first.CenterX - GameModel.EggW / 2, first.Y + 10));
                model.Update(0, 0, 0);
            }

            Require(first.Hp == 0 && model.Defeated == 1, "four hits must defeat one chicken");
            checks++;

            double defeatedPosition = first.CenterX;
            int defeatedDirection = first.Direction;
            model.Update(0.05, 0, 0);
            Require(first.CenterX == defeatedPosition && first.Direction == defeatedDirection,
                "defeated chickens and their KO nests must stop moving");
            Require(first.Anim == AnimState.Die && first.AnimTime > 0,
                "a defeated chicken must keep running out the death clip once");
            checks += 2;

            double damagedFrom = movingSecond.CenterX;
            model.ApplyChickenHit(movingSecond);
            Require(movingSecond.Anim == AnimState.Damage && movingSecond.StandTime <= 0,
                "a survivable hit must flash the damage clip without rooting the chicken");
            model.Update(0.05, 0, 0);
            Require(movingSecond.CenterX != damagedFrom && movingSecond.Anim == AnimState.Damage,
                "the damage clip must keep playing while the chicken patrols on");
            checks += 2;

            int lifeBefore = model.Player(0).LivesHalf;
            PlaceMiss(model, 0, EggKind.Speed, 10);
            model.Update(0, 0, 0);
            Require(model.Player(0).LivesHalf == lifeBefore, "a missed speed egg must be harmless");
            checks++;

            PlaceMiss(model, 0, EggKind.Normal, 20);
            model.Update(0, 0, 0);
            Require(model.Player(0).LivesHalf == lifeBefore - 1, "a normal miss must cost half a heart");
            Require(model.Player(0).Combo == 0, "a normal miss must reset only that player's combo");
            checks += 2;

            PlaceCatch(model, 0, EggKind.Speed);
            model.Update(0.01, 0, 0);
            Require(model.Player(0).SpeedTime > 4.9, "single speed egg must give a five-second boost");
            checks++;

            model.Elapsed = 0;
            double startingSpeed = model.BaseFallSpeed();
            double startingInterval = model.SpawnInterval();
            double startingPatrolSpeed = model.ChickenPatrolSpeed(model.Chickens[1]);
            model.Elapsed = 41;
            Require(model.BaseFallSpeed() > startingSpeed, "difficulty must increase falling speed");
            Require(model.SpawnInterval() < startingInterval, "difficulty must shorten spawn intervals");
            Require(model.ChickenPatrolSpeed(model.Chickens[1]) > startingPatrolSpeed,
                "difficulty must also increase chicken patrol speed");
            checks += 3;

            GameModel singleSpawnProbe = new GameModel(new Random(31));
            singleSpawnProbe.StartRound(Mode.Single);
            GameModel.Chicken singleSource = singleSpawnProbe.Chickens[0];
            singleSource.CenterX = 130;
            singleSpawnProbe.Chickens[1].Hp = 0;
            singleSpawnProbe.Chickens[2].Hp = 0;
            singleSpawnProbe.SpawnSingleEgg();
            GameModel.FallingEgg singleSpawnedEgg = singleSpawnProbe.FallingEggs[0];
            Require(singleSpawnedEgg.SourceLane == singleSource.Lane
                    && Math.Abs((singleSpawnedEgg.X + GameModel.EggW / 2) - singleSource.CenterX) <= 8.000001,
                "Single eggs must spawn from the chicken's current moving position");
            Require(singleSource.Anim == AnimState.Jumping && singleSource.StandTime > 0,
                "laying an egg must start the jump clip and hold the chicken still");
            checks += 2;

            for (int i = 0; i < 13; i++)
            {
                singleSpawnProbe.Update(0.05, 0, 0);
            }

            Require(singleSource.Anim == AnimState.Walking && singleSource.ActionTime == 0,
                "the jump clip must play once and hand the chicken back to walking");
            checks++;

            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                while (chicken.Alive())
                {
                    model.ApplyChickenHit(chicken);
                }
            }

            Require(model.Phase == Phase.Won, "defeating all single-player chickens must win");
            checks++;

            GameModel loss = new GameModel(new Random(11));
            loss.StartRound(Mode.Single);
            loss.SpawnTimers[0] = 999;
            for (int i = 0; i < 6; i++)
            {
                PlaceMiss(loss, 0, EggKind.Normal, 10 + i * 12);
            }

            loss.Update(0, 0, 0);
            Require(loss.Phase == Phase.Lost && loss.Player(0).LivesHalf == 0,
                "six single-player misses must end the round");
            checks++;

            model.StartRound(Mode.Duo);
            model.SpawnTimers[0] = 999;
            model.SpawnTimers[1] = 999;
            Require(model.Mode == Mode.Duo
                    && model.Player(0).LivesHalf == 6
                    && model.Player(1).LivesHalf == 6,
                "Duo must reset two independent players");
            Require(model.Player(0).BasketX < GameModel.WorldW / 2.0
                    && model.Player(1).BasketX > GameModel.WorldW / 2.0,
                "Duo baskets must start in separate halves");
            Require(model.Chickens.Count == 4
                    && model.Chickens.Count(chicken => chicken.Owner == 0) == 2
                    && model.Chickens.Count(chicken => chicken.Owner == 1) == 2,
                "Duo must create two moving chicken sources for each player");
            checks += 3;

            double p1Start = model.Player(0).BasketX;
            double p2Start = model.Player(1).BasketX;
            model.Update(0.05, 1, 0);
            Require(model.Player(0).BasketX > p1Start && model.Player(1).BasketX == p2Start,
                "P1 input must move only the P1 basket");
            p1Start = model.Player(0).BasketX;
            model.Update(0.05, 0, -1);
            Require(model.Player(1).BasketX < p2Start && model.Player(0).BasketX == p1Start,
                "P2 input must move only the P2 basket");
            checks += 2;

            model.FallingEggs.Clear();
            model.Elapsed = 0;
            model.SpawnDuoEgg(1);
            GameModel.FallingEgg duoSpawnedEgg = model.FallingEggs[0];
            GameModel.Chicken duoSource = model.Chickens
                .First(chicken => chicken.Owner == 1 && chicken.Lane == duoSpawnedEgg.SourceLane);
            Require(Math.Abs((duoSpawnedEgg.X + GameModel.EggW / 2) - duoSource.CenterX) <= 7.000001
                    && Math.Abs(duoSpawnedEgg.Y - (duoSource.Y + 25)) < 0.000001,
                "Duo eggs must spawn from their owner's current moving chicken");
            checks++;
            model.FallingEggs.Clear();

            PlaceCatch(model, 0, EggKind.Normal);
            model.Update(0, 0, 0);
            Require(model.Player(0).Score == 10
                    && model.Player(0).Combo == 1
                    && model.Player(0).Ammo == 0
                    && model.Player(1).Score == 0,
                "Duo normal catches must credit only the catching player without ammo");
            checks++;

            int p1Lives = model.Player(0).LivesHalf;
            int p2Lives = model.Player(1).LivesHalf;
            PlaceMiss(model, 1, EggKind.Normal, 300);
            model.Update(0, 0, 0);
            Require(model.Player(0).LivesHalf == p1Lives && model.Player(1).LivesHalf == p2Lives - 1,
                "Duo misses must damage only the egg owner");
            checks++;

            p1Lives = model.Player(0).LivesHalf;
            p2Lives = model.Player(1).LivesHalf;
            PlaceMiss(model, 0, EggKind.Freeze, 20);
            model.Update(0, 0, 0);
            Require(model.Player(0).LivesHalf == p1Lives && model.Player(1).LivesHalf == p2Lives,
                "missed Duo power eggs must be harmless");
            checks++;

            PlaceCatch(model, 0, EggKind.Speed);
            model.Update(0, 0, 0);
            Require(Math.Abs(model.Player(0).SpeedTime - 4.0) < 1e-9 && model.Player(1).SpeedTime == 0,
                "Duo speed must affect only the catcher");
            checks++;

            PlaceCatch(model, 0, EggKind.Freeze);
            model.Update(0, 0, 0);
            double frozenX = model.Player(1).BasketX;
            model.Update(0.05, 0, 1);
            Require(model.Player(1).BasketX == frozenX, "freeze must stop the opponent's movement");
            checks++;

            model.Player(1).FreezeTime = 0;
            PlaceCatch(model, 0, EggKind.Reverse);
            model.Update(0, 0, 0);
            double reversedX = model.Player(1).BasketX;
            model.Update(0.05, 0, 1);
            Require(model.Player(1).BasketX < reversedX, "reverse must invert the opponent's controls");
            checks++;

            model.Player(1).Combo = 6;
            PlaceCatch(model, 0, EggKind.Golden);
            model.Update(0, 0, 0);
            GameModel.FallingEgg opponentEgg = new GameModel.FallingEgg(EggKind.Normal, 1, 0, 300, 100);
            Require(Math.Abs(model.Player(1).SabotageTime - 5.0) < 1e-9
                    && model.Player(1).Combo == 0
                    && model.FallSpeedFor(opponentEgg) > model.BaseFallSpeed(),
                "golden eggs must speed up the opponent's drops and break their combo");
            checks++;

            GameModel duoWin = new GameModel(new Random(17));
            duoWin.StartRound(Mode.Duo);
            duoWin.SpawnTimers[0] = 999;
            duoWin.SpawnTimers[1] = 999;
            for (int i = 0; i < 6; i++)
            {
                PlaceMiss(duoWin, 0, EggKind.Normal, 10 + i * 12);
            }

            duoWin.Update(0, 0, 0);
            Require(duoWin.Phase == Phase.Won && duoWin.WinnerPlayer == 2,
                "P2 must win when P1 loses all hearts");
            checks++;

            GameModel draw = new GameModel(new Random(23));
            draw.StartRound(Mode.Duo);
            draw.SpawnTimers[0] = 999;
            draw.SpawnTimers[1] = 999;
            for (int i = 0; i < 6; i++)
            {
                PlaceMiss(draw, 0, EggKind.Normal, 10 + i * 12);
                PlaceMiss(draw, 1, EggKind.Normal, 260 + i * 12);
            }

            draw.Update(0, 0, 0);
            Require(draw.Phase == Phase.Won && draw.WinnerPlayer == 0,
                "simultaneous equal-score Duo knockouts must draw");
            checks++;

            GameModel patrolStress = new GameModel(new Random(37));
            patrolStress.StartRound(Mode.Duo);
            patrolStress.SpawnTimers[0] = 999;
            patrolStress.SpawnTimers[1] = 999;
            for (int i = 0; i < 10000; i++)
            {
                patrolStress.Update(0.05, 0, 0);
            }

            Require(patrolStress.Chickens.All(chicken =>
                    chicken.CenterX >= chicken.MinX
                    && chicken.CenterX <= chicken.MaxX
                    && (chicken.Owner == 0
                        ? chicken.CenterX < GameModel.WorldW / 2.0
                        : chicken.CenterX > GameModel.WorldW / 2.0)),
                "long-running Duo patrols must stay inside their owner's arena");
            checks++;

            model.RestartCurrentMode();
            Require(model.Mode == Mode.Duo
                    && model.Phase == Phase.Playing
                    && model.Player(0).LivesHalf == 6
                    && model.Player(1).LivesHalf == 6
                    && model.FallingEggs.Count == 0
                    && model.Chickens.All(chicken =>
                        chicken.CenterX == chicken.StartX && chicken.Direction == chicken.StartDirection),
                "Duo restart must preserve the mode and fully reset both players");
            checks++;

            model.ReturnToMenu();
            Require(model.Phase == Phase.Menu, "Escape flow must return the model to the menu");
            checks++;

            return "SELF-TEST PASSED: " + checks + " gameplay checks";
        }

        private static void PlaceCatch(GameModel model, int owner, EggKind kind)
        {
            GameModel.PlayerState player = model.Player(owner);
            model.FallingEggs.Add(new GameModel.FallingEgg(
                kind,
                owner,
                0,
                player.BasketX + 10,
                GameModel.BasketY - GameModel.EggH + 1));
        }

        private static void PlaceMiss(GameModel model, int owner, EggKind kind, double x)
        {
            model.FallingEggs.Add(new GameModel.FallingEgg(kind, owner, 0, x, GameModel.GroundY + 8));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Self-test failed: " + message);
            }
        }
    }
}
