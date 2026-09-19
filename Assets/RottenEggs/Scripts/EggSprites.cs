using System;
using System.Collections.Generic;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>Exact, top-down frames for the supplied egg and basket artwork.</summary>
    public sealed class EggSprites
    {
        private const string Root = "Sprites/eggs/";
        private readonly Dictionary<EggKind, ChickenSprites.Animation> falling =
            new Dictionary<EggKind, ChickenSprites.Animation>();

        public ChickenSprites.Animation Crack { get; private set; }
        public ChickenSprites.Animation Thrown { get; private set; }
        public ChickenSprites.Animation BasketCatch { get; private set; }
        public ChickenSprites.Animation BasketThrow { get; private set; }
        public ChickenSprites.Animation CatchPuff { get; private set; }
        public SpriteFrame BasketIdle { get; private set; }

        private EggSprites() { }

        public ChickenSprites.Animation Falling(EggKind kind)
        {
            return falling[kind];
        }

        public static EggSprites Load()
        {
            EggSprites sprites = new EggSprites();
            foreach (EggKind kind in Enum.GetValues(typeof(EggKind)))
            {
                string name = kind.ToString().ToLowerInvariant();
                sprites.falling[kind] = LoadStrip("sheets/" + name + "_fall_6x14x18", 14, 18, 6, 0.1);
            }

            sprites.Crack = LoadStrip("sheets/crack_5x14x18", 14, 18, 5, 0.1);
            sprites.Thrown = LoadStrip("sheets/normal_thrown_4x14x18", 14, 18, 4, 0.1);
            sprites.BasketIdle = LoadStrip("basket/basket_idle", 48, 26, 1, 0.1).FrameAt(0, false);
            sprites.BasketCatch = LoadStrip("sheets/basket_catch_4x48x26", 48, 26, 4, GameModel.BasketFrameSeconds);
            sprites.BasketThrow = LoadStrip("sheets/basket_throw_4x48x26", 48, 26, 4, GameModel.BasketFrameSeconds);
            List<SpriteFrame> puffs = new List<SpriteFrame>();
            for (int i = 1; i <= 3; i++)
                puffs.Add(LoadStrip("basket/catch_puff_" + i, 16, 5, 1, 0.025).FrameAt(0, false));
            sprites.CatchPuff = new ChickenSprites.Animation(puffs, new List<double> { 0.025, 0.025, 0.025 });
            return sprites;
        }

        private static ChickenSprites.Animation LoadStrip(string path, int width, int height, int count, double seconds)
        {
            Texture2D sheet = Resources.Load<Texture2D>(Root + path);
            if (sheet == null || sheet.width != width * count || sheet.height != height)
                throw new InvalidOperationException("Egg sprite Resources/" + Root + path
                    + " must be " + (width * count) + " x " + height + ".");
            if (!sheet.isReadable)
                throw new InvalidOperationException("Enable Read/Write on Resources/" + Root + path + ".");

            Color32[] source = sheet.GetPixels32();
            List<SpriteFrame> frames = new List<SpriteFrame>();
            List<double> times = new List<double>();
            for (int frame = 0; frame < count; frame++)
            {
                Color32[] pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    Array.Copy(source, (height - 1 - y) * sheet.width + frame * width, pixels, y * width, width);
                frames.Add(new SpriteFrame(width, height, pixels));
                times.Add(seconds);
            }
            return new ChickenSprites.Animation(frames, times);
        }

        /// <summary>Checks every sliced pixel against the matching individual PNG.</summary>
        public static string VerifyBundledAssets()
        {
            EggSprites sprites = Load();
            foreach (EggKind kind in Enum.GetValues(typeof(EggKind)))
            {
                string name = kind.ToString().ToLowerInvariant();
                VerifyClip(sprites.Falling(kind), "eggs/" + name + "/" + name + "_fall_", 6, 0.1);
            }
            VerifyClip(sprites.Crack, "crack/crack_", 5, 0.1);
            VerifyClip(sprites.Thrown, "thrown/normal_thrown_", 4, 0.1);
            VerifyClip(sprites.BasketCatch, "basket/basket_catch_", 4, GameModel.BasketFrameSeconds);
            VerifyClip(sprites.BasketThrow, "basket/basket_throw_", 4, GameModel.BasketFrameSeconds);
            VerifyClip(sprites.CatchPuff, "basket/catch_puff_", 3, 0.025);
            VerifyTintedBlit();
            return "EGG SPRITES VERIFIED: 7 kinds, 62 animation frames and basket idle; exact pixels, looping and one-shots";
        }

        private static void VerifyClip(ChickenSprites.Animation clip, string prefix, int count, double seconds)
        {
            if (clip.FrameCount != count || Math.Abs(clip.Duration - count * seconds) > 0.000001)
                throw new InvalidOperationException(prefix + " has incorrect frame count or timing.");
            SpriteFrame first = clip.FrameAt(0, false);
            for (int i = 0; i < count; i++)
            {
                SpriteFrame actual = clip.FrameAt((i + 0.5) * seconds, false);
                SpriteFrame expected = LoadStrip(prefix + (i + 1), first.Width, first.Height, 1, seconds).FrameAt(0, false);
                for (int p = 0; p < actual.Pixels.Length; p++)
                    if (!actual.Pixels[p].Equals(expected.Pixels[p]))
                        throw new InvalidOperationException(prefix + (i + 1) + " does not match its strip frame.");
            }
            if (!ReferenceEquals(first, clip.FrameAt(clip.Duration, true))
                || !ReferenceEquals(clip.FrameAt(clip.Duration - seconds / 2, false), clip.FrameAt(99, false)))
                throw new InvalidOperationException(prefix + " must wrap when looping and hold when one-shot.");
        }

        private static void VerifyTintedBlit()
        {
            PixelCanvas canvas = new PixelCanvas(1, 1);
            canvas.Clear(new Color32(0, 0, 0, 255));
            SpriteFrame frame = new SpriteFrame(1, 1, new[] { new Color32(200, 100, 50, 128) });
            canvas.DrawSprite(frame, 0, 0, 1, 1, new Color32(128, 255, 0, 128));
            // RGB tint first, then source alpha multiplied by the age fade.
            if (!canvas.Pixels[0].Equals(new Color32(25, 25, 0, 255))
                || !frame.Pixels[0].Equals(new Color32(200, 100, 50, 128)))
                throw new InvalidOperationException("Crack tint/fade must multiply RGB and alpha without mutating the source.");
            canvas.DrawSprite(frame, 0, 0, 1, 1, new Color32(255, 255, 255, 0));
            if (!canvas.Pixels[0].Equals(new Color32(25, 25, 0, 255)))
                throw new InvalidOperationException("A fully faded crack must leave the background unchanged.");
        }
    }
}
