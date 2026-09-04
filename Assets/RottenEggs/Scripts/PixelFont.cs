using System;

namespace RottenEggs
{
    /// <summary>
    /// Tiny built-in 5x7 bitmap typeface for sharp, portable retro UI text.
    ///
    /// <para>Every letter is drawn as whole rectangles on the pixel canvas, so
    /// the game does not depend on whichever font happens to be installed, and
    /// text lands on exact pixel coordinates at any window size.</para>
    /// </summary>
    public static class PixelFont
    {
        private const int GlyphWidth = 5;
        private const int GlyphHeight = 7;

        private static readonly int[][] Ascii = new int[128][];

        private static readonly int[] Bullet = Rows(0, 0, 4, 14, 4, 0, 0);
        private static readonly int[] ArrowUp = Rows(4, 14, 21, 4, 4, 4, 4);
        private static readonly int[] ArrowDown = Rows(4, 4, 4, 4, 21, 14, 4);
        private static readonly int[] ArrowLeft = Rows(0, 4, 8, 31, 8, 4, 0);
        private static readonly int[] ArrowRight = Rows(0, 4, 2, 31, 2, 4, 0);

        public struct Style
        {
            public readonly int PixelSize;
            public readonly int LetterGap;
            public readonly int ShadowOffset;

            public Style(int pixelSize, int letterGap, int shadowOffset)
            {
                if (pixelSize < 1 || letterGap < 0 || shadowOffset < 0)
                {
                    throw new ArgumentException("Pixel-font measurements cannot be negative");
                }

                PixelSize = pixelSize;
                LetterGap = letterGap;
                ShadowOffset = shadowOffset;
            }
        }

        static PixelFont()
        {
            Glyph(' ', 0, 0, 0, 0, 0, 0, 0);
            Glyph('A', 14, 17, 17, 31, 17, 17, 17);
            Glyph('B', 30, 17, 17, 30, 17, 17, 30);
            Glyph('C', 14, 17, 16, 16, 16, 17, 14);
            Glyph('D', 30, 17, 17, 17, 17, 17, 30);
            Glyph('E', 31, 16, 16, 30, 16, 16, 31);
            Glyph('F', 31, 16, 16, 30, 16, 16, 16);
            Glyph('G', 14, 17, 16, 23, 17, 17, 15);
            Glyph('H', 17, 17, 17, 31, 17, 17, 17);
            Glyph('I', 31, 4, 4, 4, 4, 4, 31);
            Glyph('J', 7, 2, 2, 2, 18, 18, 12);
            Glyph('K', 17, 18, 20, 24, 20, 18, 17);
            Glyph('L', 16, 16, 16, 16, 16, 16, 31);
            Glyph('M', 17, 27, 21, 21, 17, 17, 17);
            Glyph('N', 17, 25, 21, 19, 17, 17, 17);
            Glyph('O', 14, 17, 17, 17, 17, 17, 14);
            Glyph('P', 30, 17, 17, 30, 16, 16, 16);
            Glyph('Q', 14, 17, 17, 17, 21, 18, 13);
            Glyph('R', 30, 17, 17, 30, 20, 18, 17);
            Glyph('S', 15, 16, 16, 14, 1, 1, 30);
            Glyph('T', 31, 4, 4, 4, 4, 4, 4);
            Glyph('U', 17, 17, 17, 17, 17, 17, 14);
            Glyph('V', 17, 17, 17, 17, 17, 10, 4);
            Glyph('W', 17, 17, 17, 21, 21, 21, 10);
            Glyph('X', 17, 17, 10, 4, 10, 17, 17);
            Glyph('Y', 17, 17, 10, 4, 4, 4, 4);
            Glyph('Z', 31, 1, 2, 4, 8, 16, 31);

            Glyph('0', 14, 17, 19, 21, 25, 17, 14);
            Glyph('1', 4, 12, 4, 4, 4, 4, 14);
            Glyph('2', 14, 17, 1, 2, 4, 8, 31);
            Glyph('3', 30, 1, 1, 14, 1, 1, 30);
            Glyph('4', 2, 6, 10, 18, 31, 2, 2);
            Glyph('5', 31, 16, 16, 30, 1, 1, 30);
            Glyph('6', 14, 16, 16, 30, 17, 17, 14);
            Glyph('7', 31, 1, 2, 4, 8, 8, 8);
            Glyph('8', 14, 17, 17, 14, 17, 17, 14);
            Glyph('9', 14, 17, 17, 15, 1, 1, 14);

            Glyph('.', 0, 0, 0, 0, 0, 12, 12);
            Glyph(',', 0, 0, 0, 0, 6, 4, 8);
            Glyph(':', 0, 4, 4, 0, 4, 4, 0);
            Glyph(';', 0, 4, 4, 0, 4, 4, 8);
            Glyph('!', 4, 4, 4, 4, 4, 0, 4);
            Glyph('?', 14, 17, 1, 2, 4, 0, 4);
            Glyph('-', 0, 0, 0, 31, 0, 0, 0);
            Glyph('_', 0, 0, 0, 0, 0, 0, 31);
            Glyph('+', 0, 4, 4, 31, 4, 4, 0);
            Glyph('/', 1, 2, 2, 4, 8, 8, 16);
            Glyph('\\', 16, 8, 8, 4, 2, 2, 1);
            Glyph('(', 2, 4, 8, 8, 8, 4, 2);
            Glyph(')', 8, 4, 2, 2, 2, 4, 8);
            Glyph('[', 14, 8, 8, 8, 8, 8, 14);
            Glyph(']', 14, 2, 2, 2, 2, 2, 14);
            Glyph('=', 0, 31, 0, 31, 0, 0, 0);
            Glyph('%', 17, 2, 4, 8, 16, 17, 0);
            Glyph('#', 10, 31, 10, 10, 31, 10, 0);
            Glyph('\'', 4, 4, 8, 0, 0, 0, 0);
            Glyph('"', 10, 10, 20, 0, 0, 0, 0);
        }

