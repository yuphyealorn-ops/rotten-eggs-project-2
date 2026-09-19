using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace RottenEggs.EditorTools
{
    /// <summary>
    /// Editor-only regression checks for the controller navigation layer. These
    /// tests intentionally drive private game input plumbing by reflection so the
    /// gameplay driver remains free of test-only public surface area.
    /// </summary>
    public static class ControllerSelfTest
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Rotten Eggs/Verify Controller Controls")]
        public static void VerifyControllerControls()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            int checks = 0;

#if ENABLE_INPUT_SYSTEM
            TestGamepadButtonsArePressedOnlyAndLatchUse(ref checks);
            TestGamepadNavigationIsEdgeTriggeredAndResets(ref checks);
            TestGamepadMinorDriftDoesNotLatchUsage(ref checks);
            TestGamepadShouldersSetVolumeDirections(ref checks);
#else
            TestLegacyControllerReaderIsEmpty(ref checks);
#endif
            TestMainMenuControllerNavigationWrapsAndConfirms(ref checks);
            TestPauseResumeDoesNotThrowThrough(ref checks);
            TestPauseConfirmDoesNotThrowThrough(ref checks);
            TestPauseMenuNavigationWrapsAndConfirmsRestart(ref checks);
            TestPauseMenuConfirmMainMenuClearsPaused(ref checks);
            TestPausedTickFreezesGameplayTimers(ref checks);
            TestVolumeChangesWhilePaused(ref checks);
            TestMuteTogglesWhilePaused(ref checks);
            TestRestartClearsPausedState(ref checks);
            TestResultsBackReturnsToMenu(ref checks);
            TestResultsRestartStartsFreshRound(ref checks);

            return "controller controls self-test passed (" + checks + " checks)";
        }

