using System;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Draws the whole game into the pixel canvas, port for port from the Java
    /// prototype's Java2D rendering. Nothing here touches game rules: it reads
    /// the model and paints it, exactly as the original panel did.
    ///
    /// Day 3 additions (renderer only, no logic changes):
    ///   - CRT scanline overlay drawn last on every frame
    ///   - Two-layer twinkling starfield replaces uniform dot-grid sky
    ///   - Menu: credits ticker above panel, corner brackets, bouncing arrow
    ///   - HUD: 1UP / 2UP labels, hearts backing box, flashing power labels, FEVER blink
    ///   - Result screen: corner brackets, blinking GAME OVER text for loss
    ///   - Duo divider: double solid line instead of dashed
    ///
    /// Single-player stage additions: the chained stage run (classic, respawn,
    /// boss), the stage goal readout, the respawn countdown, the boss health bar
    /// and the shield / slow-down egg artwork.
    /// </summary>
    public sealed class GameRenderer
    {
        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color32 Sky        = new Color32(126, 213, 247, 255);
        private static readonly Color32 SkyDot     = new Color32(185, 230, 250, 255);
        private static readonly Color32 Cloud      = new Color32(255, 241, 246, 255);
        private static readonly Color32 Grass      = new Color32(82,  172,  91, 255);
        private static readonly Color32 GrassDark  = new Color32(47,  123,  70, 255);
        private static readonly Color32 Soil       = new Color32(194, 119,  63, 255);
        private static readonly Color32 SoilDark   = new Color32(132,  75,  48, 255);
        private static readonly Color32 Wood       = new Color32(150,  92,  52, 255);
        private static readonly Color32 WoodLight  = new Color32(196, 129,  74, 255);
        private static readonly Color32 WoodDark   = new Color32(102,  58,  36, 255);
        private static readonly Color32 Panel      = new Color32(24,   37,  48, 226);
        private static readonly Color32 White      = new Color32(255, 250, 240, 255);
        private static readonly Color32 Muted      = new Color32(182, 218, 226, 255);
        private static readonly Color32 Black      = new Color32(0,     0,   0, 255);

        /// <summary>The chicken frames are 20 x 21, drawn at whole-number scale like the rest.</summary>
        private const int SpriteScale = 2;

        /// <summary>
        /// Tint applied to chickens, perches and baskets so they sit in the
        /// backdrop's evening light rather than floating over it at full
        /// brightness. Roughly 20% darker with the green pulled down, which
        /// reads as the same warm, low sun that lights the painting.
        /// </summary>
        private const byte SunsetShadeR = 209;
        private const byte SunsetShadeG = 184;
        private const byte SunsetShadeB = 179;

        // ── Font styles ───────────────────────────────────────────────────────
        private static readonly PixelFont.Style FontTiny   = new PixelFont.Style(1, 1, 0);
        private static readonly PixelFont.Style FontSmall  = new PixelFont.Style(1, 2, 0);
        private static readonly PixelFont.Style FontOption = new PixelFont.Style(2, 2, 1);
        private static readonly PixelFont.Style FontMedium = new PixelFont.Style(2, 2, 1);
        private static readonly PixelFont.Style FontLarge  = new PixelFont.Style(3, 3, 2);

        private readonly PixelCanvas canvas;
        private readonly ChickenSprites sprites;
        private readonly EggSprites eggs;

        /// <summary>
        /// Painted backdrop, already scaled to the canvas. Null falls back to the
        /// hand-drawn sky, hills and fields below.
        /// </summary>
        private readonly SpriteFrame backdrop;

        /// <summary>
        /// Layered HUD heart artwork. Null falls back to the drawn polygon heart.
        /// </summary>
        private readonly HeartSprites hearts;

        /// <summary>Boss attack artwork; any missing clip is drawn procedurally.</summary>
        private readonly BossSprites bossArt;

        /// <summary>
        /// Running clock (seconds) used for retro blink / animation effects.
        /// Set once at the top of Render() and read by every sub-drawer that
        /// needs timed animation without touching game rules.
        /// </summary>
        private double _clock;
        private bool controllerHints;
        private static readonly string[] PauseOptions = { "RESUME", "RESTART", "MAIN MENU", "QUIT GAME" };

        public GameRenderer(PixelCanvas canvas, ChickenSprites sprites, EggSprites eggs,
                            SpriteFrame backdrop = null, HeartSprites hearts = null,
                            BossSprites bossArt = null)
        {
            this.canvas   = canvas;
            this.sprites  = sprites;
            this.eggs     = eggs;
            this.backdrop = backdrop;
            this.hearts   = hearts;
            this.bossArt  = bossArt;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Entry point
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Paints one complete frame of the game.</summary>
        public void Render(GameModel model, AudioManager audio, double menuClock, int menuSelection,
                           bool controllerHints = false, bool paused = false, int pauseSelection = 0)
        {
            _clock = menuClock;   // store for all sub-drawers
            this.controllerHints = controllerHints;

            canvas.ResetTranslate();
            canvas.ClearClip();
            DrawBackground();

            if (model.Phase == Phase.Menu)
            {
                DrawMenuScene();
                DrawMenuOverlay(audio, menuSelection);
                DrawScanlines();
                return;
            }

            if (model.ShakeTime > 0)
            {
                int shake = ((int)(model.ShakeTime * 100) % 2 == 0) ? 2 : -2;
                canvas.SetTranslate(shake, 0);
            }

            if (model.Mode == Mode.Single)
            {
                DrawSingleWorld(model);
            }
            else
            {
                DrawDuoWorld(model);
            }

            DrawParticles(model);
            DrawCrackedEggs(model);
            DrawBossHazards(model);
            canvas.ResetTranslate();

            if (model.Mode == Mode.Single)
            {
                DrawSingleHud(model);
            }
            else
            {
                DrawDuoHud(model);
            }

            if (model.Phase == Phase.Won || model.Phase == Phase.Lost)
            {
                DrawResultOverlay(model);
            }
            if (paused) DrawPauseOverlay(audio, pauseSelection);

            // CRT scanline pass — always last so it sits over every layer.
            DrawScanlines();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Background
        // ══════════════════════════════════════════════════════════════════════

        private void DrawBackground()
        {
            // Painted artwork replaces every drawn layer below it — the picture
            // already carries its own sky, clouds, hills and ground line, so the
            // procedural versions would only fight it.
            if (backdrop != null)
            {
                canvas.DrawSprite(backdrop, 0, 0, GameModel.WorldW, GameModel.WorldH);
                return;
            }

            // Sky fill
            canvas.SetColor(Sky);
            canvas.FillRect(0, 0, GameModel.WorldW, GameModel.WorldH);

            // ── Day 3: two-layer twinkling starfield ──────────────────────────
            // Fixed positions (deterministic — no RNG per frame) so stars never
            // jump. Every 4th star "twinkles" by skipping every other frame on
            // its own half-second beat.
            int[] starX = { 14,  43,  78, 112, 158, 203, 261, 298, 345, 388, 421, 464,
                             27,  91, 137, 182, 234, 270, 316, 362, 405, 448,  19,  55 };
            int[] starY = { 28,  17,  44,  62,  23,  51,  38,  69,  15,  55,  31,  47,
                             58,  34,   8,  70,  19,  55,  43,  27,  62,  13,  78,   6 };

            for (int i = 0; i < starX.Length; i++)
            {
                // Twinkle: every 4th star drops out on alternate half-beats
                if (i % 4 == 0 && (int)(_clock * 1.25 + i) % 2 == 0)
                {
                    continue;
                }

                byte alpha = (byte)(i % 3 == 0 ? 230 : 175);
                canvas.SetColor(SkyDot.r, SkyDot.g, SkyDot.b, alpha);
                int size = (i % 6 == 0) ? 2 : 1;
                canvas.FillRect(starX[i], starY[i], size, size);
            }
            // ─────────────────────────────────────────────────────────────────

            DrawCloud(20,  42, 1);
            DrawCloud(186, 82, 0);
            DrawCloud(392, 36, 1);

            canvas.SetColor(98, 184, 120);
            canvas.FillPolygon(new[] { 0, 74, 145, 220 }, new[] { 219, 170, 219, 219 }, 4);
            canvas.SetColor(72, 155, 103);
            canvas.FillPolygon(new[] { 254, 337, 421, 480 }, new[] { 219, 174, 215, 219 }, 4);

            canvas.SetColor(Grass);
            canvas.FillRect(0, (int)GameModel.GroundY - 7, GameModel.WorldW, 14);
            canvas.SetColor(GrassDark);
            for (int x = 0; x < GameModel.WorldW; x += 7)
            {
                canvas.FillRect(x, (int)GameModel.GroundY - 10 - (x % 3), 2, 6 + (x % 3));
            }

            canvas.SetColor(Soil);
            canvas.FillRect(0, (int)GameModel.GroundY + 7, GameModel.WorldW, 25);
            canvas.SetColor(SoilDark);
            for (int x = 3; x < GameModel.WorldW; x += 13)
            {
                canvas.FillRect(x, 250 + (x * 7 % 13), 5, 2);
            }

            DrawFlower(31,  219, GameModel.Pink);
            DrawFlower(450, 220, GameModel.Gold);
            DrawFlower(420, 226, White);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Menu
        // ══════════════════════════════════════════════════════════════════════

        // ── Menu vignette timing ─────────────────────────────────────────────
        // One chicken per side, in the strips the menu panel leaves uncovered,
        // acting out a full fight: land, take four egg hits, die, vanish, and
        // come back. The two sides run half a cycle apart so something is
        // always moving. Four hits matches a real chicken's hit points.
        private const double VignettePeriod  = 7.6;
        private const double VignetteArrive  = 0.6;    // Jumping clip on landing
        private const double VignetteEggAir  = 0.35;   // seconds an egg takes to reach the chicken
        private const double VignetteVanish  = 7.2;    // dead chicken disappears here until the loop restarts
        private static readonly double[] VignetteHits = { 2.0, 3.5, 5.0, 6.5 };

        private const int VignettePerchTop = 156;
        private const int VignettePerchHalf = 26;

        private void DrawMenuScene()
        {
            DrawMenuVignette(32,  true,  _clock);
            DrawMenuVignette(448, false, _clock + VignettePeriod / 2);
        }

        private void DrawMenuVignette(int centerX, bool facingRight, double clock)
        {
            double t = clock % VignettePeriod;
            int chickenTop = VignettePerchTop - (int)GameModel.ChickenH;
            int chickenMidY = chickenTop + (int)GameModel.ChickenH / 2;

            DrawPerch(centerX - VignettePerchHalf, centerX + VignettePerchHalf, VignettePerchTop);

            // How many hits have landed so far, and when the latest one did.
            int hp = 4;
            double lastHit = -1;
            foreach (double hit in VignetteHits)
            {
                if (t >= hit)
                {
                    hp--;
                    lastHit = hit;
                }
            }

            bool dead = hp <= 0;
            if (dead && t >= VignetteVanish)
            {
                return;
            }

            AnimState state;
            double animTime;
            if (t < VignetteArrive)
            {
                state = AnimState.Jumping;
                animTime = t;
            }
            else if (dead)
            {
                state = AnimState.Die;
                animTime = t - lastHit;
            }
            else if (lastHit >= 0 && t - lastHit < GameModel.DamageAnimSeconds)
            {
                state = AnimState.Damage;
                animTime = t - lastHit;
            }
            else
            {
                state = AnimState.Idle;
                animTime = t;
            }

            DrawChicken(centerX, chickenTop, state, animTime, facingRight);
            if (!dead)
            {
                DrawHealthPips(hp, centerX, chickenTop - 8);
            }

            // An egg on its way up to the next hit, thrown from below the screen.
            foreach (double hit in VignetteHits)
            {
                double launch = hit - VignetteEggAir;
                if (t >= launch && t < hit)
                {
                    double progress = (t - launch) / VignetteEggAir;
                    int eggY = (int)Math.Round(GameModel.WorldH + (chickenMidY - GameModel.WorldH) * progress);
                    DrawEgg(centerX - (int)GameModel.EggW / 2, eggY, EggKind.Normal, true, t - launch);
                }
            }
        }

        private void DrawMenuOverlay(AudioManager audio, int menuSelection)
        {
            DrawCreditsTicker();

            // Panel background
            canvas.SetColor(15, 29, 39, 232);
            canvas.FillRect(64, 38, 352, 190);

            // ── Day 3: corner-bracket decoration (arcade cabinet style) ───────
            const int px = 64, py = 38, pw = 352, ph = 190, blen = 18;
            canvas.SetColor(GameModel.Pink);
            // top-left
            canvas.FillRect(px,            py,            blen, 4);
            canvas.FillRect(px,            py,            4,    blen);
            // top-right
            canvas.FillRect(px + pw - blen, py,            blen, 4);
            canvas.FillRect(px + pw - 4,    py,            4,    blen);
            // bottom-left
            canvas.FillRect(px,            py + ph - 4,   blen, 4);
            canvas.FillRect(px,            py + ph - blen, 4,   blen);
            // bottom-right
            canvas.FillRect(px + pw - blen, py + ph - 4,   blen, 4);
            canvas.FillRect(px + pw - 4,    py + ph - blen, 4,   blen);
            // ─────────────────────────────────────────────────────────────────

            // Title, centred in the header space between the panel top (38) and
            // the first menu option (105). FontLarge is 7 rows at PixelSize 3,
            // so a 21-tall line centred there has its baseline at 82, leaving an
            // even 23px above and below.
            DrawCenteredText("ROTTEN EGGS", 240, 82, White, Black, FontLarge);

            DrawMenuOption(92, 99, 296, 31, 0, menuSelection,
                "1  SINGLE PLAYER", "CATCH • THROW • CLEAR ALL " + GameModel.StageCount + " STAGES");
            DrawMenuOption(92, 134, 296, 31, 1, menuSelection,
                "2  DUO PLAYER", controllerHints ? "P1 STICK • P2 ARROWS" : "P1 A/D • P2 ARROWS");
            DrawMenuOption(92, 169, 296, 31, 2, menuSelection,
                "QUIT GAME", "CLOSE ROTTEN EGGS");

            // Navigation hint (original position)
            DrawCenteredText(controllerHints ? "STICK/D-PAD SELECT  •  SOUTH CONFIRM" : "↑/↓ OR W/S SELECT  •  ENTER CONFIRM",
                240, 210, Muted, Black, FontTiny);

            // Audio status
            string audioText = audio.IsMuted()
                ? "AUDIO MUTED"
                : "AUDIO " + Mathf.RoundToInt(audio.GetVolume() * 100) + "%";
            DrawCenteredText((controllerHints ? "SELECT MUTE • LB/RB VOLUME • " : "M MUTE • -/+ VOLUME • ") + audioText, 240, 220,
                audio.IsMuted() ? GameModel.Pink : GameModel.Gold, Black, FontTiny);

            // Developer credit on the open ground below the panel — a slim dark
            // plaque matched to the panel width keeps the text readable over the
            // painted dirt while leaving the fighting chickens at the edges clear.
            canvas.SetColor(15, 29, 39, 220);
            canvas.FillRect(64, 234, 352, 28);
            canvas.SetColor(GameModel.Pink);
            canvas.FillRect(64, 234, 18, 2);
            canvas.FillRect(64, 234, 2, 8);
            canvas.FillRect(416 - 18, 234, 18, 2);
            canvas.FillRect(416 - 2, 234, 2, 8);
            DrawCenteredText("A GAME BY", 240, 245, Muted, Black, FontTiny);
            DrawCenteredText("YE HTET AUNG  •  CHANYUPHYEA LORN", 240, 257, White, Black, FontTiny);
        }

        // ── Credits ticker ────────────────────────────────────────────────────
        // Everyone whose work is in the game, scrolling across the sky above the
        // panel. Keep this in step with CREDITS.md. Only characters the pixel
        // font has: no ampersand, so "and".
        private const string CreditsLine =
            "A GAME BY YE HTET AUNG AND CHANYUPHYEA LORN" +
            "   •   MUSIC: PECAN PIE (UPPBEAT), REST! (RICARDO CUELLO), CHRISYQN" +
            "   •   ART: VAMPIREGIRL, VMIINV" +
            "   •   SOUND: CHEQUERED INK" +
            "   •   ";
        private const double CreditsSpeed = 28;   // px/s, slow enough to read

        private void DrawCreditsTicker()
        {
            int width = PixelFont.TextWidth(CreditsLine, FontTiny);
            int loop = width + GameModel.WorldW / 2;
            int x = GameModel.WorldW - (int)((_clock * CreditsSpeed) % loop);

            // A dim band so the text reads over any part of the sky.
            canvas.SetColor(15, 29, 39, 150);
            canvas.FillRect(0, 20, GameModel.WorldW, 11);

            DrawShadowText(CreditsLine, x, 28, GameModel.Gold, Black, FontTiny);
            // The tail of the line follows the head so the loop is seamless.
            DrawShadowText(CreditsLine, x + loop, 28, GameModel.Gold, Black, FontTiny);
        }

        private void DrawMenuOption(
            int x, int y, int width, int height,
            int option, int menuSelection,
            string title, string detail)
        {
            bool selected   = menuSelection == option;
            Color32 accent  = option == 0 ? GameModel.Cyan : GameModel.Pink;

            if (selected)
            {
                canvas.SetColor(accent, 70);
            }
            else
            {
                canvas.SetColor(255, 255, 255, 18);
            }

            canvas.FillRect(x, y, width, height);

            canvas.SetColor(selected ? accent : new Color32(102, 126, 137, 255));
            canvas.FillRect(x,              y,              width, 2);
            canvas.FillRect(x,              y + height - 2, width, 2);
            canvas.FillRect(x,              y,              2,     height);
            canvas.FillRect(x + width - 2,  y,              2,     height);

            // ── Day 3: bouncing selector arrow ───────────────────────────────
            if (selected)
            {
                int bounce = (int)(_clock * 6.0) % 2 == 0 ? 0 : 2;
                canvas.SetColor(accent);
                canvas.FillPolygon(
                    new[] { x + 10 + bounce, x + 16 + bounce, x + 10 + bounce },
                    new[] { y + 13, y + 18, y + 23 }, 3);
            }
            // ─────────────────────────────────────────────────────────────────

            DrawCenteredText(title,  x + width / 2, y + 18,
                selected ? White : Muted, Black, FontOption);
            DrawCenteredText(detail, x + width / 2, y + 27,
                selected ? accent : new Color32(133, 165, 174, 255), Black, FontTiny);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Game worlds
        // ══════════════════════════════════════════════════════════════════════

        private void DrawSingleWorld(GameModel model)
        {
            // Eggs go down first so a fresh one drops out from under its perch.
            DrawFallingEggs(model);
            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                DrawPerch(chicken);
            }

            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                DrawChicken(chicken.CenterX, chicken.DrawY, chicken.Anim, chicken.AnimTime,
                    chicken.Facing > 0, ChickenDrawScale(chicken));
                if (chicken.Alive())
                {
                    if (chicken.IsBoss)
                    {
                        DrawBossHealthBar(chicken.Hp, GameModel.BossHp, chicken.CenterX, (int)chicken.DrawY - 8);
                        if (chicken.BossVulnerable)
                        {
                            DrawSleepMarker(chicken);
                        }
                    }
                    else
                    {
                        DrawHealthPips(chicken.Hp, chicken.CenterX, (int)chicken.Y - 8);
                    }
                }
                else if (chicken.IsRespawning())
                {
                    // Stage 2: the countdown says when the chicken is back on its perch.
                    DrawCenteredText("KO " + Mathf.CeilToInt((float)chicken.RespawnTimer),
                        (int)Math.Round(chicken.CenterX), (int)chicken.Y + 20,
                        White, GameModel.Dark, FontSmall);
                }
                else
                {
                    DrawCenteredText("KO", (int)Math.Round(chicken.CenterX),
                        (int)chicken.Y + (chicken.IsBoss ? 74 : 20),
                        White, GameModel.Dark, FontSmall);
                }
            }

            foreach (GameModel.Shot shot in model.Shots)
            {
                DrawEgg((int)shot.X, (int)shot.Y, EggKind.Normal, true, shot.Age);
            }

            GameModel.PlayerState player = model.Player(0);
            DrawBasket(player, GameModel.Pink, player.Ammo);
            if (player.InFever())
            {
                DrawFeverAura(model.Elapsed, (int)player.BasketX, (int)GameModel.BasketRimY, GameModel.Pink);
            }
        }

        private void DrawDuoWorld(GameModel model)
        {
            // ── Day 3: double solid centre divider (retro split-screen style) ─
            // Two 1-px columns with a 1-px gap give a sharper, more deliberate
            // look than the original dashed single line.
            canvas.SetColor(255, 255, 255, 115);
            canvas.FillRect(238, 38, 1, 220);
            canvas.FillRect(241, 38, 1, 220);
            // ─────────────────────────────────────────────────────────────────

            DrawFallingEggs(model);
            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                DrawPerch(chicken);
            }

            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                DrawChicken(chicken.CenterX, chicken.Y, chicken.Anim, chicken.AnimTime,
                    chicken.Facing > 0, ChickenDrawScale(chicken));
            }

            GameModel.PlayerState one = model.Player(0);
            GameModel.PlayerState two = model.Player(1);
            DrawBasket(one, GameModel.Cyan, 0);
            DrawBasket(two, GameModel.Pink, 0);
            if (one.InFever())
            {
                DrawFeverAura(model.Elapsed, (int)one.BasketX, (int)GameModel.BasketRimY, GameModel.Cyan);
            }

            if (two.InFever())
            {
                DrawFeverAura(model.Elapsed, (int)two.BasketX, (int)GameModel.BasketRimY, GameModel.Pink);
            }
        }

        private void DrawFallingEggs(GameModel model)
        {
            foreach (GameModel.FallingEgg egg in model.FallingEggs)
            {
                DrawFallingEgg(egg);
            }
        }

        private void DrawFallingEgg(GameModel.FallingEgg egg)
        {
            int x = (int)Math.Round(egg.X);
            int y = (int)Math.Round(egg.Y);
            double shadowT = Math.Max(0, Math.Min(1, 1 - (GameModel.GroundY - y) / 180.0));
            int sw = 2 + (int)(12 * shadowT);
            int sh = 1 + (int)(3 * shadowT);
            canvas.SetColor(0, 0, 0, (byte)(50 * shadowT));
            canvas.FillOval(x + (int)GameModel.EggW / 2 - sw / 2, (int)GameModel.GroundY - sh - 1, sw, sh);
            DrawEgg(x, y, egg.Kind, false, egg.Age);
        }

        // ══════════════════════════════════════════════════════════════════════
        // World decoration
        // ══════════════════════════════════════════════════════════════════════

        private void DrawCloud(int x, int y, int large)
        {
            canvas.SetColor(80, 154, 190);
            canvas.FillRect(x + 4, y + 7, 36 + large * 10, 7);
            canvas.SetColor(Cloud);
            canvas.FillRect(x + 3,              y + 5, 40 + large * 10, 7);
            canvas.FillRect(x + 9,              y + 1, 12,              7);
            canvas.FillRect(x + 22,             y - 3, 14 + large * 6,  11);
            canvas.FillRect(x + 34 + large * 5, y + 1, 10,              7);
        }

        private void DrawFlower(int x, int y, Color32 petals)
        {
            canvas.SetColor(GrassDark);
            canvas.FillRect(x,     y - 8,  2, 10);
            canvas.FillRect(x - 3, y - 4,  4,  2);
            canvas.SetColor(petals);
            canvas.FillRect(x - 3, y - 12, 3,  3);
            canvas.FillRect(x + 2, y - 12, 3,  3);
            canvas.FillRect(x,     y - 15, 3,  3);
            canvas.FillRect(x,     y - 9,  3,  3);
            canvas.SetColor(GameModel.Gold);
            canvas.FillRect(x,     y - 12, 3,  3);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Sprites & game objects
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws one chicken from its sheet clip. The clip advances with the
        /// model's own animation clock, so a one-shot clip such as the death
        /// sequence stops on its final frame instead of looping forever.
        /// </summary>
        private void DrawChicken(double centerX, double topY, AnimState state, double animTime, bool facingRight,
                                 int scale = SpriteScale)
        {
            SpriteFrame frame     = sprites.Animate(state).FrameAt(animTime, state.Loops());
            int         drawWidth  = frame.Width  * scale;
            int         drawHeight = frame.Height * scale;
            int         x          = (int)Math.Round(centerX) - drawWidth / 2;
            int         y          = (int)Math.Round(topY);
            // The artwork faces right; a left-bound chicken is mirrored in place.
            int nearX = facingRight ? x               : x + drawWidth;
            int farX  = facingRight ? x + drawWidth   : x;
            canvas.SetShade(SunsetShadeR, SunsetShadeG, SunsetShadeB);
            canvas.DrawSprite(frame, nearX, y, farX, y + drawHeight);
            canvas.ClearShade();
        }

        /// <summary>The boss is drawn one scale step larger than the laying chickens.</summary>
        private static int ChickenDrawScale(GameModel.Chicken chicken)
        {
            return chicken.IsBoss ? SpriteScale + 1 : SpriteScale;
        }

        /// <summary>Distance from a chicken's top to its perch, in draw pixels.</summary>
        private static int ChickenDrawHeight(GameModel.Chicken chicken)
        {
            return (int)GameModel.ChickenH * ChickenDrawScale(chicken) / SpriteScale;
        }

        /// <summary>Wooden perch a chicken patrols along, drawn under its whole lane.</summary>
        private void DrawPerch(GameModel.Chicken chicken)
        {
            DrawPerch(
                (int)Math.Round(chicken.MinX - GameModel.PerchMargin),
                (int)Math.Round(chicken.MaxX + GameModel.PerchMargin),
                (int)Math.Round(chicken.Y    + ChickenDrawHeight(chicken)));
        }

        private void DrawPerch(int left, int right, int top)
        {
            int width  = right - left;
            int height = (int)GameModel.PerchH;
            canvas.SetShade(SunsetShadeR, SunsetShadeG, SunsetShadeB);
            canvas.SetColor(GameModel.Dark);
            canvas.FillRect(left, top, width, height);
            canvas.SetColor(WoodLight);
            canvas.FillRect(left + 1, top + 1, width - 2, 2);
            canvas.SetColor(Wood);
            canvas.FillRect(left + 1, top + 3, width - 2, height - 4);
            canvas.SetColor(WoodDark);
            for (int x = left + 11; x < right - 6; x += 19)
            {
                canvas.FillRect(x, top + 1, 1, height - 2);
            }

            for (int x = left + 4; x < right - 8; x += 19)
            {
                canvas.FillRect(x, top + 4, 5, 1);
            }

            canvas.SetColor(GameModel.Dark);
            canvas.FillRect(left  + 4, top + height, 4, 4);
            canvas.FillRect(right - 8, top + height, 4, 4);
            canvas.SetColor(WoodDark);
            canvas.FillRect(left  + 5, top + height, 2, 3);
            canvas.FillRect(right - 7, top + height, 2, 3);
            canvas.ClearShade();
        }

        private void DrawHealthPips(int hp, double centerX, int y)
        {
            int x = (int)Math.Round(centerX) - 16;
            for (int i = 0; i < 4; i++)
            {
                canvas.SetColor(GameModel.Dark);
                canvas.FillRect(x + i * 8, y, 7, 4);
                canvas.SetColor(i < hp ? GameModel.Pink : new Color32(78, 94, 101, 255));
                canvas.FillRect(x + 1 + i * 8, y + 1, 5, 2);
            }
        }

        /// <summary>
        /// The boss takes eight hits, so it wears a segmented bar instead of the
        /// four pips the laying chickens carry. The bar flips to pink when the
        /// boss is past half health.
        /// </summary>
        private void DrawBossHealthBar(int hp, int maxHp, double centerX, int y)
        {
            const int cell = 9;
            int width = maxHp * cell + 2;
            int x = (int)Math.Round(centerX) - width / 2;
            canvas.SetColor(GameModel.Dark);
            canvas.FillRect(x, y, width, 7);
            for (int i = 0; i < maxHp; i++)
            {
                Color32 lit = hp * 2 > maxHp ? GameModel.Gold : GameModel.Pink;
                canvas.SetColor(i < hp ? lit : new Color32(78, 94, 101, 255));
                canvas.FillRect(x + 1 + i * cell, y + 2, cell - 1, 3);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Boss fight
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Drifting "Z"s over a sleeping boss, and a pulsing ring around its
        /// health bar: the one moment it can be hurt should be unmistakable.
        /// </summary>
        private void DrawSleepMarker(GameModel.Chicken boss)
        {
            int cx = (int)Math.Round(boss.CenterX);
            int top = (int)boss.DrawY;
            for (int i = 0; i < 3; i++)
            {
                double phase = (_clock * 0.9 + i * 0.33) % 1.0;
                int zx = cx + 22 + i * 6 + (int)(Math.Sin(phase * Math.PI * 2) * 3);
                int zy = top + 4 - (int)(phase * 22);
                byte alpha = (byte)(255 * (1.0 - phase));
                canvas.SetColor(White.r, White.g, White.b, alpha);
                PixelFont.Draw(canvas, "Z", zx, zy, FontTiny);
            }

            if ((int)(_clock * 3.0) % 2 == 0)
            {
                canvas.SetColor(GameModel.Gold, 200);
                int width = GameModel.BossHp * 9 + 6;
                int x = cx - width / 2;
                int y = top - 10;
                canvas.FillRect(x, y, width, 1);
                canvas.FillRect(x, y + 10, width, 1);
                canvas.FillRect(x, y, 1, 11);
                canvas.FillRect(x + width - 1, y, 1, 11);
            }
        }

        private void DrawBossHazards(GameModel model)
        {
            foreach (GameModel.FeatherStrike strike in model.Feathers)
            {
                DrawFeatherStrike(strike);
            }

            foreach (GameModel.Bomb bomb in model.Bombs)
            {
                if (bomb.Exploded)
                {
                    DrawExplosion(bomb);
                }
                else
                {
                    DrawBomb(bomb);
                }
            }
        }

        /// <summary>
        /// The bomb on its 14×18 egg rect: the falling wobble while it drops and
        /// sits, then the red armed flash for the last half second of the fuse.
        /// </summary>
        private void DrawBomb(GameModel.Bomb bomb)
        {
            int x = (int)Math.Round(bomb.X);
            int y = (int)Math.Round(bomb.Y);
            int w = GameModel.BombW;
            int h = GameModel.BombH;

            if (bossArt != null && bossArt.BombFall != null && bossArt.BombArmed != null)
            {
                SpriteFrame frame = bomb.Armed
                    ? BossSprites.Loop(bossArt.BombArmed, bomb.FuseTime, BossSprites.BombArmedFps)
                    : BossSprites.Loop(bossArt.BombFall, bomb.Age, BossSprites.BombFallFps);
                canvas.DrawSprite(frame, x, y, x + w, y + h);
                return;
            }

            // Placeholder: dark ball, fuse stub, spark; flushes red once armed.
            bool flash = bomb.Armed && (int)(bomb.FuseTime * BossSprites.BombArmedFps) % 2 == 0;
            canvas.SetColor(flash ? new Color32(172, 50, 50, 255) : new Color32(50, 60, 57, 255));
            canvas.FillOval(x + 1, y + 6, 12, 12);
            canvas.SetColor(WoodDark);
            canvas.FillRect(x + 7, y + 3, 2, 4);
            canvas.SetColor((int)(bomb.Age * 20.0) % 2 == 0 ? GameModel.Gold : White);
            canvas.FillRect(x + 7, y + 1, 2, 2);
        }

        /// <summary>
        /// The 32×32 blast, centred on the bomb: fireball, collapse, then dithered
        /// smoke that thins itself out — no fade needed.
        /// </summary>
        private void DrawExplosion(GameModel.Bomb bomb)
        {
            int cx = (int)Math.Round(bomb.CenterX);
            int cy = (int)Math.Round(bomb.CenterY);
            int size = GameModel.BlastSize;
            int x = cx - size / 2;
            int y = cy - size / 2;

            if (bossArt != null && bossArt.Explosion != null)
            {
                canvas.DrawSprite(BossSprites.Once(bossArt.Explosion, bomb.BlastTime, GameModel.BombExplodeSeconds),
                                  x, y, x + size, y + size);
                return;
            }

            double t = Math.Min(1.0, bomb.BlastTime / GameModel.BombExplodeSeconds);
            int radius = (int)Math.Round(4 + 12 * Math.Min(1.0, t * 2.0));
            byte alpha = (byte)(255 * (t < 0.5 ? 1.0 : 1.0 - (t - 0.5) * 2.0));
            Color32 fill = t < 0.25 ? GameModel.Gold
                         : t < 0.5  ? new Color32(255, 140, 40, 255)
                         : new Color32(120, 116, 112, 255);
            canvas.SetColor(fill, alpha);
            canvas.FillOval(cx - radius, cy - radius, radius * 2, radius * 2);
        }

        /// <summary>
        /// A feather wave. While the boss hovers, the warning sits under it with
        /// a dotted line to the ground; once released, each feather tumbles
        /// down on its own flutter phase and bursts where it lands.
        /// </summary>
        private void DrawFeatherStrike(GameModel.FeatherStrike strike)
        {
            int cx = (int)Math.Round(strike.X);
            int startY = (int)Math.Round(strike.StartY);

            if (strike.WindingUp)
            {
                // Telegraph: dotted line to the ground and the warning icon under the bird.
                canvas.SetColor(White, (byte)((int)(_clock * 8.0) % 2 == 0 ? 170 : 90));
                for (int y = startY + 8; y < (int)GameModel.GroundY; y += 5)
                {
                    canvas.FillRect(cx, y, 1, 2);
                }

                int wx = cx - GameModel.FlockWarnW / 2;
                int wy = startY - GameModel.FlockWarnH / 2;
                if (bossArt != null && bossArt.FlockWarn != null)
                {
                    canvas.DrawSprite(BossSprites.Loop(bossArt.FlockWarn, strike.Time, BossSprites.WarnFps),
                                      wx, wy, wx + GameModel.FlockWarnW, wy + GameModel.FlockWarnH);
                }
                else
                {
                    canvas.SetColor(GameModel.Gold);
                    canvas.FillRect(wx + 5, wy, 2, 5);
                    canvas.FillRect(wx + 5, wy + 6, 2, 2);
                }

                return;
            }

            foreach (GameModel.Feather feather in strike.Feathers)
            {
                int fx = (int)Math.Round(feather.X);
                int fy = (int)Math.Round(feather.Y);
                int fw = feather.Width;
                int fh = feather.Height;

                if (feather.Landed)
                {
                    int bs = GameModel.FeatherBurstSize;
                    int bx = fx + fw / 2 - bs / 2;
                    int by = fy + fh / 2 - bs / 2;
                    if (bossArt != null && bossArt.FeatherBurst != null)
                    {
                        canvas.DrawSprite(BossSprites.Once(bossArt.FeatherBurst, feather.BurstTime, GameModel.FeatherBurstSeconds),
                                          bx, by, bx + bs, by + bs);
                    }
                    else
                    {
                        double p = feather.BurstTime / GameModel.FeatherBurstSeconds;
                        canvas.SetColor(White, (byte)(200 * (1.0 - p)));
                        int r = 2 + (int)(6 * p);
                        canvas.FillOval(fx + fw / 2 - r, fy + fh / 2 - r, r * 2, r * 2);
                    }

                    continue;
                }

                SpriteFrame[] clip = bossArt == null ? null : (feather.Down ? bossArt.DownFall : bossArt.FeatherFall);
                if (clip != null)
                {
                    canvas.DrawSprite(BossSprites.Loop(clip, _clock + feather.FrameOffset, BossSprites.FeatherFps),
                                      fx, fy, fx + fw, fy + fh);
                }
                else
                {
                    // Placeholder: a small white oval that narrows as it "turns".
                    double turn = Math.Abs(Math.Sin((_clock + feather.FrameOffset) * 6.0));
                    int ow = Math.Max(2, (int)Math.Round(fw * (0.4 + 0.6 * turn)));
                    canvas.SetColor(White);
                    canvas.FillOval(fx + (fw - ow) / 2, fy, ow, fh);
                }
            }
        }

        private void DrawEgg(int x, int y, EggKind kind, bool thrown, double age = 0)
        {
            SpriteFrame frame = thrown ? eggs.Thrown.FrameAt(age, true) : eggs.Falling(kind).FrameAt(age, true);
            canvas.DrawSprite(frame, x, y, x + (int)GameModel.EggW, y + (int)GameModel.EggH);
        }

        private void DrawBasket(GameModel.PlayerState player, Color32 accent, int ammo)
        {
            int x = (int)player.BasketX;
            int y = (int)GameModel.BasketSpriteY;
            int rimY = (int)GameModel.BasketRimY;
            if (player.SpeedTime > 0)
            {
                canvas.SetColor(accent, 130);
                for (int i = 0; i < 4; i++)
                    canvas.FillRect(x - 7 - i * 6, rimY + 5 + (i % 2) * 3, 5, 2);
            }

            // Fill the four front slots first, but paint the back row underneath.
            int backCount = Math.Min(3, Math.Max(0, ammo - 4));
            for (int i = 0; i < backCount; i++)
                DrawEgg(x + 6 + i * 11, rimY - 12, EggKind.Normal, false);
            for (int i = 0; i < Math.Min(4, ammo); i++)
                DrawEgg(x + 1 + i * 11, rimY - 7, EggKind.Normal, false);

            SpriteFrame basket = eggs.BasketIdle;
            if (player.ThrowTime >= 0)
                basket = eggs.BasketThrow.FrameAt(player.ThrowTime, false);
            else if (player.CatchTime >= 0)
                basket = eggs.BasketCatch.FrameAt(player.CatchTime, false);
            canvas.SetShade(SunsetShadeR, SunsetShadeG, SunsetShadeB);
            canvas.DrawSprite(basket, x, y, x + 48, y + 26);
            canvas.ClearShade();

            if (ammo > 7)
                DrawShadowText("+" + (ammo - 7), x + 39, rimY - 17, White, GameModel.Dark, FontTiny);
            if (player.CatchTime >= 0 && player.CatchTime < eggs.CatchPuff.Duration)
            {
                int puffX = (int)Math.Round(player.CatchX) - 8;
                canvas.DrawSprite(eggs.CatchPuff.FrameAt(player.CatchTime, false),
                    puffX, rimY - 3, puffX + 16, rimY + 2);
            }
        }

        /// <summary>A compact animated frame that makes the x3 combo state readable during play.</summary>
        private void DrawFeverAura(double elapsed, int x, int y, Color32 accent)
        {
            int inset  = 4 + (int)(elapsed * 8.0) % 3;
            int left   = x - inset;
            int right  = x + (int)GameModel.BasketW + inset;
            int top    = y - inset;
            int bottom = (int)(GameModel.BasketY + GameModel.BasketH) + inset;

            canvas.SetColor(GameModel.Gold, 190);
            canvas.FillRect(left,  top,    right - left, 2);
            canvas.FillRect(left,  bottom, right - left, 2);
            canvas.FillRect(left,  top,    2, bottom - top);
            canvas.FillRect(right, top,    2, bottom - top);
            canvas.SetColor(accent);
            canvas.FillRect(left + 4,    top - 2,    7, 2);
            canvas.FillRect(right - 10,  bottom + 2, 7, 2);
        }

        private void DrawParticles(GameModel model)
        {
            foreach (GameModel.Particle particle in model.Particles)
            {
                int alpha = (int)(255 * Math.Max(0, particle.Life / particle.MaxLife));
                canvas.SetColor(particle.Color, (byte)Mathf.Clamp(alpha, 0, 255));
                canvas.FillRect((int)particle.X, (int)particle.Y, particle.Size, particle.Size);
            }
        }

        /// <summary>
        /// Draws the cracked shells left by eggs that smashed on the ground.
        /// </summary>
        private void DrawCrackedEggs(GameModel model)
        {
            foreach (GameModel.CrackedEgg egg in model.CrackedEggs)
            {
                DrawCrackedEgg(egg);
            }
        }

        /// <summary>Play the crack once on the egg's original rect, then hold and fade.</summary>
        private void DrawCrackedEgg(GameModel.CrackedEgg egg)
        {
            Color32 tint = GameModel.ColorFor(egg.Kind);
            tint.a = (byte)Mathf.Clamp((int)(255 * Math.Max(0, egg.Life / egg.MaxLife)), 0, 255);
            int x = (int)Math.Round(egg.X);
            int y = (int)Math.Round(egg.Y);
            canvas.DrawSprite(eggs.Crack.FrameAt(egg.MaxLife - egg.Life, false),
                x, y, x + (int)GameModel.EggW, y + (int)GameModel.EggH, tint);
        }

        // ══════════════════════════════════════════════════════════════════════
        // HUD
        // ══════════════════════════════════════════════════════════════════════

        private void DrawSingleHud(GameModel model)
        {
            GameModel.PlayerState player = model.Player(0);
            DrawTopPanel();

            // ── Day 3: hearts with backing box ───────────────────────────────
            // Five hearts span 10..100, so the 1UP/score block sits just past.
            DrawHeartsWithBox(10, 9, player.LivesHalf, GameModel.SingleMaxLivesHalf / 2);

            // ── Day 3: 1UP label above score (classic arcade style) ──────────
            DrawShadowText("1UP",                         110,  9, GameModel.Gold, Black, FontTiny);
            DrawShadowText("SCORE " + player.Score.ToString("D5"), 110, 22, White,  Black, FontSmall);
            DrawShadowText("COMBO " + player.Combo + "  x" + player.Multiplier(),
                110, 30, Muted, Black, FontSmall);

            // ── Day 3: FEVER blink ────────────────────────────────────────────
            if (player.InFever() && (int)(_clock * 3.0) % 2 == 0)
            {
                DrawShadowText("FEVER!", 294, 26, GameModel.Gold, Black, FontSmall);
            }
            // ─────────────────────────────────────────────────────────────────

            DrawCenteredText(model.StageLabel + "  " + model.Defeated + "/" + model.StageGoal,
                240, 14, GameModel.Gold, Black, FontSmall);
            DrawCenteredText("CHAOS LV." + model.DifficultyTier(), 240, 26, Muted, Black, FontTiny);
            DrawShadowText("EGGS " + player.Ammo.ToString("D2"),  391, 14, White, Black, FontSmall);
            DrawShadowText("SPACE TO THROW",                       372, 26, Muted, Black, FontSmall);

            // Shields and the slow-down field only spawn in single player, so
            // their readouts live here rather than on the Duo HUD.
            if (player.ShieldCount > 0)
            {
                DrawShadowText("SHIELD x" + player.ShieldCount, 8, 187,
                    GameModel.Mint, GameModel.Dark, FontTiny);
            }

            if (player.SlowDownTimer > 0)
            {
                DrawShadowText("EGGS SLOW " + Mathf.CeilToInt((float)player.SlowDownTimer), 8, 197,
                    GameModel.Ice, GameModel.Dark, FontTiny);
            }

            DrawEffectBars(player, 8, 202, 120, 5.0);
            if (player.StatusTimer > 0 && model.Phase == Phase.Playing)
            {
                DrawCenteredText(player.StatusText, 240, 197, White, GameModel.Dark, FontSmall);
            }

            DrawFooter(controllerHints ? "STICK/D-PAD MOVE • EAST THROW • START PAUSE" : "A/D OR ARROWS MOVE • SPACE THROW • P PAUSE • ESC MENU");
        }

        private void DrawDuoHud(GameModel model)
        {
            GameModel.PlayerState one = model.Player(0);
            GameModel.PlayerState two = model.Player(1);
            DrawTopPanel();

            // ── Day 3: P1 side — 1UP label + hearts with box ─────────────────
            DrawShadowText("1UP", 7, 9, GameModel.Gold, Black, FontTiny);
            DrawShadowText("P1",  7, 20, GameModel.Cyan, Black, FontSmall);
            DrawHeartsWithBox(28, 9, one.LivesHalf, GameModel.DuoMaxLivesHalf / 2);
            DrawShadowText(one.Score.ToString("D4"), 87, 14, White, Black, FontSmall);
            DrawShadowText("x" + one.Multiplier(),  87, 26, Muted, Black, FontSmall);

            // Centre
            DrawCenteredText("DUO",                           240, 14, GameModel.Gold, Black, FontSmall);
            DrawCenteredText("CHAOS LV." + model.DifficultyTier(), 240, 26, Muted, Black, FontTiny);

            // ── Day 3: P2 side — 2UP label + hearts with box ─────────────────
            DrawShadowText(two.Score.ToString("D4"), 337, 14, White,          Black, FontSmall);
            DrawShadowText("x" + two.Multiplier(),  369, 26, Muted,          Black, FontSmall);
            DrawHeartsWithBox(401, 9, two.LivesHalf, GameModel.DuoMaxLivesHalf / 2);
            DrawShadowText("P2",  455, 20, GameModel.Pink, Black, FontSmall);
            DrawShadowText("2UP", 455,  9, GameModel.Gold, Black, FontTiny);
            // ─────────────────────────────────────────────────────────────────

            if (one.StatusTimer > 0 && model.Phase == Phase.Playing)
            {
                DrawCenteredText(one.StatusText, 120, 194, White, GameModel.Dark, FontTiny);
            }

            if (two.StatusTimer > 0 && model.Phase == Phase.Playing)
            {
                DrawCenteredText(two.StatusText, 360, 194, White, GameModel.Dark, FontTiny);
            }

            DrawEffectLabel(one, 120, 207, GameModel.Cyan);
            DrawEffectLabel(two, 360, 207, GameModel.Pink);
            DrawFooter(controllerHints ? "P1 STICK/D-PAD • P2 ARROWS • START PAUSE" : "P1 A/D • P2 ARROWS • P PAUSE • ESC MENU");
        }

        private void DrawTopPanel()
        {
            canvas.SetColor(Panel);
            canvas.FillRect(0, 0, GameModel.WorldW, 33);
            canvas.SetColor(255, 255, 255, 28);
            canvas.FillRect(0, 31, GameModel.WorldW, 2);
        }

        /// <summary>
        /// The heart group sits directly on the top panel. The Day 3 backing box
        /// was dropped once the hearts gained their own dark silhouette and
        /// border, which give them all the contrast they need. Draws the mode's
        /// full heart row: five hearts in single player, three in Duo, with
        /// empty hearts staying visible as lives are lost.
        /// </summary>
        private void DrawHeartsWithBox(int startX, int y, int halfUnits, int maxHearts)
        {
            DrawHearts(startX, y, halfUnits, maxHearts);
        }

        private void DrawHearts(int startX, int y, int halfUnits, int maxHearts)
        {
            for (int i = 0; i < maxHearts; i++)
            {
                double fill = Math.Max(0, Math.Min(1, (halfUnits - i * 2) / 2.0));
                DrawHeart(startX + i * 18, y, fill);
            }
        }

        /// <summary>
        /// Day 3: the speed bar flashes on its final second to warn the player.
        /// </summary>
        /// <summary>
        /// One countdown bar per active power-up, stacked upward from
        /// <paramref name="bottomY"/> so the list grows away from the basket.
        /// Timed effects drain over their full duration and flash in their
        /// last second; shields have no timer, so they show as a full bar with
        /// the count. Colours match the egg that granted each effect.
        /// </summary>
        private void DrawEffectBars(GameModel.PlayerState player, int x, int bottomY, int width, double speedSeconds)
        {
            int y = bottomY;
            y = DrawEffectRow(x, y, width, player.SpeedTime,     speedSeconds, GameModel.ColorFor(EggKind.Speed),    "SPEED");
            y = DrawEffectRow(x, y, width, player.SlowDownTimer, 5.0,          GameModel.ColorFor(EggKind.SlowDown), "SLOW EGGS");
            y = DrawEffectRow(x, y, width, player.FreezeTime,    2.0,          GameModel.ColorFor(EggKind.Freeze),   "FROZEN");
            y = DrawEffectRow(x, y, width, player.ReverseTime,   3.0,          GameModel.ColorFor(EggKind.Reverse),  "REVERSED");
            y = DrawEffectRow(x, y, width, player.SabotageTime,  5.0,          GameModel.ColorFor(EggKind.Golden),   "EGG STORM");

            if (player.ShieldCount > 0)
            {
                Color32 mint = GameModel.ColorFor(EggKind.Shield);
                canvas.SetColor(GameModel.Dark);
                canvas.FillRect(x, y, width, 7);
                canvas.SetColor(mint);
                canvas.FillRect(x + 2, y + 2, width - 4, 3);
                DrawShadowText("SHIELD x" + player.ShieldCount, x + width + 6, y + 6, mint, GameModel.Dark, FontTiny);
            }
        }

        /// <summary>
        /// Draws one bar if the effect is running and returns the row above it.
        /// A bar that is blinked off this frame still takes its row, so the
        /// stack never shuffles while something flashes.
        /// </summary>
        private int DrawEffectRow(int x, int y, int width, double remaining, double total, Color32 color, string label)
        {
            if (remaining <= 0)
            {
                return y;
            }

            // Flash at 4 Hz when less than 1 second remains
            bool blinkedOff = remaining < 1.0 && (int)(_clock * 4.0) % 2 != 0;
            if (!blinkedOff)
            {
                int fill = (int)Math.Round((width - 4) * Math.Min(1.0, remaining / total));
                canvas.SetColor(GameModel.Dark);
                canvas.FillRect(x, y, width, 7);
                canvas.SetColor(color);
                canvas.FillRect(x + 2, y + 2, Math.Max(1, fill), 3);
                DrawShadowText(label, x + width + 6, y + 6, color, GameModel.Dark, FontTiny);
            }

            return y - 10;
        }

        /// <summary>
        /// Day 3: power-effect labels flash at ~2.5 Hz while active and use
        /// the canonical colour for each effect type.
        /// </summary>
        private void DrawEffectLabel(GameModel.PlayerState player, int centerX, int y, Color32 accent)
        {
            string  label      = "";
            Color32 labelColor = accent;

            if (player.FreezeTime > 0)
            {
                label      = "FROZEN";
                labelColor = GameModel.Ice;
            }
            else if (player.ReverseTime > 0)
            {
                label      = "REVERSED";
                labelColor = GameModel.Purple;
            }
            else if (player.SabotageTime > 0)
            {
                label      = "EGG STORM";
                labelColor = GameModel.Gold;
            }
            else if (player.SpeedTime > 0)
            {
                label      = "SPEED";
                labelColor = GameModel.Cyan;
            }

            // Blink at ~2.5 Hz
            if (label.Length > 0 && (int)(_clock * 2.5) % 2 == 0)
            {
                DrawCenteredText(label, centerX, y, labelColor, GameModel.Dark, FontTiny);
            }
        }

        private void DrawFooter(string controls)
        {
            canvas.SetColor(20, 34, 44, 220);
            canvas.FillRect(0, 260, GameModel.WorldW, 10);
            DrawCenteredText(controls, 240, 268, Muted, Black, FontTiny);
        }

        private void DrawHeart(int x, int y, double fill)
        {
            if (hearts != null)
            {
                DrawSpriteHeart(x, y, fill);
                return;
            }

            int[] xs = { x, x + 3, x + 7, x + 11, x + 14, x + 14, x + 7, x,     x };
            int[] ys = { y + 3, y, y + 3, y,     y + 3,  y + 7,  y + 14, y + 7, y + 3 };
            canvas.SetColor(84, 96, 102);
            canvas.FillPolygon(xs, ys, 9);
            if (fill > 0)
            {
                canvas.SetClip(x, y, (int)Math.Round(15 * fill), 15);
                canvas.SetColor(GameModel.Pink);
                canvas.FillPolygon(xs, ys, 9);
                canvas.ClearClip();
            }

            canvas.SetColor(GameModel.Dark);
            canvas.DrawPolygon(xs, ys, 9);
        }

        /// <summary>
        /// The layered artwork version of a heart. The dark background is always
        /// there; the red heart is clipped to its left <paramref name="fill"/>
        /// fraction so a half heart keeps its left lobe and shows the background
        /// on the right; and the border goes on last so the outline stays
        /// whole across the cut.
        /// </summary>
        private void DrawSpriteHeart(int x, int y, double fill)
        {
            int w = hearts.Width;
            int h = hearts.Height;

            // The 17px art is two rows taller than the drawn heart was; lifting
            // it one row keeps it centred inside the existing backing box.
            int top = y - 1;

            canvas.DrawSprite(hearts.Background, x, top, x + w, top + h);

            if (fill > 0)
            {
                // Ceiling so a half heart keeps the centre column and reads as
                // a proper half rather than a sliver.
                int keep = (int)Math.Ceiling(w * fill);
                canvas.SetClip(x, top, keep, h);
                canvas.DrawSprite(hearts.Full, x, top, x + w, top + h);
                canvas.ClearClip();
            }

            canvas.DrawSprite(hearts.Border, x, top, x + w, top + h);
        }

        private void DrawResultOverlay(GameModel model)
        {
            const int rx = 82, ry = 72, rw = 316, rh = 142;
            canvas.SetColor(15, 29, 39, 236);
            canvas.FillRect(rx, ry, rw, rh);

            Color32 accent;
            string  heading;
            string  detail;

            if (model.Mode == Mode.Single)
            {
                bool won = model.Phase == Phase.Won;
                accent  = won ? GameModel.Gold  : GameModel.Pink;
                heading = won ? "ALL STAGES CLEARED!" : "BASKET BROKEN";
                detail  = won
                    ? "STAGE 1  •  STAGE 2  •  BOSS BEATEN"
                    : "RAN OUT OF HEARTS ON " + model.StageLabel;
            }
            else if (model.WinnerPlayer == 0)
            {
                accent  = GameModel.Gold;
                heading = "DRAW!";
                detail  = "BOTH BASKETS CRACKED TOGETHER";
            }
            else
            {
                accent  = model.WinnerPlayer == 1 ? GameModel.Cyan : GameModel.Pink;
                heading = "PLAYER " + model.WinnerPlayer + " WINS!";
                detail  = "THE OTHER BASKET LOST ALL 3 HEARTS";
            }

            // ── Day 3: corner-bracket decoration ─────────────────────────────
            const int blen = 14;
            canvas.SetColor(accent);
            // top-left
            canvas.FillRect(rx,            ry,            blen, 3);
            canvas.FillRect(rx,            ry,            3,    blen);
            // top-right
            canvas.FillRect(rx + rw - blen, ry,            blen, 3);
            canvas.FillRect(rx + rw - 3,    ry,            3,    blen);
            // bottom-left
            canvas.FillRect(rx,            ry + rh - 3,   blen, 3);
            canvas.FillRect(rx,            ry + rh - blen, 3,   blen);
            // bottom-right
            canvas.FillRect(rx + rw - blen, ry + rh - 3,   blen, 3);
            canvas.FillRect(rx + rw - 3,    ry + rh - blen, 3,   blen);
            // ─────────────────────────────────────────────────────────────────

            // ── Day 3: blinking GAME OVER on single-player loss ───────────────
            if (model.Mode == Mode.Single && model.Phase == Phase.Lost
                && (int)(_clock * 1.8) % 2 == 0)
            {
                DrawCenteredText("GAME OVER", 240, 100, GameModel.Pink, Black, FontMedium);
            }
            // ─────────────────────────────────────────────────────────────────

            DrawCenteredText(heading, 240, 117, accent, Black, FontMedium);

            if (model.Mode == Mode.Single)
            {
                DrawCenteredText("FINAL SCORE  " + model.Player(0).Score.ToString("D5"),
                    240, 141, White, Black, FontSmall);
            }
            else
            {
                DrawCenteredText(
                    "P1 " + model.Player(0).Score.ToString("D4") + "     •     P2 " +
                    model.Player(1).Score.ToString("D4"),
                    240, 141, White, Black, FontSmall);
            }

            DrawCenteredText(detail,                       240, 158, Muted,  Black, FontTiny);
            DrawCenteredText(controllerHints ? "SOUTH REMATCH • EAST MENU" : "ENTER REMATCH • ESC MENU", 240, 187, White, Black, FontSmall);
            DrawCenteredText(controllerHints ? "WEST ALSO RESTARTS" : "R ALSO RESTARTS", 240, 201, Muted, Black, FontTiny);
        }

        private void DrawPauseOverlay(AudioManager audio, int selection)
        {
            canvas.SetColor(0, 0, 0, 150);
            canvas.FillRect(0, 0, GameModel.WorldW, GameModel.WorldH);
            canvas.SetColor(15, 29, 39, 245);
            canvas.FillRect(80, 48, 320, 184);
            DrawCenteredText("PAUSED", 240, 76, White, Black, FontLarge);
            for (int i = 0; i < PauseOptions.Length; i++)
            {
                int y = 88 + i * 24;
                bool selected = i == selection;
                canvas.SetColor(selected ? GameModel.Cyan : Muted, selected ? (byte)70 : (byte)18);
                canvas.FillRect(104, y, 272, 21);
                DrawCenteredText((selected ? "> " : "") + PauseOptions[i], 240, y + 16,
                    selected ? White : Muted, Black, FontSmall);
            }
            DrawCenteredText(controllerHints ? "D-PAD SELECT • SOUTH CONFIRM • EAST RESUME" : "UP/DOWN SELECT • ENTER CONFIRM • P RESUME",
                240, 200, Muted, Black, FontTiny);
            DrawCenteredText(controllerHints ? "WEST RESTART • SELECT MUTE • LB/RB VOLUME" : "R RESTART • M MUTE • -/+ VOLUME",
                240, 212, Muted, Black, FontTiny);
            DrawCenteredText(audio.IsMuted() ? "AUDIO MUTED" : "AUDIO " + Mathf.RoundToInt(audio.GetVolume() * 100) + "%",
                240, 224, GameModel.Gold, Black, FontTiny);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Day 3: CRT scanline post-process
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Draws a very faint dark stripe over every other horizontal row,
        /// simulating the line structure of a cathode-ray tube screen.
        /// Alpha 38/255 ≈ 15% — visible but never obscures art or text.
        /// Called last in Render() so it sits on top of every other layer.
        /// </summary>
        private void DrawScanlines()
        {
            canvas.SetColor(0, 0, 0, 38);
            for (int y = 0; y < GameModel.WorldH; y += 2)
            {
                canvas.FillRect(0, y, GameModel.WorldW, 1);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Text helpers
        // ══════════════════════════════════════════════════════════════════════

        private void DrawShadowText(
            string text, int x, int baselineY,
            Color32 color, Color32 shadow, PixelFont.Style style)
        {
            if (style.ShadowOffset > 0)
            {
                canvas.SetColor(shadow);
                PixelFont.Draw(canvas, text, x + style.ShadowOffset, baselineY + style.ShadowOffset, style);
            }

            canvas.SetColor(color);
            PixelFont.Draw(canvas, text, x, baselineY, style);
        }

        private void DrawCenteredText(
            string text, int centerX, int baselineY,
            Color32 color, Color32 shadow, PixelFont.Style style)
        {
            int x = centerX - PixelFont.TextWidth(text, style) / 2;
            DrawShadowText(text, x, baselineY, color, shadow, style);
        }
    }
}
