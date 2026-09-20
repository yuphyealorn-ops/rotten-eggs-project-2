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
            Require(model.Player(0).LivesHalf == GameModel.SingleMaxLivesHalf && model.Player(0).Ammo == 0,
                "single mode must start with five hearts and empty ammo");
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

            model.Player(0).Combo = 9;
            PlaceCatch(model, 0, EggKind.Normal);
            model.Update(0.01, 0, 0);
            Require(model.Player(0).InFever() && model.Player(0).Multiplier() == 3,
                "the tenth consecutive catch must activate Fever's x3 score multiplier");
            checks++;
            model.Player(0).Combo = 1;
            model.Player(0).Ammo = 1;

            model.Player(0).FireCooldown = 0;
            double throwStartX = model.Player(0).BasketX;
            model.Fire(0);
            Require(model.Player(0).Ammo == 0
                    && model.Player(0).PendingThrow
                    && model.Player(0).ThrowTime == 0
                    && Math.Abs(model.Player(0).FireCooldown - 0.22) < 1e-9
                    && model.Shots.Count == 0,
                "single throw must consume one ammo and wait for frame three before releasing a shot");
            checks++;

            model.Update(0.024, 0, 0);
            Require(model.Player(0).PendingThrow && model.Shots.Count == 0,
                "throw animation must not release before frame three");
            checks++;

            model.Update(0.026, 0, 0);
            Require(!model.Player(0).PendingThrow
                    && model.Player(0).ThrowTime > 0
                    && model.Shots.Count == 1
                    && Math.Abs(model.Shots[0].X - (throwStartX + GameModel.BasketW / 2 - GameModel.EggW / 2)) < 1e-9
                    && Math.Abs(model.Shots[0].Y - (GameModel.BasketRimY - GameModel.EggH)) < 1e-9,
                "throw animation must release one shot on frame three from the basket rim");
            checks++;

            model.Update(0.05, 0, 0);
            Require(model.Shots.Count == 1 && model.Player(0).ThrowTime == -1,
                "completed throw animation must end without releasing duplicate shots");
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

            GameModel.Chicken[] stageOne = model.Chickens.ToArray();
            foreach (GameModel.Chicken chicken in stageOne)
            {
                while (chicken.Alive())
                {
                    model.ApplyChickenHit(chicken);
                }

                model.Update(0, 0, 0);
            }

            Require(model.CurrentStage == Stage.Stage2 && model.Phase == Phase.Playing,
                "clearing the three stage-1 chickens must move the run on to stage 2");
            Require(model.Player(0).Score > 0 && model.Player(0).LivesHalf > 0
                    && model.Player(0).LivesHalf < GameModel.SingleMaxLivesHalf,
                "a stage clear must carry the run's score and hearts over, not reset them");
            checks += 2;

            // ── Stages: stage 2 respawns, the boss takes eight hits ───────────
            GameModel stages = new GameModel(new Random(53));
            stages.StartRound(Mode.Single, Stage.Stage2);
            stages.SpawnTimers[0] = 999;
            Require(stages.CurrentStage == Stage.Stage2
                    && stages.Chickens.Count == 3
                    && stages.Chickens.All(chicken => chicken.CanRespawn),
                "stage 2 must line up three chickens that can respawn");
            checks++;

            GameModel.Chicken respawner = stages.Chickens[0];
            while (respawner.Alive())
            {
                stages.ApplyChickenHit(respawner);
            }

            Require(!respawner.Alive() && respawner.RespawnTimer > 0,
                "a downed stage-2 chicken must start its respawn countdown");
            checks++;

            // Run one second past the respawn delay, whatever the rules set it to.
            int respawnTicks = (int)Math.Ceiling((GameModel.Stage2RespawnSeconds + 1.0) / 0.05);
            for (int i = 0; i < respawnTicks; i++)
            {
                stages.Update(0.05, 0, 0);
            }

            Require(respawner.Alive() && respawner.Hp == 4,
                "a stage-2 chicken must climb back onto its perch at full health");
            checks++;

            int stageGuard = 0;
            while (stages.CurrentStage == Stage.Stage2 && stageGuard < 12)
            {
                stageGuard++;
                GameModel.Chicken target = stages.Chickens.First(chicken => chicken.Alive());
                while (target.Alive())
                {
                    stages.ApplyChickenHit(target);
                }

                stages.Update(0, 0, 0);
                if (stages.CurrentStage != Stage.Stage2)
                {
                    break;
                }

                // Let any downed chicken climb back before the next knockdown, which
                // is what makes stage 2 a race against the respawn clock.
                for (int i = 0; i < 110; i++)
                {
                    stages.SpawnTimers[0] = 999;
                    stages.Update(0.05, 0, 0);
                }
            }

            Require(stages.CurrentStage == Stage.BossStage && stages.Phase == Phase.Playing,
                "six stage-2 knockdowns must open the boss stage");
            Require(stages.Chickens.Count == 1 && stages.Chickens[0].IsBoss && stages.Chickens[0].Hp == 8,
                "the boss stage must roll out a single eight-hit boss");
            checks += 2;

            GameModel.Chicken boss = stages.Chickens[0];
            for (int i = 0; i < 7; i++)
            {
                stages.ApplyChickenHit(boss);
            }

            Require(boss.Alive() && boss.Hp == 1,
                "the boss must still be standing after seven hits");
            checks++;

            stages.ApplyChickenHit(boss);
            stages.Update(0, 0, 0);
            Require(stages.Phase == Phase.Won && stages.WinnerPlayer == 1,
                "the eighth hit must clear the whole run");
            checks++;

            // ── Shields and the slow-down field ───────────────────────────────
            GameModel power = new GameModel(new Random(61));
            power.StartRound(Mode.Single);
            power.SpawnTimers[0] = 999;
            for (int i = 0; i < 3; i++)
            {
                PlaceCatch(power, 0, EggKind.Shield);
                power.Update(0, 0, 0);
            }

            Require(power.Player(0).ShieldCount == 3, "shield eggs must bank up to three shields");
            checks++;

            int shieldedLives = power.Player(0).LivesHalf;
            PlaceMiss(power, 0, EggKind.Normal, 20);
            power.Update(0, 0, 0);
            Require(power.Player(0).LivesHalf == shieldedLives && power.Player(0).ShieldCount == 2,
                "a banked shield must absorb a missed egg instead of half a heart");
            checks++;

            PlaceCatch(power, 0, EggKind.SlowDown);
            power.Update(0, 0, 0);
            GameModel.FallingEgg slowedEgg = new GameModel.FallingEgg(EggKind.Normal, 0, 0, 100, 100);
            Require(Math.Abs(power.Player(0).SlowDownTimer - 5.0) < 1e-9
                    && power.FallSpeedFor(slowedEgg) < power.BaseFallSpeed(),
                "a slow-down egg must last five seconds and halve the falling eggs' speed");
            checks++;

            GameModel spriteRules = new GameModel(new Random(67));
            spriteRules.StartRound(Mode.Single);
            spriteRules.SpawnTimers[0] = 999;
            Require(GameModel.EggW == 14 && GameModel.EggH == 18,
                "egg sprites must use the 14x18 animation frame size");
            Require(GameModel.BasketY == 209
                    && GameModel.BasketH == 15
                    && GameModel.BasketSpriteY == 199
                    && GameModel.BasketRimY == 203,
                "new basket sprite anchors must preserve the old gameplay basket anchor");
            UnityEngine.Rect basketBounds = spriteRules.Player(0).BasketBounds();
            Require(Math.Abs(basketBounds.y - GameModel.BasketRimY) < 1e-6
                    && Math.Abs(basketBounds.height - (225 - GameModel.BasketRimY)) < 1e-6,
                "basket collision bounds must run from the rim to the fixed bottom");
            checks += 3;

            Require(spriteRules.Player(0).CatchTime == -1
                    && spriteRules.Player(0).ThrowTime == -1
                    && !spriteRules.Player(0).PendingThrow,
                "player animation state must start idle after a round reset");
            checks++;

            spriteRules.Player(0).Ammo = 2;
            spriteRules.Player(0).FireCooldown = 0;
            spriteRules.Fire(0);
            spriteRules.Player(0).BasketX += 20;
            spriteRules.Update(0.05, 0, 0);
            Require(spriteRules.Player(0).Ammo == 1
                    && spriteRules.Shots.Count == 1
                    && Math.Abs(spriteRules.Shots[0].X - (spriteRules.Player(0).BasketX + GameModel.BasketW / 2 - GameModel.EggW / 2)) < 1e-9,
                "delayed throw release must use the current basket center and consume ammo only once");
            checks++;
            int shotCountAfterRelease = spriteRules.Shots.Count;
            spriteRules.Update(0.05, 0, 0);
            Require(spriteRules.Shots.Count == shotCountAfterRelease,
                "released throw must not duplicate on later animation frames");
            checks++;

            GameModel.Shot oldShot = new GameModel.Shot(spriteRules.Player(0).BasketX, 100);
            spriteRules.Shots.Clear();
            spriteRules.Shots.Add(oldShot);
            spriteRules.Player(0).Ammo = 1;
            spriteRules.Player(0).FireCooldown = 0;
            spriteRules.Fire(0);
            double oldShotY = oldShot.Y;
            spriteRules.Update(0.05, 0, 0);
            Require(spriteRules.Shots.Count == 2 && oldShot.Y < oldShotY,
                "throw release must happen after existing shots advance for the tick");
            checks++;

            spriteRules.Player(0).Ammo = 1;
            spriteRules.Player(0).FireCooldown = 0;
            spriteRules.Fire(0);
            PlaceCatch(spriteRules, 0, EggKind.Normal);
            spriteRules.Update(0, 0, 0);
            Require(spriteRules.Player(0).PendingThrow
                    && spriteRules.Player(0).CatchTime == 0,
                "catch animation must not cancel a pending throw animation");
            checks++;

            GameModel catchProbe = new GameModel(new Random(71));
            catchProbe.StartRound(Mode.Single);
            catchProbe.SpawnTimers[0] = 999;
            PlaceCatch(catchProbe, 0, EggKind.Shield);
            double caughtCenter = catchProbe.FallingEggs[0].X + GameModel.EggW / 2;
            catchProbe.Update(0, 0, 0);
            Require(catchProbe.Player(0).CatchTime == 0
                    && Math.Abs(catchProbe.Player(0).CatchX - caughtCenter) < 1e-9,
                "single-player power catches must record catch animation time and egg center");
            checks++;

            GameModel duoCatchProbe = new GameModel(new Random(73));
            duoCatchProbe.StartRound(Mode.Duo);
            duoCatchProbe.SpawnTimers[0] = 999;
            duoCatchProbe.SpawnTimers[1] = 999;
            PlaceCatch(duoCatchProbe, 1, EggKind.Speed);
            caughtCenter = duoCatchProbe.FallingEggs[0].X + GameModel.EggW / 2;
            duoCatchProbe.Update(0, 0, 0);
            Require(duoCatchProbe.Player(1).CatchTime == 0
                    && Math.Abs(duoCatchProbe.Player(1).CatchX - caughtCenter) < 1e-9,
                "duo catches must record catch animation state for the catching player");
            checks++;

            GameModel ageProbe = new GameModel(new Random(79));
            ageProbe.StartRound(Mode.Single);
            ageProbe.SpawnTimers[0] = 999;
            ageProbe.FallingEggs.Add(new GameModel.FallingEgg(EggKind.Normal, 0, 0, 30, 30));
            ageProbe.Shots.Add(new GameModel.Shot(30, 120));
            ageProbe.Update(0.05, 0, 0);
            Require(Math.Abs(ageProbe.FallingEggs[0].Age - 0.05) < 1e-9
                    && Math.Abs(ageProbe.Shots[0].Age - 0.05) < 1e-9,
                "falling eggs and thrown shots must age by the simulation delta");
            checks++;

            GameModel landingProbe = new GameModel(new Random(83));
            landingProbe.StartRound(Mode.Single);
            landingProbe.SpawnTimers[0] = 999;
            landingProbe.FallingEggs.Add(new GameModel.FallingEgg(
                EggKind.Normal,
                0,
                0,
                42,
                GameModel.GroundY - GameModel.EggH));
            landingProbe.Update(0, 0, 0);
            Require(landingProbe.FallingEggs.Count == 0
                    && landingProbe.CrackedEggs.Count == 1
                    && Math.Abs(landingProbe.CrackedEggs[0].Y - (GameModel.GroundY - GameModel.EggH)) < 1e-9,
                "eggs must miss when their 14x18 rect touches the ground and crack on that same rect");
            checks++;

            GameModel loss = new GameModel(new Random(11));
            loss.StartRound(Mode.Single);
            loss.SpawnTimers[0] = 999;
            loss.Player(0).CatchTime = 0;
            loss.Player(0).ThrowTime = 0;
            loss.Player(0).PendingThrow = true;
            for (int i = 0; i < GameModel.SingleMaxLivesHalf; i++)
            {
                PlaceMiss(loss, 0, EggKind.Normal, 10 + i * 12);
            }

            loss.Update(0, 0, 0);
            Require(loss.Phase == Phase.Lost
                    && loss.Player(0).LivesHalf == 0
                    && loss.Player(0).CatchTime == -1
                    && loss.Player(0).ThrowTime == -1
                    && !loss.Player(0).PendingThrow,
                "ten single-player misses must end the round and clear animation state");
            checks++;

            model.StartRound(Mode.Duo);
            model.SpawnTimers[0] = 999;
            model.SpawnTimers[1] = 999;
            Require(model.Mode == Mode.Duo
                    && model.Player(0).LivesHalf == GameModel.DuoMaxLivesHalf
                    && model.Player(1).LivesHalf == GameModel.DuoMaxLivesHalf,
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

            // Duo power eggs are banked on catch and fire only when used.
            PlaceCatch(model, 0, EggKind.Speed);
            model.Update(0, 0, 0);
            Require(model.Player(0).SpeedTime == 0
                    && model.Player(0).Slots.Count == 1 && model.Player(0).Slots[0] == EggKind.Speed,
                "a Duo power catch must bank the egg instead of firing it");
            model.Deploy(0);
            Require(Math.Abs(model.Player(0).SpeedTime - 4.0) < 1e-9 && model.Player(1).SpeedTime == 0
                    && model.Player(0).Slots.Count == 0,
                "using a banked speed egg must boost only the user and empty the slot");
            checks += 2;

            PlaceCatch(model, 0, EggKind.Freeze);
            model.Update(0, 0, 0);
            double freeX = model.Player(1).BasketX;
            model.Update(0.05, 0, 1);
            Require(model.Player(1).BasketX > freeX, "a banked freeze must not touch the opponent until used");
            model.Deploy(0);
            double frozenX = model.Player(1).BasketX;
            model.Update(0.05, 0, 1);
            Require(model.Player(1).BasketX == frozenX, "a used freeze must stop the opponent's movement");
            checks += 2;

            model.Player(1).FreezeTime = 0;
            PlaceCatch(model, 0, EggKind.Reverse);
            model.Update(0, 0, 0);
            model.Deploy(0);
            double reversedX = model.Player(1).BasketX;
            model.Update(0.05, 0, 1);
            Require(model.Player(1).BasketX < reversedX, "a used reverse must invert the opponent's controls");
            checks++;

            // Two slots: a third catch replaces the selected one; W/S moves the selection.
            PlaceCatch(model, 0, EggKind.Golden);
            model.Update(0, 0, 0);
            PlaceCatch(model, 0, EggKind.Speed);
            model.Update(0, 0, 0);
            Require(model.Player(0).Slots.Count == 2 && model.Player(0).SelectedSlot == 1,
                "the bank holds two eggs and selects the newest");
            model.CycleSlot(0, -1);
            Require(model.Player(0).SelectedSlot == 0, "cycling must move the selection");
            PlaceCatch(model, 0, EggKind.Freeze);
            model.Update(0, 0, 0);
            Require(model.Player(0).Slots.Count == 2 && model.Player(0).Slots[0] == EggKind.Freeze
                    && model.Player(0).Slots[1] == EggKind.Speed,
                "a catch with a full bank must replace the selected slot");
            checks += 3;

            model.Player(0).Slots.Clear();
            model.Player(1).Combo = 6;
            PlaceCatch(model, 0, EggKind.Golden);
            model.Update(0, 0, 0);
            model.Deploy(0);
            GameModel.FallingEgg opponentEgg = new GameModel.FallingEgg(EggKind.Normal, 1, 0, 300, 100);
            Require(Math.Abs(model.Player(1).SabotageTime - 5.0) < 1e-9
                    && model.Player(1).Combo == 0
                    && model.FallSpeedFor(opponentEgg) > model.BaseFallSpeed(),
                "a used golden egg must speed up the opponent's drops and break their combo");
            checks++;

            // Mirrored spawns: one clock, one roll, two reflected eggs.
            GameModel mirror = new GameModel(new Random(11));
            mirror.StartRound(Mode.Duo);
            mirror.SpawnTimers[0] = 0;
            mirror.Update(0.001, 0, 0);
            Require(mirror.FallingEggs.Count == 2
                    && mirror.FallingEggs[0].Owner == 0 && mirror.FallingEggs[1].Owner == 1
                    && mirror.FallingEggs[0].Kind == mirror.FallingEggs[1].Kind
                    && Math.Abs(mirror.FallingEggs[0].Y - mirror.FallingEggs[1].Y) < 1e-9
                    && Math.Abs((mirror.FallingEggs[0].X + mirror.FallingEggs[1].X + GameModel.EggW) - GameModel.WorldW) < 1e-6,
                "Duo must lay the same egg on both sides, reflected across the divider");
            checks++;

            // Sudden death: past the clock, eggs fall faster and keep ramping.
            GameModel clock = new GameModel(new Random(5));
            clock.StartRound(Mode.Duo);
            clock.SpawnTimers[0] = 999;
            double calm = clock.BaseFallSpeed();
            clock.Elapsed = GameModel.DuoSuddenDeathSeconds - 0.01;
            Require(!clock.SuddenDeath && clock.SuddenDeathIn > 0 && clock.SuddenDeathIn < 0.02, "the clock counts down to sudden death");
            clock.Elapsed = GameModel.DuoSuddenDeathSeconds;
            double onset = clock.BaseFallSpeed();
            clock.Elapsed = GameModel.DuoSuddenDeathSeconds + 20;
            Require(clock.SuddenDeath && onset > calm && clock.BaseFallSpeed() > onset,
                "sudden death must speed the drops up and keep ramping");
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

            model.Player(0).CatchTime = 0;
            model.Player(0).ThrowTime = 0;
            model.Player(0).PendingThrow = true;
            model.RestartCurrentMode();
            Require(model.Mode == Mode.Duo
                    && model.Phase == Phase.Playing
                    && model.Player(0).LivesHalf == GameModel.DuoMaxLivesHalf
                    && model.Player(1).LivesHalf == GameModel.DuoMaxLivesHalf
                    && model.Player(0).CatchTime == -1
                    && model.Player(0).ThrowTime == -1
                    && !model.Player(0).PendingThrow
                    && model.FallingEggs.Count == 0
                    && model.Chickens.All(chicken =>
                        chicken.CenterX == chicken.StartX && chicken.Direction == chicken.StartDirection),
                "Duo restart must preserve the mode and fully reset both players and animation state");
            checks++;

            model.Player(0).CatchTime = 0;
            model.Player(0).ThrowTime = 0;
            model.Player(0).PendingThrow = true;
            model.ReturnToMenu();
            Require(model.Phase == Phase.Menu
                    && model.Player(0).CatchTime == -1
                    && model.Player(0).ThrowTime == -1
                    && !model.Player(0).PendingThrow,
                "Escape flow must return the model to the menu and clear animation state");
            checks++;

            // ── Boss fight ────────────────────────────────────────────────────
            GameModel bossRun = new GameModel();
            bossRun.StartRound(Mode.Single, Stage.BossStage);
            GameModel.Chicken bossChicken = bossRun.Chickens[0];
            GameModel.PlayerState hero = bossRun.Player(0);
            // The boss keeps laying eggs; drain them so only its attacks touch the hero here.
            Action tick = () =>
            {
                bossRun.Update(0.05, 0, 0);
                bossRun.FallingEggs.Clear();
            };
            Require(bossChicken.IsBoss && bossChicken.BossPhase == BossPhase.Fly && !bossChicken.BossVulnerable,
                "the boss must open the fight airborne and armoured");
            checks++;

            // An egg thrown at the awake boss is spent but does no damage.
            hero.Ammo = 5;
            int hpBefore = bossChicken.Hp;
            bossRun.Shots.Add(new GameModel.Shot(bossChicken.CenterX - GameModel.EggW / 2, bossChicken.DrawY + 10));
            tick();
            Require(bossChicken.Hp == hpBefore && bossRun.Shots.Count == 0,
                "an awake boss must shrug an egg off without losing health");
            checks++;

            // Flying long enough, the boss breaks off, chases the basket, stalls
            // over it, and the flock gathers beneath the now-stationary bird.
            for (int i = 0; i < 80 && bossRun.Feathers.Count == 0; i++)
            {
                tick();
            }

            Require(bossRun.Feathers.Count == 1 && bossChicken.FlyState == BossFlyState.Hovering,
                "the flying boss must stall overhead before letting the flock go");
            Require(Math.Abs(bossRun.Feathers[0].X - bossChicken.CenterX) < 0.001
                    && Math.Abs(bossRun.Feathers[0].X - (hero.BasketX + GameModel.BasketW / 2)) <= GameModel.BossArriveDistance + 0.001,
                "the flock must form directly under the boss, which is directly over the basket");
            double stalledX = bossChicken.CenterX;
            tick();
            tick();
            Require(bossChicken.CenterX == stalledX && bossRun.Feathers[0].WindingUp,
                "the boss must hold still while the flock gathers");
            checks += 3;

            // Standing still under it costs half a heart; a shield takes it instead.
            hero.ShieldCount = 1;
            int livesBefore = hero.LivesHalf;
            for (int i = 0; i < 40 && bossRun.Feathers.Count > 0; i++)
            {
                tick();
            }

            Require(hero.ShieldCount == 0 && hero.LivesHalf == livesBefore,
                "a feather strike must be absorbed by a shield before it touches hearts");
            checks++;

            // Cycle: the boss lands to bomb, then falls asleep and becomes vulnerable.
            while (bossChicken.BossPhase == BossPhase.Fly)
            {
                tick();
            }

            Require(bossChicken.BossPhase == BossPhase.Bomb, "after flying the boss must land and start bombing");
            checks++;

            for (int i = 0; i < 80 && bossRun.Bombs.Count == 0; i++)
            {
                tick();
            }

            Require(bossRun.Bombs.Count >= 1, "the bombing boss must drop a bomb on its jump");
            checks++;

            // Park the basket under the bomb and let it go off.
            GameModel.Bomb bomb = bossRun.Bombs[0];
            hero.BasketX = bomb.CenterX - GameModel.BasketW / 2;
            livesBefore = hero.LivesHalf;
            hero.ShieldCount = 0;
            for (int i = 0; i < 80 && !bomb.Hurt && bossRun.Bombs.Contains(bomb); i++)
            {
                tick();
                hero.BasketX = bomb.CenterX - GameModel.BasketW / 2;
            }

            Require(hero.LivesHalf == livesBefore - 1, "a bomb going off under the basket must cost half a heart");
            checks++;

            while (bossChicken.BossPhase == BossPhase.Bomb)
            {
                tick();
            }

            Require(bossChicken.BossPhase == BossPhase.Sleep && bossChicken.BossVulnerable,
                "after bombing the boss must fall asleep and drop its guard");
            checks++;

            hpBefore = bossChicken.Hp;
            bossRun.Shots.Add(new GameModel.Shot(bossChicken.CenterX - GameModel.EggW / 2, bossChicken.DrawY + 10));
            tick();
            Require(bossChicken.Hp == hpBefore - 1 && bossChicken.BossPhase == BossPhase.Sleep,
                "an egg must hurt the sleeping boss, and one hit must not wake it");
            checks++;

            // The second hit of the nap wakes it, so a stockpile can't end the fight in one sleep.
            for (int i = 0; i < 20 && bossChicken.ActionTime > 0; i++)
            {
                tick();
            }

            bossRun.Shots.Add(new GameModel.Shot(bossChicken.CenterX - GameModel.EggW / 2, bossChicken.DrawY + 10));
            tick();
            Require(bossChicken.Hp == hpBefore - 2 && bossChicken.BossPhase == BossPhase.Fly && !bossChicken.BossVulnerable,
                "a second hit in the same sleep must wake the boss into flight, armoured again");
            checks++;

            // A bomb dropped at the end of the bombing phase may still be burning;
            // every bomb must be gone within its fall, fuse and smoke time.
            double bombLifetime = 1.0 + GameModel.BombFuseSeconds + GameModel.BombExplodeSeconds;
            for (int i = 0; i < (int)Math.Ceiling(bombLifetime / 0.05) && bossRun.Bombs.Count > 0; i++)
            {
                tick();
            }

            Require(bossRun.Bombs.Count == 0, "no bomb outlives its fall, fuse and smoke");
            checks++;

            // The damage flash starts on the sheet's first red frame, not its lead-in.
            GameModel probeRun = new GameModel(new Random(3));
            probeRun.StartRound(Mode.Single);
            probeRun.ApplyChickenHit(probeRun.Chickens[1]);
            Require(probeRun.Chickens[1].Anim == AnimState.Damage
                    && Math.Abs(probeRun.Chickens[1].AnimTime - GameModel.DamageLeadInSeconds) < 1e-9
                    && Math.Abs(probeRun.Chickens[1].ActionTime - (GameModel.DamageAnimSeconds - GameModel.DamageLeadInSeconds)) < 1e-9,
                "a hit must start the damage clip on its first red frame and run only the rest of it");
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
                GameModel.BasketRimY - GameModel.EggH + 1));
        }

        private static void PlaceMiss(GameModel model, int owner, EggKind kind, double x)
        {
            model.FallingEggs.Add(new GameModel.FallingEgg(kind, owner, 0, x, GameModel.GroundY - GameModel.EggH));
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