#if ENABLE_INPUT_SYSTEM
        private static Gamepad AddGamepad()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            pad.MakeCurrent();
            return pad;
        }

        private static void TestGamepadButtonsArePressedOnlyAndLatchUse(ref int checks)
        {
            Gamepad pad = AddGamepad();
            GamepadControls controls = new GamepadControls();
            try
            {
                Require(!controls.Read(false).Confirm
                        && !controls.Used,
                    "connecting a neutral gamepad must not latch controller usage");
                GamepadState pressed = new GamepadState()
                    .WithButton(GamepadButton.South)
                    .WithButton(GamepadButton.East)
                    .WithButton(GamepadButton.Start)
                    .WithButton(GamepadButton.West)
                    .WithButton(GamepadButton.Select)
                    .WithButton(GamepadButton.LeftShoulder)
                    .WithButton(GamepadButton.RightShoulder);
                InputSystem.QueueStateEvent(pad, pressed);
                InputSystem.Update();

                GamepadControls.Frame first = controls.Read(false);
                Require(first.Confirm, "south must confirm on the press frame");
                Require(first.Back, "east must route to back on the press frame");
                Require(first.Fire, "east must also expose fire for gameplay routing on the press frame");
                Require(first.Pause, "start must pause on the press frame");
                Require(first.Restart, "west must restart on the press frame");
                Require(first.Mute, "select must toggle mute on the press frame");
                Require(first.Volume == 0, "opposing shoulder presses must cancel volume direction");
                Require(controls.Used, "real button input must latch controller usage");
                checks += 9;

                InputSystem.QueueStateEvent(pad, pressed);
                InputSystem.Update();
                GamepadControls.Frame held = controls.Read(false);
                Require(!held.Confirm
                        && !held.Back
                        && !held.Fire
                        && !held.Pause
                        && !held.Restart
                        && !held.Mute,
                    "held gamepad buttons must not repeat as pressedThisFrame actions");
                checks++;
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

        private static void TestGamepadNavigationIsEdgeTriggeredAndResets(ref int checks)
        {
            Gamepad pad = AddGamepad();
            GamepadControls controls = new GamepadControls();
            try
            {
                Require(controls.Read(true).Navigate == 0
                        && !controls.Used,
                    "connecting a neutral gamepad must not latch controller usage");
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 1f) });
                InputSystem.Update();

                GamepadControls.Frame first = controls.Read(true);
                Require(first.Navigate == -1, "stick up must navigate to the previous menu item once immediately");
                GamepadControls.Frame held = controls.Read(true);
                Require(held.Navigate == 0, "held navigation must not loop through menu selections");
                GamepadControls.Frame stillHeld = controls.Read(true);
                Require(stillHeld.Navigate == 0, "held navigation must remain silent until the stick returns to neutral");

                controls.Read(false);
                GamepadControls.Frame reset = controls.Read(true);
                Require(reset.Navigate == -1, "leaving navigation context must reset the repeat gate");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.Update();
                Require(controls.Read(true).Navigate == 0, "returning the stick to neutral must not navigate");

                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0f, 1f) });
                InputSystem.Update();
                Require(controls.Read(true).Navigate == -1,
                    "a fresh stick press after neutral must navigate exactly once");
                checks += 6;

                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.05f, 0.05f) });
                InputSystem.Update();
                GamepadControls.Frame drift = controls.Read(true);
                Require(drift.Navigate == 0, "minor stick drift must not navigate");
                checks++;
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }
        private static void TestGamepadMinorDriftDoesNotLatchUsage(ref int checks)
        {
            Gamepad pad = AddGamepad();
            GamepadControls controls = new GamepadControls();
            try
            {
                controls.Read(true);
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.05f, 0.05f) });
                InputSystem.Update();

                GamepadControls.Frame drift = controls.Read(true);
                Require(drift.Move == 0 && drift.Navigate == 0 && !controls.Used,
                    "minor stick drift must not latch controller usage or produce movement");
                checks++;
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

        private static void TestGamepadShouldersSetVolumeDirections(ref int checks)
        {
            Gamepad pad = AddGamepad();
            GamepadControls controls = new GamepadControls();
            try
            {
                controls.Read(false);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                InputSystem.Update();
                Require(controls.Read(false).Volume == -1,
                    "left shoulder must lower volume");

                InputSystem.QueueStateEvent(pad, new GamepadState());
                InputSystem.Update();
                controls.Read(false);

                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                InputSystem.Update();
                Require(controls.Read(false).Volume == 1,
                    "right shoulder must raise volume");
                checks += 2;
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

#else
        private static void TestLegacyControllerReaderIsEmpty(ref int checks)
        {
            GamepadControls controls = new GamepadControls();
            GamepadControls.Frame frame = controls.Read(true);
            Require(frame.Move == 0
                    && frame.Navigate == 0
                    && frame.Volume == 0
                    && !frame.Confirm
                    && !frame.Back
                    && !frame.Pause
                    && !frame.Restart
                    && !frame.Mute
                    && !frame.Fire
                    && !controls.Used,
                "legacy controller reader must return an empty frame and not latch usage");
            checks++;
        }
#endif

        private static void TestMainMenuControllerNavigationWrapsAndConfirms(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Tick(0, Frame(navigate: -1));
                Require(harness.MenuSelection == 2, "main menu navigation must wrap from first option to quit");
                harness.Tick(0, Frame(navigate: 1));
                Require(harness.MenuSelection == 0, "main menu navigation must wrap forward from quit to first option");
                harness.Tick(0, Frame(navigate: 1));
                harness.Tick(0, Frame(confirm: true));
                Require(harness.Model.Phase == Phase.Playing && harness.Model.Mode == Mode.Duo,
                    "confirming the second main menu option must start duo mode");
                checks += 3;
            }
        }

        private static void TestPauseResumeDoesNotThrowThrough(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Model.Player(0).Ammo = 1;
                harness.Model.Player(0).FireCooldown = 0;

                harness.Tick(0, Frame(pause: true));
                Require(harness.Paused, "start must pause during play");
                harness.Tick(0, Frame(back: true, fire: true));
                Require(!harness.Paused, "east/back must resume from pause");
                Require(harness.Model.Player(0).Ammo == 1
                        && !harness.Model.Player(0).PendingThrow
                        && harness.Model.Shots.Count == 0,
                    "resuming with east must not also fire a held egg on the same frame");
                checks += 3;
            }
        }

        private static void TestPauseConfirmDoesNotThrowThrough(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Model.Player(0).Ammo = 1;
                harness.Model.Player(0).FireCooldown = 0;

                harness.Tick(0, Frame(pause: true));
                harness.Tick(0, Frame(confirm: true));
                Require(!harness.Paused, "confirming the default pause option must resume");
                Require(harness.Model.Player(0).Ammo == 1
                        && !harness.Model.Player(0).PendingThrow
                        && harness.Model.Shots.Count == 0,
                    "confirming pause resume must not throw on the same frame");
                checks += 2;
            }
        }

        private static void TestPauseMenuNavigationWrapsAndConfirmsRestart(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Model.Player(0).LivesHalf = 1;
                harness.Paused = true;

                harness.Tick(0, Frame(navigate: -1));
                Require(harness.PauseSelection == 3,
                    "pause menu navigation must wrap from resume to quit without confirming it");
                harness.Tick(0, Frame(navigate: 1));
                Require(harness.PauseSelection == 0,
                    "pause menu navigation must wrap forward from quit to resume");
                harness.Tick(0, Frame(navigate: 1));
                harness.Tick(0, Frame(confirm: true));
                Require(harness.Model.Phase == Phase.Playing
                        && harness.Model.Player(0).LivesHalf == GameModel.SingleMaxLivesHalf
                        && !harness.Paused,
                    "confirming restart in the pause menu must start a fresh unpaused round");
                checks += 3;
            }
        }

        private static void TestPauseMenuConfirmMainMenuClearsPaused(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Paused = true;

                harness.Tick(0, Frame(navigate: 1));
                harness.Tick(0, Frame(navigate: 1));
                Require(harness.PauseSelection == 2,
                    "pause menu navigation must reach the main menu option");
                harness.Tick(0, Frame(confirm: true));
                Require(harness.Model.Phase == Phase.Menu && !harness.Paused,
                    "confirming main menu in pause must return to the menu and clear pause");
                checks += 2;
            }
        }

        private static void TestPausedTickFreezesGameplayTimers(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                GameModel.PlayerState player = harness.Model.Player(0);
                player.Ammo = 1;
                player.FireCooldown = 0;
                harness.Model.Fire(0);
                player.SpeedTime = 5.0;
                harness.Model.FallingEggs.Add(new GameModel.FallingEgg(EggKind.Normal, 0, 0, 120, 40));
                harness.Model.CrackedEggs.Add(new GameModel.CrackedEgg(EggKind.Normal, 50, 60, 1.1));
                harness.Model.Particles.Add(new GameModel.Particle(10, 20, 3, 4, 0.9, 2, GameModel.Cream));
                double basketX = player.BasketX;
                double chickenX = harness.Model.Chickens[0].CenterX;
                double elapsed = harness.Model.Elapsed;

                harness.Paused = true;
                harness.Tick(0.20, Frame(move: 1));

                Require(player.PendingThrow && Math.Abs(player.ThrowTime) < 1e-9,
                    "paused ticks must not advance pending throw animation time");
                Require(Math.Abs(player.SpeedTime - 5.0) < 1e-9,
                    "paused ticks must not advance power-up timers");
                Require(Math.Abs(player.BasketX - basketX) < 1e-9
                        && Math.Abs(harness.Model.Chickens[0].CenterX - chickenX) < 1e-9
                        && Math.Abs(harness.Model.Elapsed - elapsed) < 1e-9,
                    "paused ticks must not move baskets, chickens, or elapsed time");
                Require(Math.Abs(harness.Model.FallingEggs[0].Age) < 1e-9
                        && harness.Model.Shots.Count == 0,
                    "paused ticks must not advance falling eggs or release throws");
                Require(Math.Abs(harness.Model.CrackedEggs[0].Life - 1.1) < 1e-9
                        && Math.Abs(harness.Model.Particles[0].Life - 0.9) < 1e-9,
                    "paused ticks must not advance crack or particle lifetimes");
                checks += 5;
            }
        }

        private static void TestVolumeChangesWhilePaused(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Audio.SetVolume(0.50f);
                harness.Paused = true;
                harness.Tick(0, Frame(volume: 1));
                Require(harness.Audio.GetVolume() > 0.50f, "controller shoulder volume must work while paused");
                checks++;
            }
        }

        private static void TestMuteTogglesWhilePaused(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Paused = true;
                harness.Tick(0, Frame(mute: true));
                Require(harness.Audio.IsMuted(), "controller mute must work while paused");
                checks++;
            }
        }

        private static void TestRestartClearsPausedState(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Model.Player(0).LivesHalf = 1;
                harness.Paused = true;
                harness.Tick(0, Frame(restart: true));
                Require(!harness.Paused, "controller restart must clear pause state");
                Require(harness.Model.Phase == Phase.Playing
                        && harness.Model.Player(0).LivesHalf == GameModel.SingleMaxLivesHalf,
                    "controller restart must start a fresh playing round");
                checks += 2;
            }
        }

        private static void TestResultsBackReturnsToMenu(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Model.Phase = Phase.Lost;
                harness.Tick(0, Frame(back: true));
                Require(harness.Model.Phase == Phase.Menu && !harness.Paused,
                    "controller back on the results screen must return to the main menu");
                checks++;
            }
        }

        private static void TestResultsRestartStartsFreshRound(ref int checks)
        {
            using (GameHarness harness = GameHarness.Create())
            {
                harness.Model.StartRound(Mode.Single, Stage.Stage1);
                harness.Model.Phase = Phase.Won;
                harness.Tick(0, Frame(restart: true));
                Require(harness.Model.Phase == Phase.Playing && !harness.Paused,
                    "controller restart on the results screen must start a fresh round");
                checks++;
            }
        }

        private static GamepadControls.Frame Frame(
            int move = 0,
            int navigate = 0,
            int volume = 0,
            bool confirm = false,
            bool back = false,
            bool pause = false,
            bool restart = false,
            bool mute = false,
            bool fire = false)
        {
            return new GamepadControls.Frame
            {
                Move = move,
                Navigate = navigate,
                Volume = volume,
                Confirm = confirm,
                Back = back,
                Pause = pause,
                Restart = restart,
                Mute = mute,
                Fire = fire
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class GameHarness : IDisposable
        {
            private readonly GameObject host;
            private readonly RottenEggsGame game;
            private readonly MethodInfo tick;

            private GameHarness(GameObject host, RottenEggsGame game, MethodInfo tick)
            {
                this.host = host;
                this.game = game;
                this.tick = tick;
            }

            public GameModel Model { get; private set; }
            public AudioManager Audio { get; private set; }

            public bool Paused
            {
                get { return (bool)GetPrivateField("paused"); }
                set { SetPrivateField("paused", value); }
            }

            public int MenuSelection
            {
                get { return (int)GetPrivateField("menuSelection"); }
            }

            public int PauseSelection
            {
                get { return (int)GetPrivateField("pauseSelection"); }
            }

            public static GameHarness Create()
            {
                GameObject host = new GameObject("Controller Controls Self Test");
                host.SetActive(false);
                RottenEggsGame game = host.AddComponent<RottenEggsGame>();
                MethodInfo tick = typeof(RottenEggsGame).GetMethod(
                    "Tick",
                    PrivateInstance,
                    null,
                    new[] { typeof(double), typeof(GamepadControls.Frame) },
                    null);
                Require(tick != null, "RottenEggsGame must expose private Tick(double dt, GamepadControls.Frame pad)");

                GameHarness harness = new GameHarness(host, game, tick)
                {
                    Model = new GameModel(new System.Random(101)),
                    Audio = AudioManager.Silent()
                };
                harness.SetPrivateField("model", harness.Model);
                harness.SetPrivateField("audioManager", harness.Audio);
                harness.SetPrivateFieldIfPresent("ready", true);
                harness.SetPrivateFieldIfPresent("menuSelection", 0);
                harness.SetPrivateFieldIfPresent("pauseSelection", 0);
                return harness;
            }

            public void Tick(double dt, GamepadControls.Frame frame)
            {
                tick.Invoke(game, new object[] { dt, frame });
            }

            public void Dispose()
            {
                if (host != null)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }

            private object GetPrivateField(string name)
            {
                FieldInfo field = typeof(RottenEggsGame).GetField(name, PrivateInstance);
                Require(field != null, "RottenEggsGame must contain private field " + name);
                return field.GetValue(game);
            }

            private void SetPrivateField(string name, object value)
            {
                FieldInfo field = typeof(RottenEggsGame).GetField(name, PrivateInstance);
                Require(field != null, "RottenEggsGame must contain private field " + name);
                field.SetValue(game, value);
            }

            private void SetPrivateFieldIfPresent(string name, object value)
            {
                FieldInfo field = typeof(RottenEggsGame).GetField(name, PrivateInstance);
                if (field != null)
                {
                    field.SetValue(game, value);
                }
            }
        }
    }
}
