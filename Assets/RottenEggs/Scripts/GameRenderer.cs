using System;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Draws the whole game into the pixel canvas, port for port from the Java
    /// prototype's Java2D rendering. Nothing here touches game rules: it reads
    /// the model and paints it, exactly as the original panel did.
    /// </summary>
    public sealed class GameRenderer
    {
        private static readonly Color32 Sky = new Color32(126, 213, 247, 255);
        private static readonly Color32 SkyDot = new Color32(155, 224, 245, 255);
        private static readonly Color32 Cloud = new Color32(255, 241, 246, 255);
        private static readonly Color32 Grass = new Color32(82, 172, 91, 255);
        private static readonly Color32 GrassDark = new Color32(47, 123, 70, 255);
        private static readonly Color32 Soil = new Color32(194, 119, 63, 255);
        private static readonly Color32 SoilDark = new Color32(132, 75, 48, 255);
        private static readonly Color32 Wood = new Color32(150, 92, 52, 255);
        private static readonly Color32 WoodLight = new Color32(196, 129, 74, 255);
        private static readonly Color32 WoodDark = new Color32(102, 58, 36, 255);
        private static readonly Color32 Basket = new Color32(144, 82, 48, 255);
        private static readonly Color32 BasketLight = new Color32(207, 132, 74, 255);
        private static readonly Color32 Panel = new Color32(24, 37, 48, 226);
        private static readonly Color32 White = new Color32(255, 250, 240, 255);
        private static readonly Color32 Muted = new Color32(182, 218, 226, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        /// <summary>The chicken frames are 20 x 21, drawn at whole-number scale like the rest.</summary>
        private const int SpriteScale = 2;

        private static readonly PixelFont.Style FontTiny = new PixelFont.Style(1, 1, 0);
        private static readonly PixelFont.Style FontSmall = new PixelFont.Style(1, 2, 0);
        private static readonly PixelFont.Style FontOption = new PixelFont.Style(2, 2, 1);
        private static readonly PixelFont.Style FontMedium = new PixelFont.Style(2, 2, 1);
        private static readonly PixelFont.Style FontLarge = new PixelFont.Style(3, 3, 2);

        private readonly PixelCanvas canvas;
        private readonly ChickenSprites sprites;

        public GameRenderer(PixelCanvas canvas, ChickenSprites sprites)
        {
            this.canvas = canvas;
            this.sprites = sprites;
        }

        /// <summary>Paints one complete frame of the game.</summary>
        public void Render(GameModel model, AudioManager audio, double menuClock, int menuSelection)
        {
            canvas.ResetTranslate();
            canvas.ClearClip();
            DrawBackground();

            if (model.Phase == Phase.Menu)
            {
                DrawMenuScene(menuClock);
                DrawMenuOverlay(audio, menuSelection);
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
        }

        private void DrawBackground()
        {
            canvas.SetColor(Sky);
            canvas.FillRect(0, 0, GameModel.WorldW, GameModel.WorldH);
            canvas.SetColor(SkyDot);
            for (int y = 35; y < 205; y += 18)
            {
                for (int x = (y / 18 % 2) * 9; x < GameModel.WorldW; x += 18)
                {
                    canvas.FillRect(x, y, 2, 2);
                }
            }

            DrawCloud(20, 42, 1);
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

            DrawFlower(31, 219, GameModel.Pink);
            DrawFlower(450, 220, GameModel.Gold);
            DrawFlower(420, 226, White);
        }

        private void DrawMenuScene(double menuClock)
        {
            int menuPerchY = (int)(43 + GameModel.ChickenH);
            DrawPerch(47, 103, menuPerchY);
            DrawPerch(377, 433, menuPerchY);
            DrawChicken(75, 43, AnimState.Idle, menuClock, true);
            DrawChicken(405, 43, AnimState.Idle, menuClock + 0.25, false);
            DrawEgg(60, 115, EggKind.Normal, false);
            DrawEgg(415, 137, EggKind.Golden, false);
            DrawBasket(84, 216, GameModel.Cyan, 0, false);
            DrawBasket(348, 216, GameModel.Pink, 0, false);
        }

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
                DrawChicken(chicken.CenterX, chicken.Y, chicken.Anim, chicken.AnimTime, chicken.Facing > 0);
                if (chicken.Alive())
                {
                    DrawHealthPips(chicken.Hp, chicken.CenterX, (int)chicken.Y - 8);
                }
                else
                {
                    DrawCenteredText("KO", (int)Math.Round(chicken.CenterX), (int)chicken.Y + 20,
                        White, GameModel.Dark, FontSmall);
                }
            }

            foreach (GameModel.Shot shot in model.Shots)
            {
                canvas.SetColor(255, 255, 255, 120);
                canvas.FillRect((int)shot.X + 2, (int)shot.Y + 8, 3, 6);
                DrawEgg((int)shot.X, (int)shot.Y, EggKind.Normal, true);
            }

            GameModel.PlayerState player = model.Player(0);
            DrawBasket((int)player.BasketX, (int)GameModel.BasketY,
                GameModel.Pink, player.Ammo, player.SpeedTime > 0);
            if (player.InFever())
            {
                DrawFeverAura(model.Elapsed, (int)player.BasketX, (int)GameModel.BasketY, GameModel.Pink);
            }
        }

        private void DrawDuoWorld(GameModel model)
        {
            canvas.SetColor(255, 255, 255, 115);
            for (int y = 38; y < 258; y += 12)
            {
                canvas.FillRect(239, y, 2, 7);
            }

            DrawFallingEggs(model);
            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                DrawPerch(chicken);
            }

            foreach (GameModel.Chicken chicken in model.Chickens)
            {
                DrawChicken(chicken.CenterX, chicken.Y, chicken.Anim, chicken.AnimTime, chicken.Facing > 0);
            }

            GameModel.PlayerState one = model.Player(0);
            GameModel.PlayerState two = model.Player(1);
            DrawBasket((int)one.BasketX, (int)GameModel.BasketY,
                GameModel.Cyan, 0, one.SpeedTime > 0);
            DrawBasket((int)two.BasketX, (int)GameModel.BasketY,
                GameModel.Pink, 0, two.SpeedTime > 0);
            if (one.InFever())
            {
                DrawFeverAura(model.Elapsed, (int)one.BasketX, (int)GameModel.BasketY, GameModel.Cyan);
            }

            if (two.InFever())
            {
                DrawFeverAura(model.Elapsed, (int)two.BasketX, (int)GameModel.BasketY, GameModel.Pink);
            }
        }

        private void DrawFallingEggs(GameModel model)
        {
            foreach (GameModel.FallingEgg egg in model.FallingEggs)
            {
                DrawEgg((int)Math.Round(egg.X), (int)Math.Round(egg.Y), egg.Kind, false);
            }
        }

        private void DrawCloud(int x, int y, int large)
        {
            canvas.SetColor(80, 154, 190);
            canvas.FillRect(x + 4, y + 7, 36 + large * 10, 7);
            canvas.SetColor(Cloud);
            canvas.FillRect(x + 3, y + 5, 40 + large * 10, 7);
            canvas.FillRect(x + 9, y + 1, 12, 7);
            canvas.FillRect(x + 22, y - 3, 14 + large * 6, 11);
            canvas.FillRect(x + 34 + large * 5, y + 1, 10, 7);
        }

        private void DrawFlower(int x, int y, Color32 petals)
        {
            canvas.SetColor(GrassDark);
            canvas.FillRect(x, y - 8, 2, 10);
            canvas.FillRect(x - 3, y - 4, 4, 2);
            canvas.SetColor(petals);
            canvas.FillRect(x - 3, y - 12, 3, 3);
            canvas.FillRect(x + 2, y - 12, 3, 3);
            canvas.FillRect(x, y - 15, 3, 3);
            canvas.FillRect(x, y - 9, 3, 3);
            canvas.SetColor(GameModel.Gold);
            canvas.FillRect(x, y - 12, 3, 3);
        }

        /// <summary>
        /// Draws one chicken from its sheet clip. The clip advances with the
        /// model's own animation clock, so a one-shot clip such as the death
        /// sequence stops on its final frame instead of looping forever.
        /// </summary>
        private void DrawChicken(double centerX, double topY, AnimState state, double animTime, bool facingRight)
        {
            SpriteFrame frame = sprites.Animate(state).FrameAt(animTime, state.Loops());
            int drawWidth = frame.Width * SpriteScale;
            int drawHeight = frame.Height * SpriteScale;
            int x = (int)Math.Round(centerX) - drawWidth / 2;
            int y = (int)Math.Round(topY);
            // The artwork faces right, so a left-bound chicken is mirrored in place.
            int nearX = facingRight ? x : x + drawWidth;
            int farX = facingRight ? x + drawWidth : x;
            canvas.DrawSprite(frame, nearX, y, farX, y + drawHeight);
        }

        /// <summary>Wooden perch a chicken patrols along, drawn under its whole lane.</summary>
        private void DrawPerch(GameModel.Chicken chicken)
        {
            DrawPerch(
                (int)Math.Round(chicken.MinX - GameModel.PerchMargin),
                (int)Math.Round(chicken.MaxX + GameModel.PerchMargin),
                (int)Math.Round(chicken.Y + GameModel.ChickenH));
        }

        private void DrawPerch(int left, int right, int top)
        {
            int width = right - left;
            int height = (int)GameModel.PerchH;
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
            canvas.FillRect(left + 4, top + height, 4, 4);
            canvas.FillRect(right - 8, top + height, 4, 4);
            canvas.SetColor(WoodDark);
            canvas.FillRect(left + 5, top + height, 2, 3);
            canvas.FillRect(right - 7, top + height, 2, 3);
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

        private void DrawEgg(int x, int y, EggKind kind, bool thrown)
        {
            Color32 shell = GameModel.ColorFor(kind);
            canvas.SetColor(GameModel.Dark);
            canvas.FillOval(x - 1, y - 1, 9, 11);
            canvas.SetColor(shell);
            canvas.FillOval(x, y, 7, 9);
            canvas.SetColor(White);
            canvas.FillRect(x + 2, y + 1, 2, 2);

            canvas.SetColor(GameModel.Dark);
            switch (kind)
            {
                case EggKind.Speed:
                    canvas.FillPolygon(
                        new[] { x + 4, x + 2, x + 4, x + 3, x + 6 },
                        new[] { y + 1, y + 5, y + 5, y + 8, y + 4 }, 5);
                    break;
                case EggKind.Freeze:
                    canvas.FillRect(x + 3, y + 2, 1, 5);
                    canvas.FillRect(x + 1, y + 4, 5, 1);
                    break;
                case EggKind.Reverse:
                    canvas.FillRect(x + 1, y + 3, 4, 1);
                    canvas.FillRect(x + 1, y + 3, 1, 3);
                    canvas.FillRect(x + 4, y + 5, 2, 1);
                    break;
                case EggKind.Golden:
                    canvas.FillRect(x + 2, y + 3, 3, 3);
                    break;
                case EggKind.Normal:
                    if (thrown)
                    {
                        canvas.SetColor(GameModel.Pink);
                        canvas.FillRect(x + 2, y + 5, 3, 2);
                    }

                    break;
            }
        }

        private void DrawBasket(int x, int y, Color32 accent, int ammo, bool boosted)
        {
            if (boosted)
            {
                canvas.SetColor(accent, 130);
                for (int i = 0; i < 4; i++)
                {
                    canvas.FillRect(x - 7 - i * 6, y + 5 + (i % 2) * 3, 5, 2);
                }
            }

            canvas.SetColor(accent);
            canvas.FillRect(x - 3, y, 54, 3);
            canvas.SetColor(GameModel.Dark);
            canvas.FillPolygon(new[] { x - 2, x + 50, x + 44, x + 4 },
                new[] { y + 2, y + 2, y + 17, y + 17 }, 4);
            canvas.SetColor(Basket);
            canvas.FillPolygon(new[] { x, x + 48, x + 42, x + 6 },
                new[] { y + 3, y + 3, y + 15, y + 15 }, 4);
            canvas.SetColor(BasketLight);
            canvas.FillRect(x + 3, y + 6, 42, 3);
            canvas.FillRect(x + 5, y + 11, 38, 2);
            canvas.SetColor(GameModel.Dark);
            for (int i = 0; i < 5; i++)
            {
                canvas.FillRect(x + 7 + i * 8, y + 4, 2, 11);
            }

            canvas.DrawArc(x + 10, y - 10, 28, 22, 180, -180, 2);

            int visibleAmmo = Math.Min(5, ammo);
            for (int i = 0; i < visibleAmmo; i++)
            {
                DrawEgg(x + 7 + i * 8, y - 1 - (i % 2) * 2, EggKind.Normal, false);
            }

            if (ammo > 5)
            {
                DrawShadowText("+" + (ammo - 5), x + 39, y - 5, White, GameModel.Dark, FontTiny);
            }
        }

        /// <summary>A compact animated frame that makes the x3 combo state readable during play.</summary>
        private void DrawFeverAura(double elapsed, int x, int y, Color32 accent)
        {
            int inset = 4 + (int)(elapsed * 8.0) % 3;
            int left = x - inset;
            int right = x + (int)GameModel.BasketW + inset;
            int top = y - inset;
            int bottom = y + (int)GameModel.BasketH + inset;

            canvas.SetColor(GameModel.Gold, 190);
            canvas.FillRect(left, top, right - left, 2);
            canvas.FillRect(left, bottom, right - left, 2);
            canvas.FillRect(left, top, 2, bottom - top);
            canvas.FillRect(right, top, 2, bottom - top);
            canvas.SetColor(accent);
            canvas.FillRect(left + 4, top - 2, 7, 2);
            canvas.FillRect(right - 10, bottom + 2, 7, 2);
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

        private void DrawSingleHud(GameModel model)
        {
            GameModel.PlayerState player = model.Player(0);
            DrawTopPanel();
            DrawHearts(10, 9, player.LivesHalf);
            DrawShadowText("SCORE " + player.Score.ToString("D5"), 72, 14, White, Black, FontSmall);
            DrawShadowText("COMBO " + player.Combo + "  x" + player.Multiplier(), 72, 26, Muted, Black, FontSmall);
            if (player.InFever())
            {
                DrawShadowText("FEVER!", 294, 26, GameModel.Gold, Black, FontSmall);
            }
            DrawCenteredText("COOP " + model.Defeated + "/3", 240, 14, GameModel.Gold, Black, FontSmall);
            DrawCenteredText("CHAOS LV." + model.DifficultyTier(), 240, 26, Muted, Black, FontTiny);
            DrawShadowText("EGGS " + player.Ammo.ToString("D2"), 391, 14, White, Black, FontSmall);
            DrawShadowText("SPACE TO THROW", 372, 26, Muted, Black, FontSmall);

            DrawEffectBar(player, 8, 202, 120, GameModel.Pink);
            if (player.StatusTimer > 0 && model.Phase == Phase.Playing)
            {
                DrawCenteredText(player.StatusText, 240, 197, White, GameModel.Dark, FontSmall);
            }

            DrawFooter("A/D OR ARROWS MOVE  •  SPACE THROW  •  ESC MENU  •  M AUDIO");
        }

        private void DrawDuoHud(GameModel model)
        {
            GameModel.PlayerState one = model.Player(0);
            GameModel.PlayerState two = model.Player(1);
            DrawTopPanel();
            DrawShadowText("P1", 7, 14, GameModel.Cyan, Black, FontSmall);
            DrawHearts(28, 9, one.LivesHalf);
            DrawShadowText(one.Score.ToString("D4"), 87, 14, White, Black, FontSmall);
            DrawShadowText("x" + one.Multiplier(), 87, 26, Muted, Black, FontSmall);

            DrawCenteredText("DUO", 240, 14, GameModel.Gold, Black, FontSmall);
            DrawCenteredText("CHAOS LV." + model.DifficultyTier(), 240, 26, Muted, Black, FontTiny);

            DrawShadowText(two.Score.ToString("D4"), 337, 14, White, Black, FontSmall);
            DrawShadowText("x" + two.Multiplier(), 369, 26, Muted, Black, FontSmall);
            DrawHearts(401, 9, two.LivesHalf);
            DrawShadowText("P2", 455, 14, GameModel.Pink, Black, FontSmall);

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
            DrawFooter("P1  A / D     •     P2  ← / →     •     ESC MENU     •     M AUDIO");
        }

        private void DrawTopPanel()
        {
            canvas.SetColor(Panel);
            canvas.FillRect(0, 0, GameModel.WorldW, 33);
            canvas.SetColor(255, 255, 255, 28);
            canvas.FillRect(0, 31, GameModel.WorldW, 2);
        }

        private void DrawHearts(int startX, int y, int halfUnits)
        {
            for (int i = 0; i < 3; i++)
            {
                double fill = Math.Max(0, Math.Min(1, (halfUnits - i * 2) / 2.0));
                DrawHeart(startX + i * 18, y, fill);
            }
        }

        private void DrawEffectBar(GameModel.PlayerState player, int x, int y, int width, Color32 accent)
        {
            if (player.SpeedTime <= 0)
            {
                return;
            }

            int fill = (int)Math.Round((width - 4) * player.SpeedTime / 5.0);
            canvas.SetColor(GameModel.Dark);
            canvas.FillRect(x, y, width, 7);
            canvas.SetColor(accent);
            canvas.FillRect(x + 2, y + 2, Math.Max(1, fill), 3);
            DrawShadowText("SPEED", x + width + 6, y + 6, GameModel.Cyan, GameModel.Dark, FontTiny);
        }

        private void DrawEffectLabel(GameModel.PlayerState player, int centerX, int y, Color32 accent)
        {
            string label = "";
            if (player.FreezeTime > 0)
            {
                label = "FROZEN";
            }
            else if (player.ReverseTime > 0)
            {
                label = "REVERSED";
            }
            else if (player.SabotageTime > 0)
            {
                label = "EGG STORM";
            }
            else if (player.SpeedTime > 0)
            {
                label = "SPEED";
            }

            if (label.Length > 0)
            {
                DrawCenteredText(label, centerX, y, accent, GameModel.Dark, FontTiny);
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
            int[] xs = { x, x + 3, x + 7, x + 11, x + 14, x + 14, x + 7, x, x };
            int[] ys = { y + 3, y, y + 3, y, y + 3, y + 7, y + 14, y + 7, y + 3 };
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

        private void DrawMenuOverlay(AudioManager audio, int menuSelection)
        {
            canvas.SetColor(15, 29, 39, 232);
            canvas.FillRect(64, 38, 352, 190);
            canvas.SetColor(GameModel.Pink);
            canvas.FillRect(64, 38, 352, 4);
            canvas.FillRect(64, 224, 352, 4);
            canvas.FillRect(64, 38, 4, 190);
            canvas.FillRect(412, 38, 4, 190);

            DrawCenteredText("ROTTEN EGGS", 240, 75, White, Black, FontLarge);
            DrawCenteredText("UNITY EDITION", 240, 91, GameModel.Cyan, Black, FontSmall);
            DrawMenuOption(92, 105, 296, 43, 0, menuSelection,
                "1  SINGLE PLAYER", "CATCH • THROW • LEAD MOVING TARGETS");
            DrawMenuOption(92, 154, 296, 43, 1, menuSelection,
                "2  DUO PLAYER", "P1 A/D • P2 ARROWS • POWER-EGG SABOTAGE");

            string audioText = audio.IsMuted()
                ? "AUDIO MUTED"
                : "AUDIO " + Mathf.RoundToInt(audio.GetVolume() * 100) + "%";
            DrawCenteredText("↑/↓ OR W/S SELECT  •  ENTER START", 240, 210, Muted, Black, FontTiny);
            DrawCenteredText("M MUTE  •  -/+ VOLUME  •  " + audioText, 240, 220,
                audio.IsMuted() ? GameModel.Pink : GameModel.Gold, Black, FontTiny);
        }

        private void DrawMenuOption(
            int x,
            int y,
            int width,
            int height,
            int option,
            int menuSelection,
            string title,
            string detail)
        {
            bool selected = menuSelection == option;
            Color32 accent = option == 0 ? GameModel.Cyan : GameModel.Pink;
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
            canvas.FillRect(x, y, width, 2);
            canvas.FillRect(x, y + height - 2, width, 2);
            canvas.FillRect(x, y, 2, height);
            canvas.FillRect(x + width - 2, y, 2, height);
            if (selected)
            {
                canvas.FillPolygon(new[] { x + 10, x + 16, x + 10 }, new[] { y + 13, y + 18, y + 23 }, 3);
            }

            DrawCenteredText(title, x + width / 2, y + 20,
                selected ? White : Muted, Black, FontOption);
            DrawCenteredText(detail, x + width / 2, y + 36,
                selected ? accent : new Color32(133, 165, 174, 255), Black, FontTiny);
        }

        private void DrawResultOverlay(GameModel model)
        {
            canvas.SetColor(15, 29, 39, 236);
            canvas.FillRect(82, 72, 316, 142);
            Color32 accent;
            string heading;
            string detail;

            if (model.Mode == Mode.Single)
            {
                bool won = model.Phase == Phase.Won;
                accent = won ? GameModel.Gold : GameModel.Pink;
                heading = won ? "COOP CLEARED!" : "BASKET BROKEN";
                detail = won ? "ALL 3 CHICKENS DEFEATED" : "SIX CRACKS USED ALL 3 HEARTS";
            }
            else if (model.WinnerPlayer == 0)
            {
                accent = GameModel.Gold;
                heading = "DRAW!";
                detail = "BOTH BASKETS CRACKED TOGETHER";
            }
            else
            {
                accent = model.WinnerPlayer == 1 ? GameModel.Cyan : GameModel.Pink;
                heading = "PLAYER " + model.WinnerPlayer + " WINS!";
                detail = "THE OTHER BASKET LOST ALL 3 HEARTS";
            }

            canvas.SetColor(accent);
            canvas.FillRect(82, 72, 316, 4);
            canvas.FillRect(82, 210, 316, 4);
            DrawCenteredText(heading, 240, 111, accent, Black, FontMedium);
            if (model.Mode == Mode.Single)
            {
                DrawCenteredText("FINAL SCORE  " + model.Player(0).Score.ToString("D5"),
                    240, 137, White, Black, FontSmall);
            }
            else
            {
                DrawCenteredText(
                    "P1 " + model.Player(0).Score.ToString("D4") + "     •     P2 " +
                    model.Player(1).Score.ToString("D4"),
                    240, 137, White, Black, FontSmall);
            }

            DrawCenteredText(detail, 240, 154, Muted, Black, FontTiny);
            DrawCenteredText("ENTER REMATCH  •  ESC MENU", 240, 187, White, Black, FontSmall);
            DrawCenteredText("R ALSO RESTARTS", 240, 201, Muted, Black, FontTiny);
        }

        private void DrawShadowText(
            string text,
            int x,
            int baselineY,
            Color32 color,
            Color32 shadow,
            PixelFont.Style style)
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
            string text,
            int centerX,
            int baselineY,
            Color32 color,
            Color32 shadow,
            PixelFont.Style style)
        {
            int x = centerX - PixelFont.TextWidth(text, style) / 2;
            DrawShadowText(text, x, baselineY, color, shadow, style);
        }
    }
}
