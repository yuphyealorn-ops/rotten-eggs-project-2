using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// Slices the bundled chicken sheets into ready-to-blit frames.
    ///
    /// <para>The Java prototype decoded animated GIFs because Swing can only
    /// play a GIF inside a label, and such a label always loops. The game needs
    /// the opposite: every clip must run through exactly once and then hold,
    /// replaying only when its own condition happens again. Unity has the same
    /// requirement, so the equivalent source artwork - the 20 x 21 sheet strips
    /// the GIFs were made from - is cut into frames here and
    /// <see cref="Animation.FrameAt"/> picks the frame for an elapsed time.</para>
    ///
    /// <para>The sheets are the only chicken artwork the game has, so a clip
    /// that will not load is a hard error naming the file, rather than a silent
    /// fall back to something else.</para>
    /// </summary>
    public sealed class ChickenSprites
    {
        private const int FrameWidth = 20;
        private const int FrameHeight = 21;
        private const double DefaultFrameSeconds = 0.10;

        /// <summary>One sliced sheet: its frames plus the time each one starts.</summary>
        public sealed class Animation
        {
            private readonly SpriteFrame[] frames;
            private readonly double[] startTimes;
            public readonly double Duration;

            public Animation(List<SpriteFrame> slicedFrames, List<double> frameSeconds)
            {
                frames = slicedFrames.ToArray();
                startTimes = new double[frames.Length];
                double total = 0;
                for (int i = 0; i < frames.Length; i++)
                {
                    startTimes[i] = total;
                    total += frameSeconds[i];
                }

                Duration = total;
            }

            public int FrameCount
            {
                get { return frames.Length; }
            }

            /// <summary>
            /// Returns the frame for <paramref name="seconds"/> into the clip. A
            /// looping clip wraps around; a one-shot clip stops on its final
            /// frame and stays there until its action starts the clip over.
            /// </summary>
            public SpriteFrame FrameAt(double seconds, bool looping)
            {
                double time = seconds;
                if (looping && Duration > 0)
                {
                    time -= Math.Floor(time / Duration) * Duration;
                }

                for (int i = frames.Length - 1; i > 0; i--)
                {
                    if (time >= startTimes[i])
                    {
                        return frames[i];
                    }
                }

                return frames[0];
            }
        }

        private readonly Dictionary<AnimState, Animation> animations = new Dictionary<AnimState, Animation>();

        private ChickenSprites()
        {
        }

        /// <summary>Loads every chicken clip, or fails loudly naming what went wrong.</summary>
        public static ChickenSprites Load()
        {
            ChickenSprites sprites = new ChickenSprites();
            List<string> failures = new List<string>();
            foreach (AnimState state in AnimStates.All)
            {
                string filename = ResourceFor(state);
                try
                {
                    sprites.animations[state] = Slice(filename);
                }
                catch (Exception exception)
                {
                    failures.Add(filename + ": " + exception.Message);
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Chicken sprites could not be loaded: " + string.Join("; ", failures));
            }

            return sprites;
        }

        /// <summary>Returns the clip for a state.</summary>
        public Animation Animate(AnimState state)
        {
            return animations[state];
        }

        public static string SpritesDirectory
        {
            get { return Path.Combine(Application.streamingAssetsPath, "RottenEggs", "Sprites"); }
        }

        private static string ResourceFor(AnimState state)
        {
            switch (state)
            {
                case AnimState.Idle:
                    return "chicken-idle.png";
                case AnimState.Walking:
                    return "chicken-walking.png";
                case AnimState.Jumping:
                    return "chicken-jumping.png";
                case AnimState.Damage:
                    return "chicken-damage.png";
                case AnimState.Die:
                    return "chicken-die.png";
                default:
                    throw new ArgumentOutOfRangeException("state");
            }
        }

        /// <summary>
        /// Reads one horizontal strip and cuts it into frames. Unity hands back
        /// texture rows bottom-up, so each frame is flipped into the top-down
        /// order the pixel canvas draws with.
        /// </summary>
        private static Animation Slice(string filename)
        {
            string path = Path.Combine(SpritesDirectory, filename);
            if (!File.Exists(path))
            {
                throw new IOException("missing from StreamingAssets/RottenEggs/Sprites");
            }

            Texture2D sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.filterMode = FilterMode.Point;
            if (!sheet.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(sheet);
                throw new IOException("is not a readable PNG");
            }

            try
            {
                if (sheet.height != FrameHeight || sheet.width % FrameWidth != 0)
                {
                    throw new IOException("must be a strip of " + FrameWidth + " x " + FrameHeight + " frames, but is "
                                          + sheet.width + " x " + sheet.height);
                }

                int frameCount = sheet.width / FrameWidth;
                if (frameCount <= 0)
                {
                    throw new IOException("holds no frames");
                }

                Color32[] sheetPixels = sheet.GetPixels32();
                List<SpriteFrame> frames = new List<SpriteFrame>(frameCount);
                List<double> frameSeconds = new List<double>(frameCount);

                for (int frame = 0; frame < frameCount; frame++)
                {
                    Color32[] pixels = new Color32[FrameWidth * FrameHeight];
                    for (int row = 0; row < FrameHeight; row++)
                    {
                        int sourceRow = FrameHeight - 1 - row;
                        for (int column = 0; column < FrameWidth; column++)
                        {
                            pixels[row * FrameWidth + column] =
                                sheetPixels[sourceRow * sheet.width + frame * FrameWidth + column];
                        }
                    }

                    frames.Add(new SpriteFrame(FrameWidth, FrameHeight, pixels));
                    frameSeconds.Add(DefaultFrameSeconds);
                }

                return new Animation(frames, frameSeconds);
            }
            finally
            {
                UnityEngine.Object.Destroy(sheet);
            }
        }

        /// <summary>
        /// Slices every bundled clip and checks that the one-shot clips still
        /// match the timings the game rules pause for.
        /// </summary>
        public static string VerifyBundledAssets()
        {
            List<string> failures = new List<string>();
            int frames = 0;

            foreach (AnimState state in AnimStates.All)
            {
                string filename = ResourceFor(state);
                try
                {
                    Animation animation = Slice(filename);
                    if (animation.FrameCount == 0)
                    {
                        failures.Add(filename + " sliced to zero frames");
                        continue;
                    }

                    frames += animation.FrameCount;
                    double expected = GameModel.AnimSeconds(state);
                    if (!state.Loops() && Math.Abs(animation.Duration - expected) > 0.001)
                    {
                        failures.Add(filename + " runs " + animation.Duration
                                              + "s but the rules reserve " + expected + "s");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(filename + " cannot be sliced: " + exception.Message);
                }
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Sprite asset verification failed: "
                                                    + string.Join("; ", failures));
            }

            return "SPRITE ASSETS VERIFIED: " + AnimStates.All.Length
                   + " chicken animations, " + frames + " frames";
        }
    }
}
