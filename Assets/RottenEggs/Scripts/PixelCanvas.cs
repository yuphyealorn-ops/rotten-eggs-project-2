using System;
using UnityEngine;

namespace RottenEggs
{
    /// <summary>
    /// A tiny software rasterizer that stands in for Java2D's Graphics2D.
    ///
    /// <para>The Java prototype draws its whole world into one 480 x 270 image
    /// with plain rectangles, ovals and polygons, then scales that image up in
    /// whole-number steps. This class keeps that model intact: every draw call
    /// writes straight into a Color32 buffer that is uploaded to a Texture2D
    /// once per frame, so the artwork stays pixel-exact and the porting work is
    /// a one-to-one translation of the original drawing code.</para>
    ///
    /// <para>Coordinates match Java's: the origin is the top-left corner and y
    /// grows downward. The flip to Unity's bottom-up texture layout happens in
    /// <see cref="Index"/> alone.</para>
    /// </summary>
    public sealed class PixelCanvas
    {
        public readonly int Width;
        public readonly int Height;

        private readonly Color32[] pixels;
        private Color32 color = new Color32(255, 255, 255, 255);
        private int translateX;
        private int translateY;
        private int clipMinX;
        private int clipMinY;
        private int clipMaxX;
        private int clipMaxY;

        public PixelCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
            ClearClip();
        }

        public Color32[] Pixels
        {
            get { return pixels; }
        }

        public void SetColor(Color32 value)
        {
            color = value;
        }

        /// <summary>Colour with an explicit alpha, mirroring new Color(r, g, b, a).</summary>
        public void SetColor(Color32 value, byte alpha)
        {
            color = new Color32(value.r, value.g, value.b, alpha);
        }

        public void SetColor(byte r, byte g, byte b)
        {
            color = new Color32(r, g, b, 255);
        }

        public void SetColor(byte r, byte g, byte b, byte a)
        {
            color = new Color32(r, g, b, a);
        }

        public void SetTranslate(int x, int y)
        {
            translateX = x;
            translateY = y;
        }

        public void ResetTranslate()
        {
            translateX = 0;
            translateY = 0;
        }

        public void SetClip(int x, int y, int width, int height)
        {
            clipMinX = Math.Max(0, x + translateX);
            clipMinY = Math.Max(0, y + translateY);
            clipMaxX = Math.Min(Width, x + translateX + width);
            clipMaxY = Math.Min(Height, y + translateY + height);
        }

        public void ClearClip()
        {
            clipMinX = 0;
            clipMinY = 0;
            clipMaxX = Width;
            clipMaxY = Height;
        }

