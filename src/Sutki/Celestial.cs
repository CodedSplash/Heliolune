using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sutki
{
    internal static class Celestial
    {
        private const int Size = 80;
        private static readonly double[,] Craters =
        {
            { -.52, -.15, .17 }, { -.32, .36, .15 }, { .37, -.27, .22 },
            { -.09, -.39, .12 }, { .25, .35, .09 }, { -.62, .27, .085 },
            { .09, .57, .16 }, { .45, .11, .12 }, { .23, -.63, .08 }, { -.15, .14, .058 }
        };

        public static readonly ImageSource Sun = Texture(false);
        public static readonly ImageSource Moon = Texture(true);

        private static ImageSource Texture(bool lunar)
        {
            var pixels = new byte[Size * Size * 4];
            for (int row = 0; row < Size; row++)
                for (int col = 0; col < Size; col++)
                {
                    double x = 2.0 * (col + 0.5) / Size - 1;
                    double y = 2.0 * (row + 0.5) / Size - 1;
                    double radius = Math.Sqrt(x * x + y * y);
                    double alpha = Limit((1 - radius) * Size / 2 + 0.5);
                    if (alpha <= 0) continue;
                    double z = Math.Sqrt(Math.Max(0, 1 - radius * radius));
                    double grain = Noise(x * 38 + 12, y * 38 + 15) - 0.5;
                    int index = 4 * (row * Size + col);
                    if (lunar)
                    {
                        double shade = Limit(0.70 + 0.29 * z - 0.13 * x - 0.17 * y
                            + 0.25 * (Noise(x * 5 + 7, y * 5 + 8) - 0.5) + grain * 0.10 + Relief(x, y));
                        Put(pixels, index, alpha, 65 + shade * 168, 75 + shade * 169, 97 + shade * 157);
                    }
                    else
                    {
                        double plasma = 0.65 * Noise(x * 17 + 13, y * 18 + 11) + 0.35 * Noise(x * 4 + 7, y * 4 + 6);
                        double heat = Limit(0.22 + 0.51 * z + 0.43 * plasma + grain * 0.10);
                        Put(pixels, index, alpha, 255 - (1 - heat) * 19, 107 + heat * 135, 35 + heat * heat * 157);
                    }
                }
            var bitmap = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Pbgra32, null, pixels, Size * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static double Relief(double x, double y)
        {
            double result = 0;
            for (int i = 0; i < Craters.GetLength(0); i++)
            {
                double dx = x - Craters[i, 0], dy = y - Craters[i, 1];
                double distance = Math.Sqrt(dx * dx + dy * dy) / Craters[i, 2];
                if (distance > 1.2) continue;
                double rim = Math.Max(0, 1 - Math.Abs(distance - 0.86) * 4);
                result += 0.15 * rim * (dx + dy) / Craters[i, 2] - 0.13 * Math.Max(0, 1 - distance);
            }
            return result;
        }

        private static double Noise(double x, double y)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double tx = x - ix, ty = y - iy;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            double top = Hash(ix, iy) * (1 - tx) + Hash(ix + 1, iy) * tx;
            double bottom = Hash(ix, iy + 1) * (1 - tx) + Hash(ix + 1, iy + 1) * tx;
            return top * (1 - ty) + bottom * ty;
        }

        private static double Hash(int x, int y)
        {
            uint n = unchecked((uint)(x * 374761393 + y * 668265263));
            n = unchecked((n ^ (n >> 13)) * 1274126177u);
            return ((n ^ (n >> 16)) & 65535) / 65535.0;
        }

        private static void Put(byte[] pixels, int index, double alpha, double red, double green, double blue)
        {
            pixels[index] = (byte)(blue * alpha);
            pixels[index + 1] = (byte)(green * alpha);
            pixels[index + 2] = (byte)(red * alpha);
            pixels[index + 3] = (byte)(255 * alpha);
        }

        private static double Limit(double value) { return Math.Max(0, Math.Min(1, value)); }
    }
}
