using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// The three layers that make up one HUD heart. They are drawn back to
    /// front: the dark <see cref="Background"/> shows through wherever the red
    /// <see cref="Full"/> heart has been clipped away, and <see cref="Border"/>
    /// goes on last so the outline stays whole across a half-heart cut.
    ///
    /// <para>Loaded from Resources for the same reason as
    /// <see cref="BackgroundArt"/>: the game object is created at runtime, so
    /// there is no Inspector slot to drag the sprites into. Missing or
    /// unreadable files leave <see cref="Load"/> returning null and the renderer
    /// falls back to its drawn heart.</para>
    /// </summary>
    public sealed class HeartSprites
    {
        public const string FullPath = "Sprites/hearts/heart";
        public const string BorderPath = "Sprites/hearts/border";
        public const string BackgroundPath = "Sprites/hearts/background_heart";

        public readonly SpriteFrame Full;
        public readonly SpriteFrame Border;
        public readonly SpriteFrame Background;

        public int Width
        {
            get { return Full.Width; }
        }

        public int Height
        {
            get { return Full.Height; }
        }

        private HeartSprites(SpriteFrame full, SpriteFrame border, SpriteFrame background)
        {
            Full = full;
            Border = border;
            Background = background;
        }

        /// <summary>All three layers at the same size, or null if any is unavailable.</summary>
        public static HeartSprites Load()
        {
            SpriteFrame full = LoadFrame(FullPath);
            SpriteFrame border = LoadFrame(BorderPath);
            SpriteFrame background = LoadFrame(BackgroundPath);
            if (full == null || border == null || background == null)
            {
                return null;
            }

            bool sameSize = border.Width == full.Width && border.Height == full.Height
                            && background.Width == full.Width && background.Height == full.Height;
            if (!sameSize)
            {
                Debug.LogWarning("Rotten Eggs: the heart sprites are not all the same size, so the"
                                 + " drawn heart is used instead.");
                return null;
            }

            return new HeartSprites(full, border, background);
        }

        private static SpriteFrame LoadFrame(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning("Rotten Eggs: no heart sprite at Resources/" + resourcePath
                                 + " — falling back to the drawn heart.");
                return null;
            }

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (UnityException exception)
            {
                Debug.LogWarning("Rotten Eggs: " + resourcePath + " is not readable, so the drawn"
                                 + " heart is used instead. Tick Read/Write in its import settings. "
                                 + exception.Message);
                return null;
            }

            // Unity hands rows back bottom-up; the canvas counts them from the top.
            int width = texture.width;
            int height = texture.height;
            Color32[] flipped = new Color32[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(pixels, (height - 1 - y) * width, flipped, y * width, width);
            }

            return new SpriteFrame(width, height, flipped);
        }
    }
}
