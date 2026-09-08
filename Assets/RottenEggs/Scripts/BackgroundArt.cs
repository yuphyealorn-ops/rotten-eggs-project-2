using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Loads the painted backdrop that sits behind every scene.
    ///
    /// <para>The artwork lives in Resources rather than a plain asset folder
    /// because the game object is created at runtime by
    /// <see cref="RottenEggsBootstrap"/>, so there is no Inspector slot to drag
    /// a sprite into. Like the rest of the port this is fail-safe: a missing or
    /// unreadable image simply leaves <see cref="Load"/> returning null, and the
    /// renderer falls back to the hand-drawn sky and fields.</para>
    /// </summary>
    public static class BackgroundArt
    {
        /// <summary>Path under any Resources folder, without the file extension.</summary>
        public const string ResourcePath = "Sprites/background";

        /// <summary>
        /// Reads the backdrop and box-filters it down to the pixel canvas size.
        /// Averaging rather than point-sampling keeps the fence and grass from
        /// breaking up, and doing it once here means the per-frame draw is a
        /// straight copy.
        /// </summary>
        public static SpriteFrame Load()
        {
            Texture2D source = Resources.Load<Texture2D>(ResourcePath);
            if (source == null)
            {
                Debug.LogWarning("Rotten Eggs: no backdrop at Resources/" + ResourcePath
                                 + " — falling back to the drawn background.");
                return null;
            }

            Color32[] sourcePixels;
            try
            {
                sourcePixels = source.GetPixels32();
            }
            catch (UnityException exception)
            {
                Debug.LogWarning("Rotten Eggs: the backdrop is not readable, so the drawn background"
                                 + " is used instead. Tick Read/Write in its import settings. "
                                 + exception.Message);
                return null;
            }

            return Downsample(sourcePixels, source.width, source.height,
                              GameModel.WorldW, GameModel.WorldH);
        }

        /// <summary>
        /// Averages each destination pixel over the source rectangle it covers.
        /// Unity hands back texture rows bottom-up, so the vertical axis is
        /// flipped here to match the canvas, which counts rows from the top.
        /// </summary>
        private static SpriteFrame Downsample(
            Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            Color32[] result = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                int sourceTop = y * sourceHeight / height;
                int sourceBottom = Mathf.Max(sourceTop + 1, (y + 1) * sourceHeight / height);

                for (int x = 0; x < width; x++)
                {
                    int sourceLeft = x * sourceWidth / width;
                    int sourceRight = Mathf.Max(sourceLeft + 1, (x + 1) * sourceWidth / width);

                    int r = 0, g = 0, b = 0, a = 0, samples = 0;
                    for (int sy = sourceTop; sy < sourceBottom; sy++)
                    {
                        // Flip: row 0 of the canvas is the top, row 0 of the texture is the bottom.
                        int row = (sourceHeight - 1 - sy) * sourceWidth;
                        for (int sx = sourceLeft; sx < sourceRight; sx++)
                        {
                            Color32 pixel = source[row + sx];
                            r += pixel.r;
                            g += pixel.g;
                            b += pixel.b;
                            a += pixel.a;
                            samples++;
                        }
                    }

                    result[y * width + x] = new Color32(
                        (byte)(r / samples), (byte)(g / samples),
                        (byte)(b / samples), (byte)(a / samples));
                }
            }

            return new SpriteFrame(width, height, result);
        }
    }
}