        public static int TextWidth(string text, Style style)
        {
            int glyphCount = text.Length;
            if (glyphCount == 0)
            {
                return 0;
            }

            return glyphCount * GlyphWidth * style.PixelSize
                   + (glyphCount - 1) * style.LetterGap;
        }

        public static void Draw(PixelCanvas canvas, string text, int x, int baselineY, Style style)
        {
            int cursorX = x;
            int topY = baselineY - GlyphHeight * style.PixelSize;
            foreach (char character in text)
            {
                int[] glyphRows = RowsFor(character);
                for (int row = 0; row < GlyphHeight; row++)
                {
                    for (int column = 0; column < GlyphWidth; column++)
                    {
                        int bit = 1 << (GlyphWidth - 1 - column);
                        if ((glyphRows[row] & bit) != 0)
                        {
                            canvas.FillRect(
                                cursorX + column * style.PixelSize,
                                topY + row * style.PixelSize,
                                style.PixelSize,
                                style.PixelSize);
                        }
                    }
                }

                cursorX += GlyphWidth * style.PixelSize + style.LetterGap;
            }
        }

        private static int[] RowsFor(int rawCodePoint)
        {
            int codePoint = Normalized(rawCodePoint);
            if (codePoint >= 0 && codePoint < Ascii.Length && Ascii[codePoint] != null)
            {
                return Ascii[codePoint];
            }

            switch (codePoint)
            {
                case 0x2022:
                    return Bullet;
                case 0x2191:
                    return ArrowUp;
                case 0x2193:
                    return ArrowDown;
                case 0x2190:
                    return ArrowLeft;
                case 0x2192:
                    return ArrowRight;
                default:
                    return Ascii['?'];
            }
        }

        public static bool HasGlyph(int rawCodePoint)
        {
            int codePoint = Normalized(rawCodePoint);
            if (codePoint >= 0 && codePoint < Ascii.Length)
            {
                return Ascii[codePoint] != null;
            }

            return codePoint == 0x2022
                   || codePoint == 0x2191
                   || codePoint == 0x2193
                   || codePoint == 0x2190
                   || codePoint == 0x2192;
        }

        private static int Normalized(int codePoint)
        {
            if (codePoint == 0x2013 || codePoint == 0x2014)
            {
                return '-';
            }

            if (codePoint >= 'a' && codePoint <= 'z')
            {
                return codePoint - ('a' - 'A');
            }

            return codePoint;
        }

        private static void Glyph(char character, params int[] rowBits)
        {
            Ascii[character] = Rows(rowBits);
        }

        private static int[] Rows(params int[] rowBits)
        {
            if (rowBits.Length != GlyphHeight)
            {
                throw new ArgumentException("Each pixel glyph needs exactly seven rows");
            }

            return rowBits;
        }
    }
}