        /// <summary>Fills the whole surface, ignoring the clip, like a fresh frame.</summary>
        public void Clear(Color32 background)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }
        }

        private int Index(int x, int y)
        {
            // Java images start at the top row; Unity textures start at the bottom.
            return (Height - 1 - y) * Width + x;
        }

        /// <summary>Source-over blend of the current colour into one pixel.</summary>
        private void Blend(int x, int y)
        {
            int px = x + translateX;
            int py = y + translateY;
            if (px < clipMinX || px >= clipMaxX || py < clipMinY || py >= clipMaxY)
            {
                return;
            }

            int index = Index(px, py);
            if (color.a == 255)
            {
                pixels[index] = color;
                return;
            }

            if (color.a == 0)
            {
                return;
            }

            Color32 destination = pixels[index];
            int alpha = color.a;
            int inverse = 255 - alpha;
            pixels[index] = new Color32(
                (byte)((color.r * alpha + destination.r * inverse) / 255),
                (byte)((color.g * alpha + destination.g * inverse) / 255),
                (byte)((color.b * alpha + destination.b * inverse) / 255),
                255);
        }

        public void FillRect(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            for (int row = y; row < y + height; row++)
            {
                for (int column = x; column < x + width; column++)
                {
                    Blend(column, row);
                }
            }
        }

        /// <summary>Ellipse bounded by the given rectangle, as Java2D's fillOval draws it.</summary>
        public void FillOval(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            double radiusX = width / 2.0;
            double radiusY = height / 2.0;
            double centerX = x + radiusX;
            double centerY = y + radiusY;

            for (int row = y; row < y + height; row++)
            {
                double dy = (row + 0.5 - centerY) / radiusY;
                double remaining = 1.0 - dy * dy;
                if (remaining < 0)
                {
                    continue;
                }

                double span = radiusX * Math.Sqrt(remaining);
                int from = (int)Math.Ceiling(centerX - span - 0.5);
                int to = (int)Math.Floor(centerX + span - 0.5);
                for (int column = from; column <= to; column++)
                {
                    Blend(column, row);
                }
            }
        }

        /// <summary>Even-odd scanline fill, matching Java2D's fillPolygon.</summary>
        public void FillPolygon(int[] xs, int[] ys, int count)
        {
            if (count < 3)
            {
                return;
            }

            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                minY = Math.Min(minY, ys[i]);
                maxY = Math.Max(maxY, ys[i]);
            }

            double[] crossings = new double[count];
            for (int row = minY; row < maxY; row++)
            {
                double scanY = row + 0.5;
                int found = 0;
                for (int i = 0; i < count; i++)
                {
                    int j = (i + 1) % count;
                    double y0 = ys[i];
                    double y1 = ys[j];
                    if (y0 == y1)
                    {
                        continue;
                    }

                    if ((scanY >= y0 && scanY < y1) || (scanY >= y1 && scanY < y0))
                    {
                        double t = (scanY - y0) / (y1 - y0);
                        crossings[found++] = xs[i] + t * (xs[j] - xs[i]);
                    }
                }

                Array.Sort(crossings, 0, found);
                for (int pair = 0; pair + 1 < found; pair += 2)
                {
                    // Half-open spans, as Java2D fills them: a centre exactly on
                    // the left edge is inside, one on the right edge is not.
                    int from = (int)Math.Ceiling(crossings[pair] - 0.5);
                    int to = (int)Math.Ceiling(crossings[pair + 1] - 0.5) - 1;
                    for (int column = from; column <= to; column++)
                    {
                        Blend(column, row);
                    }
                }
            }
        }

        /// <summary>Closed one-pixel outline, matching Java2D's drawPolygon.</summary>
        public void DrawPolygon(int[] xs, int[] ys, int count)
        {
            if (count < 2)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                DrawLine(xs[i], ys[i], xs[j], ys[j]);
            }
        }

        public void DrawLine(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int stepX = x0 < x1 ? 1 : -1;
            int stepY = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int x = x0;
            int y = y0;

            while (true)
            {
                Blend(x, y);
                if (x == x1 && y == y1)
                {
                    return;
                }

                int doubled = 2 * error;
                if (doubled >= dy)
                {
                    error += dy;
                    x += stepX;
                }

                if (doubled <= dx)
                {
                    error += dx;
                    y += stepY;
                }
            }
        }

        /// <summary>
        /// Arc along the ellipse in the given box. Angles follow Java2D: zero
        /// degrees sits at three o'clock and positive angles sweep counter-
        /// clockwise on screen.
        /// </summary>
        public void DrawArc(int x, int y, int width, int height, double startDegrees, double extentDegrees, int strokeWidth)
        {
            if (width <= 0 || height <= 0 || extentDegrees == 0 || strokeWidth <= 0)
            {
                return;
            }

            double radiusX = width / 2.0;
            double radiusY = height / 2.0;
            double centerX = x + radiusX;
            double centerY = y + radiusY;
            double half = strokeWidth / 2.0;

            double fromDegrees = extentDegrees >= 0 ? startDegrees : startDegrees + extentDegrees;
            double sweep = Math.Abs(extentDegrees);
            int margin = strokeWidth + 1;

            for (int row = y - margin; row <= y + height + margin; row++)
            {
                for (int column = x - margin; column <= x + width + margin; column++)
                {
                    double dx = column + 0.5 - centerX;
                    double dy = row + 0.5 - centerY;
                    double nx = dx / radiusX;
                    double ny = dy / radiusY;
                    double implicitValue = nx * nx + ny * ny - 1.0;

                    // First-order distance from the ellipse, which is what a
                    // stroked path covers to either side of the curve.
                    double gradient = 2.0 * Math.Sqrt(
                        dx / (radiusX * radiusX) * (dx / (radiusX * radiusX))
                        + dy / (radiusY * radiusY) * (dy / (radiusY * radiusY)));
                    if (gradient <= 0)
                    {
                        continue;
                    }

                    if (Math.Abs(implicitValue / gradient) > half)
                    {
                        continue;
                    }

                    // Java2D measures angles counter-clockwise from three o'clock.
                    double degrees = Math.Atan2(-dy, dx) * 180.0 / Math.PI;
                    double offset = degrees - fromDegrees;
                    offset -= Math.Floor(offset / 360.0) * 360.0;
                    if (offset > sweep)
                    {
                        continue;
                    }

                    Blend(column, row);
                }
            }
        }

        /// <summary>
        /// Nearest-neighbour blit of a sprite frame. The destination corners
        /// carry the same meaning as Java2D's ten-argument drawImage: giving a
        /// first x greater than the second mirrors the artwork in place, which
        /// is how a left-bound chicken is drawn.
        /// </summary>
        public void DrawSprite(SpriteFrame frame, int destX0, int destY0, int destX1, int destY1)
        {
            int left = Math.Min(destX0, destX1);
            int right = Math.Max(destX0, destX1);
            int top = Math.Min(destY0, destY1);
            int bottom = Math.Max(destY0, destY1);
            int destWidth = right - left;
            int destHeight = bottom - top;
            if (destWidth <= 0 || destHeight <= 0)
            {
                return;
            }

            bool flipX = destX0 > destX1;
            bool flipY = destY0 > destY1;

            for (int row = 0; row < destHeight; row++)
            {
                int sourceRow = row * frame.Height / destHeight;
                if (flipY)
                {
                    sourceRow = frame.Height - 1 - sourceRow;
                }

                for (int column = 0; column < destWidth; column++)
                {
                    int sourceColumn = column * frame.Width / destWidth;
                    if (flipX)
                    {
                        sourceColumn = frame.Width - 1 - sourceColumn;
                    }

                    Color32 source = frame.Pixels[sourceRow * frame.Width + sourceColumn];
                    if (source.a == 0)
                    {
                        continue;
                    }

                    Color32 previous = color;
                    color = source;
                    Blend(left + column, top + row);
                    color = previous;
                }
            }
        }

        /// <summary>Uploads the finished frame to a texture for display.</summary>
        public void UploadTo(Texture2D texture)
        {
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }
    }

    /// <summary>One decoded animation frame held as raw top-down pixels.</summary>
    public sealed class SpriteFrame
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;

        public SpriteFrame(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }
}
