using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RottenEggs
{
    /// <summary>
    /// Entry point for the Unity port: input, the game clock, audio hooks, and
    /// presenting the finished pixel frame. This is the Unity counterpart of
    /// the Java prototype's GamePanel, minus the drawing, which lives in
    /// <see cref="GameRenderer"/>.
    ///
    /// <para>Drop this component on one empty GameObject and press Play. It
    /// builds everything it needs at runtime, so the scene stays empty.</para>
    /// </summary>
    [AddComponentMenu("Rotten Eggs/Rotten Eggs Game")]
    public sealed class RottenEggsGame : MonoBehaviour
    {
        [Tooltip("Starting music and effects volume, matching the Java prototype.")]
        [Range(0f, 1f)]
        [SerializeField]
        private float startingVolume = 0.70f;

        [Tooltip("Log the ported rule checks and asset checks when the game starts.")]
        [SerializeField]
        private bool runSelfTestOnStart = false;

        private GameModel model;
        private AudioManager audioManager;
        private ChickenSprites sprites;
        private PixelCanvas canvas;
        private GameRenderer frameRenderer;
        private Texture2D frameTexture;

        /// <summary>Drives the menu chickens, whose clock the paused game rules do not run.</summary>
        private double menuClock;
        private int menuSelection;
        private bool ready;

        private void Awake()
        {
            model = new GameModel();

            try
            {
                sprites = ChickenSprites.Load();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Rotten Eggs could not start: " + exception.Message
                               + "\nExpected the chicken sheets in " + ChickenSprites.SpritesDirectory);
                enabled = false;
                return;
            }

            canvas = new PixelCanvas(GameModel.WorldW, GameModel.WorldH);
            frameRenderer = new GameRenderer(canvas, sprites);
            frameTexture = new Texture2D(GameModel.WorldW, GameModel.WorldH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            audioManager = AudioManager.Create(gameObject);
            audioManager.SetVolume(startingVolume);
            audioManager.PlayMusic(AudioManager.Music.Menu);
            ready = true;

            if (runSelfTestOnStart)
            {
                RunSelfTest();
            }
        }

        private void Update()
        {
            if (!ready)
            {
                return;
            }

            HandleMenuKeys();
            HandleAudioKeys();

            int p1Axis = (Held(GameKey.D) ? 1 : 0) - (Held(GameKey.A) ? 1 : 0);
            int p2Axis = (Held(GameKey.Right) ? 1 : 0) - (Held(GameKey.Left) ? 1 : 0);
            if (model.Mode == Mode.Single)
            {
                p1Axis = ClampAxis(p1Axis + p2Axis);
                p2Axis = 0;
            }

            if (Pressed(GameKey.Space))
            {
                model.Fire(0);
            }

            double dt = Time.deltaTime;
            menuClock += dt;
            model.Update(dt, p1Axis, p2Axis);
            PlayModelEvents();

            frameRenderer.Render(model, audioManager, menuClock, menuSelection);
            canvas.UploadTo(frameTexture);
        }

        private void HandleMenuKeys()
        {
            if (Pressed(GameKey.Up) || Pressed(GameKey.W))
            {
                NavigateMenu(-1);
            }

            if (Pressed(GameKey.Down) || Pressed(GameKey.S))
            {
                NavigateMenu(1);
            }

            if (Pressed(GameKey.Enter))
            {
                ConfirmSelection();
            }

            if (Pressed(GameKey.One))
            {
                StartMode(Mode.Single);
            }

            if (Pressed(GameKey.Two))
            {
                StartMode(Mode.Duo);
            }

            if (Pressed(GameKey.R) && model.Phase != Phase.Menu)
            {
                RestartRound();
            }

            if (Pressed(GameKey.Escape))
            {
                ReturnToMenu();
            }
        }

        private void HandleAudioKeys()
        {
            if (Pressed(GameKey.M))
            {
                bool mutedNow = audioManager.ToggleMute();
                if (!mutedNow)
                {
                    audioManager.Play(AudioManager.Sfx.UiConfirm);
                }
            }

            if (Pressed(GameKey.Minus) || Pressed(GameKey.NumpadMinus))
            {
                AdjustVolume(-0.10f);
            }

            if (Pressed(GameKey.Equals) || Pressed(GameKey.NumpadPlus))
            {
                AdjustVolume(0.10f);
            }
        }

        private void NavigateMenu(int direction)
        {
            if (model.Phase != Phase.Menu)
            {
                return;
            }

            menuSelection = ((menuSelection + direction) % 2 + 2) % 2;
            audioManager.Play(AudioManager.Sfx.UiMove);
        }

        private void ConfirmSelection()
        {
            if (model.Phase == Phase.Menu)
            {
                StartMode(menuSelection == 0 ? Mode.Single : Mode.Duo);
            }
            else if (model.Phase == Phase.Won || model.Phase == Phase.Lost)
            {
                RestartRound();
            }
        }

        private void StartMode(Mode selectedMode)
        {
            if (model.Phase != Phase.Menu)
            {
                return;
            }

            model.StartRound(selectedMode);
            audioManager.Play(AudioManager.Sfx.UiConfirm);
            audioManager.PlayMusic(AudioManager.Music.Game);
        }

        private void RestartRound()
        {
            model.RestartCurrentMode();
            audioManager.Play(AudioManager.Sfx.UiConfirm);
            audioManager.PlayMusic(AudioManager.Music.Game);
        }

        private void ReturnToMenu()
        {
            if (model.Phase == Phase.Menu)
            {
                return;
            }

            model.ReturnToMenu();
            audioManager.Play(AudioManager.Sfx.UiMove);
            audioManager.PlayMusic(AudioManager.Music.Menu);
        }

        private void AdjustVolume(float delta)
        {
            audioManager.SetVolume(audioManager.GetVolume() + delta);
            if (!audioManager.IsMuted())
            {
                audioManager.Play(AudioManager.Sfx.UiMove);
            }
        }

        private void PlayModelEvents()
        {
            List<GameModel.GameEvent> events = model.DrainEvents();
            bool terminal = false;
            bool lost = false;
            foreach (GameModel.GameEvent gameEvent in events)
            {
                if (gameEvent.Type == EventType.Win || gameEvent.Type == EventType.Lose)
                {
                    terminal = true;
                }

                if (gameEvent.Type == EventType.Lose)
                {
                    lost = true;
                }
            }

            if (terminal)
            {
                audioManager.StopMusic();
                audioManager.Play(lost ? AudioManager.Sfx.Lose : AudioManager.Sfx.Win);
                return;
            }

            foreach (GameModel.GameEvent gameEvent in events)
            {
                audioManager.Play(EffectFor(gameEvent.Type));
            }
        }

        private static AudioManager.Sfx EffectFor(EventType type)
        {
            switch (type)
            {
                case EventType.Catch:
                    return AudioManager.Sfx.Catch;
                case EventType.Throw:
                    return AudioManager.Sfx.Throw;
                case EventType.Hit:
                    return AudioManager.Sfx.Hit;
                case EventType.Miss:
                    return AudioManager.Sfx.Crack;
                case EventType.Power:
                    return AudioManager.Sfx.Boost;
                case EventType.ChickenDown:
                    return AudioManager.Sfx.ChickenDown;
                case EventType.Lose:
                    return AudioManager.Sfx.Lose;
                default:
                    return AudioManager.Sfx.Win;
            }
        }

        private static int ClampAxis(int axis)
        {
            return Mathf.Clamp(axis, -1, 1);
        }

        /// <summary>
        /// Presents the finished frame. The view scales only in whole-number
        /// steps and is centred on a dark field, keeping text and artwork sharp
        /// at any window size, exactly as the Java panel did.
        /// </summary>
        private void OnGUI()
        {
            if (!ready || frameTexture == null || Event.current.type != UnityEngine.EventType.Repaint)
            {
                return;
            }

            Color previousColor = GUI.color;
            GUI.color = GameModel.Dark;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            float availableScale = Mathf.Min(
                Screen.width / (float)GameModel.WorldW,
                Screen.height / (float)GameModel.WorldH);
            int pixelScale = Mathf.Max(1, Mathf.FloorToInt(availableScale));
            int drawWidth = GameModel.WorldW * pixelScale;
            int drawHeight = GameModel.WorldH * pixelScale;
            int x = (Screen.width - drawWidth) / 2;
            int y = (Screen.height - drawHeight) / 2;
            GUI.DrawTexture(new Rect(x, y, drawWidth, drawHeight), frameTexture);
        }

        private void OnDestroy()
        {
            if (audioManager != null)
            {
                audioManager.Close();
            }

            if (frameTexture != null)
            {
                Destroy(frameTexture);
            }
        }

        /// <summary>Runs the ported rule checks plus the bundled-asset checks.</summary>
        [ContextMenu("Run Self Test")]
        public void RunSelfTest()
        {
            Debug.Log(GameModelSelfTest.Run());
            Debug.Log(AudioManager.VerifyBundledAssets());
            Debug.Log(ChickenSprites.VerifyBundledAssets());
        }

        private enum GameKey
        {
            A,
            D,
            Left,
            Right,
            Space,
            Up,
            Down,
            W,
            S,
            Enter,
            One,
            Two,
            R,
            Escape,
            M,
            Minus,
            Equals,
            NumpadMinus,
            NumpadPlus
        }

#if ENABLE_INPUT_SYSTEM
        private static Key Mapped(GameKey key)
        {
            switch (key)
            {
                case GameKey.A: return Key.A;
                case GameKey.D: return Key.D;
                case GameKey.Left: return Key.LeftArrow;
                case GameKey.Right: return Key.RightArrow;
                case GameKey.Space: return Key.Space;
                case GameKey.Up: return Key.UpArrow;
                case GameKey.Down: return Key.DownArrow;
                case GameKey.W: return Key.W;
                case GameKey.S: return Key.S;
                case GameKey.Enter: return Key.Enter;
                case GameKey.One: return Key.Digit1;
                case GameKey.Two: return Key.Digit2;
                case GameKey.R: return Key.R;
                case GameKey.Escape: return Key.Escape;
                case GameKey.M: return Key.M;
                case GameKey.Minus: return Key.Minus;
                case GameKey.Equals: return Key.Equals;
                case GameKey.NumpadMinus: return Key.NumpadMinus;
                default: return Key.NumpadPlus;
            }
        }

        private static bool Held(GameKey key)
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[Mapped(key)].isPressed;
        }

        private static bool Pressed(GameKey key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            if (key == GameKey.Enter && keyboard[Key.NumpadEnter].wasPressedThisFrame)
            {
                return true;
            }

            return keyboard[Mapped(key)].wasPressedThisFrame;
        }
#else
        private static KeyCode Mapped(GameKey key)
        {
            switch (key)
            {
                case GameKey.A: return KeyCode.A;
                case GameKey.D: return KeyCode.D;
                case GameKey.Left: return KeyCode.LeftArrow;
                case GameKey.Right: return KeyCode.RightArrow;
                case GameKey.Space: return KeyCode.Space;
                case GameKey.Up: return KeyCode.UpArrow;
                case GameKey.Down: return KeyCode.DownArrow;
                case GameKey.W: return KeyCode.W;
                case GameKey.S: return KeyCode.S;
                case GameKey.Enter: return KeyCode.Return;
                case GameKey.One: return KeyCode.Alpha1;
                case GameKey.Two: return KeyCode.Alpha2;
                case GameKey.R: return KeyCode.R;
                case GameKey.Escape: return KeyCode.Escape;
                case GameKey.M: return KeyCode.M;
                case GameKey.Minus: return KeyCode.Minus;
                case GameKey.Equals: return KeyCode.Equals;
                case GameKey.NumpadMinus: return KeyCode.KeypadMinus;
                default: return KeyCode.KeypadPlus;
            }
        }

        private static bool Held(GameKey key)
        {
            return Input.GetKey(Mapped(key));
        }

        private static bool Pressed(GameKey key)
        {
            if (key == GameKey.Enter && Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                return true;
            }

            return Input.GetKeyDown(Mapped(key));
        }
#endif
    }
}
