using System;
using System.Collections.Generic;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Artwork for the boss's attacks. Every clip is optional: whatever is
    /// missing from Resources/Sprites/boss is drawn procedurally at the same
    /// size and position, so the fight plays the same before and after the
    /// art arrives and each strip can be dropped in on its own.
    ///
    /// <para>Expected files, all horizontal strips with no padding:
    /// <c>bomb_5x24x24.png</c> (120×24), <c>feather_5x24x24.png</c> (120×24),
    /// <c>explosion_6x48x48.png</c> (288×48), and <c>feather_flock_10x48x48.png</c>
    /// as two rows of five (240×96).</para>
    /// </summary>
    public sealed class BossSprites
    {
        private const string Root = "Sprites/boss/";

        public SpriteFrame[] Bomb { get; private set; }        // 5 fuse frames
        public SpriteFrame[] Feather { get; private set; }     // 5 twirl frames
        public SpriteFrame[] Explosion { get; private set; }   // 6 blast frames
        public SpriteFrame[] Flock { get; private set; }       // 10 frames: gather, dive, burst

        private BossSprites() { }

        public static BossSprites Load()
        {
            BossSprites sprites = new BossSprites();
            sprites.Bomb = LoadGrid("bomb_5x24x24", 24, 24, 5, 1);
            sprites.Feather = LoadGrid("feather_5x24x24", 24, 24, 5, 1);
            sprites.Explosion = LoadGrid("explosion_6x48x48", 48, 48, 6, 1);
            sprites.Flock = LoadGrid("feather_flock_10x48x48", 48, 48, 5, 2);
            return sprites;
        }

        /// <summary>
        /// Slices a sheet laid out as <paramref name="columns"/> × <paramref name="rows"/>
        /// frames, read left to right then top to bottom. Returns null, with one
        /// warning, if the file is absent or the wrong size.
        /// </summary>
        private static SpriteFrame[] LoadGrid(string name, int width, int height, int columns, int rows)
        {
            Texture2D sheet = Resources.Load<Texture2D>(Root + name);
            if (sheet == null)
            {
                return null;
            }

            if (sheet.width != width * columns || sheet.height != height * rows)
            {
                Debug.LogWarning("Rotten Eggs: Resources/" + Root + name + " is " + sheet.width + "x" + sheet.height
                                 + " but should be " + (width * columns) + "x" + (height * rows)
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

            List<SpriteFrame> frames = new List<SpriteFrame>(columns * rows);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Color32[] pixels = new Color32[width * height];
                    for (int y = 0; y < height; y++)
                    {
                        // Texture rows are bottom-up; the canvas counts from the top.
                        int sourceRow = sheet.height - 1 - (row * height + y);
                        Array.Copy(source, sourceRow * sheet.width + column * width, pixels, y * width, width);
                    }

                    frames.Add(new SpriteFrame(width, height, pixels));
                }
            }

            return frames.ToArray();
        }

        /// <summary>Picks the frame for a point in a clip that runs once over <paramref name="seconds"/>.</summary>
        public static SpriteFrame FrameFor(SpriteFrame[] clip, double time, double seconds)
        {
            int index = (int)(clip.Length * Math.Max(0, time) / seconds);
            return clip[Math.Min(clip.Length - 1, index)];
        }
    }
}
