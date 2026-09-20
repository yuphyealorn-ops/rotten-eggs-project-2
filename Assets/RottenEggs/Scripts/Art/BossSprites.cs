using System;
using System.Collections.Generic;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Artwork for the boss's attacks, from
    /// <c>Resources/Sprites/bomb_and_feathers/sheets</c>. Every clip is
    /// optional: whatever is missing is drawn procedurally at the same size
    /// and position, so the fight plays the same with or without it and each
    /// strip can be dropped in on its own.
    ///
    /// <para>Strips are horizontal with no padding; frame <c>i</c> starts at
    /// <c>x = i × frameWidth</c>.</para>
    /// </summary>
    public sealed class BossSprites
    {
        private const string Root = "Sprites/bomb_and_feathers/sheets/";

        public SpriteFrame[] BombFall { get; private set; }       // 6, loop  ~10 fps: spark flicker + lean
        public SpriteFrame[] BombArmed { get; private set; }      // 4, loop  ~15 fps: red flash
        public SpriteFrame[] Explosion { get; private set; }      // 7, once  60 ms: fireball, collapse, smoke
        public SpriteFrame[] FeatherFall { get; private set; }    // 8, loop  ~10 fps: tumbling vane
        public SpriteFrame[] DownFall { get; private set; }       // 6, loop  small fluff
        public SpriteFrame[] FeatherBurst { get; private set; }   // 4, once  60 ms: on landing
        public SpriteFrame[] FlockWarn { get; private set; }      // 3, loop  the telegraph

        public const double BombFallFps = 10;
        public const double BombArmedFps = 15;
        public const double FeatherFps = 10;
        public const double WarnFps = 8;

        private BossSprites() { }

        public static BossSprites Load()
        {
            BossSprites sprites = new BossSprites();
            sprites.BombFall = LoadStrip("bomb_fall_6x14x18", GameModel.BombW, GameModel.BombH, 6);
            sprites.BombArmed = LoadStrip("bomb_armed_4x14x18", GameModel.BombW, GameModel.BombH, 4);
            sprites.Explosion = LoadStrip("explosion_7x32x32", GameModel.BlastSize, GameModel.BlastSize, 7);
            sprites.FeatherFall = LoadStrip("feather_fall_8x12x14", GameModel.FeatherW, GameModel.FeatherH, 8);
            sprites.DownFall = LoadStrip("down_fall_6x8x8", GameModel.DownW, GameModel.DownH, 6);
            sprites.FeatherBurst = LoadStrip("feather_burst_4x16x16", GameModel.FeatherBurstSize, GameModel.FeatherBurstSize, 4);
            sprites.FlockWarn = LoadStrip("flock_warn_3x12x8", GameModel.FlockWarnW, GameModel.FlockWarnH, 3);
            return sprites;
        }

        /// <summary>
        /// Slices one horizontal strip into frames. Returns null, with one
        /// warning, if the file is absent, the wrong size, or not readable.
        /// </summary>
        private static SpriteFrame[] LoadStrip(string name, int width, int height, int count)
        {
            Texture2D sheet = Resources.Load<Texture2D>(Root + name);
            if (sheet == null)
            {
                return null;
            }

            if (sheet.width != width * count || sheet.height != height)
            {
                Debug.LogWarning("Rotten Eggs: Resources/" + Root + name + " is " + sheet.width + "x" + sheet.height
                                 + " but should be " + (width * count) + "x" + height
                                 + "; drawing this attack procedurally instead.");
                return null;
            }

            Color32[] source;
            try
            {
                source = sheet.GetPixels32();
            }
            catch (UnityException)
            {
                Debug.LogWarning("Rotten Eggs: tick Read/Write on Resources/" + Root + name
                                 + "; drawing this attack procedurally instead.");
                return null;
            }

            SpriteFrame[] frames = new SpriteFrame[count];
            for (int frame = 0; frame < count; frame++)
            {
                Color32[] pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                {
                    // Texture rows are bottom-up; the canvas counts from the top.
                    Array.Copy(source, (height - 1 - y) * sheet.width + frame * width, pixels, y * width, width);
                }

                frames[frame] = new SpriteFrame(width, height, pixels);
            }

            return frames;
        }

        /// <summary>Frame for a clip that plays once over <paramref name="seconds"/>, holding its last frame.</summary>
        public static SpriteFrame Once(SpriteFrame[] clip, double time, double seconds)
        {
            int index = (int)(clip.Length * Math.Max(0, time) / seconds);
            return clip[Math.Min(clip.Length - 1, index)];
        }

        /// <summary>Frame for a looping clip at a fixed rate.</summary>
        public static SpriteFrame Loop(SpriteFrame[] clip, double time, double fps)
        {
            int index = (int)(Math.Max(0, time) * fps) % clip.Length;
            return clip[index];
        }
    }
}
