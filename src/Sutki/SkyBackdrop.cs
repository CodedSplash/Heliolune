using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Sutki
{
    internal sealed class SkyBackgroundLayer : FrameworkElement
    {
        private static readonly DependencyProperty PhaseProperty = RenderValue("Phase");
        private static readonly DependencyProperty NightProperty = RenderValue("Night");
        private static readonly DependencyProperty SunPositionProperty = RenderValue("SunPosition");

        public SkyBackgroundLayer(SkyDial dial)
        {
            IsHitTestVisible = false;
            BindingOperations.SetBinding(this, PhaseProperty, new Binding { Source = dial, Path = new PropertyPath(SkyDial.BackdropPhaseProperty) });
            BindingOperations.SetBinding(this, NightProperty, new Binding { Source = dial, Path = new PropertyPath(SkyDial.MoonProperty) });
            BindingOperations.SetBinding(this, SunPositionProperty, new Binding { Source = dial, Path = new PropertyPath(SkyDial.PositionProperty) });
        }

        protected override void OnRender(DrawingContext drawing)
        {
            SkyBackdrop.Draw(drawing, ActualWidth, ActualHeight, (double)GetValue(PhaseProperty),
                (double)GetValue(NightProperty), (double)GetValue(SunPositionProperty));
        }

        private static DependencyProperty RenderValue(string name)
        {
            return DependencyProperty.Register(name, typeof(double), typeof(SkyBackgroundLayer),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
        }
    }

    internal static class SkyBackdrop
    {
        // Render the cloud textures once. Only their positions and opacity change between frames.
        private static readonly ImageSource HighCloud = Cloud(0.3, false);
        private static readonly ImageSource MidCloud = Cloud(4.2, true);
        private static readonly ImageSource LowCloud = Cloud(2.7, true);
        private static readonly ImageSource HighCloudLit = Cloud(0.3, false, true);
        private static readonly ImageSource MidCloudLit = Cloud(4.2, true, true);
        private static readonly ImageSource LowCloudLit = Cloud(2.7, true, true);
        private static readonly ImageSource NightStarDust = CreateStarDust();
        private static readonly Brush Daylight = Tint(0.50, 0.50, 0xF8, 0xD4, 0xA6, 146);
        private static readonly Brush SunScattering = Tint(0.50, 0.50, 0xFF, 0xE8, 0xC2, 102);
        private static readonly Brush DayAir = Tint(0.55, 0.48, 0x81, 0xAE, 0xD1, 121);
        private static readonly Pen BirdInk = CreateBirdPen();
        private static readonly Geometry[] Wings = CreateWings();
        private static readonly Point[] Formation =
        {
            new Point(0, 0), new Point(-14, 6), new Point(-25, -3), new Point(-38, 10)
        };
        private static readonly Brush NightAir = Tint(0.60, 0.50, 0x63, 0x88, 0xB2, 24);
        private static readonly Brush Star = Tint(0.50, 0.50, 0xBB, 0xCA, 0xE7, 190);
        private static readonly Brush MeteorGlow = Tint(0.50, 0.50, 0xA1, 0xB7, 0xE3, 116);
        private static readonly Brush MeteorCore = FrozenBrush(Color.FromRgb(219, 228, 247));
        private static readonly Pen MeteorTrail = MakePen(MeteorCore, 0.8);
        private static readonly double[,] Stars =
        {
            { 26, 30, 1.35 }, { 53, 18, 0.9 }, { 79, 42, 1.2 }, { 111, 24, 1.45 },
            { 138, 52, 0.85 }, { 159, 16, 1.1 }, { 180, 28, 1.15 }, { 197, 48, 0.85 },
            { 217, 18, 1.25 }, { 241, 35, 1.05 }, { 264, 23, 1.45 }, { 278, 55, 1.2 },
            { 19, 73, 1.1 }, { 266, 88, 1.0 }, { 32, 103, 1.0 }, { 64, 94, 0.85 },
            { 86, 120, 1.1 }, { 104, 82, 0.85 }, { 176, 101, 1.05 }, { 232, 118, 1.15 },
            { 250, 104, 0.85 }, { 279, 126, 1.05 }
        };

        public static void Draw(DrawingContext dc, double width, double height, double phase, double moon, double sunPosition)
        {
            if (width <= 0 || height <= 0) return;
            double wave = phase * 2 * Math.PI;
            if (moon < 1)
            {
                dc.PushOpacity(1 - moon);
                // The main viewport reveals the bottom 140 DIPs of the shared, fixed-height background.
                // Anchor the daylight scene there and keep its size independent of the screen transition.
                dc.PushTransform(new MatrixTransform(width / 288, 0, 0, 1, 0, height - 166));
                double angle = Math.PI * sunPosition;
                double radius = Math.Max(5, (width - 74) / 2) * 288 / width;
                var sun = new Point(144 - radius * Math.Cos(angle), 134 - 69 * Math.Sin(angle));
                dc.DrawEllipse(DayAir, null, new Point(177, 38), 209, 128);
                dc.DrawEllipse(Daylight, null, new Point(sun.X, sun.Y - 5), 177, 113);
                dc.DrawEllipse(SunScattering, null, sun, 78, 69);
                DrawCloud(dc, HighCloud, HighCloudLit, sun, phase, 0.17, 30, 201, 48, 0.78);
                DrawCloud(dc, MidCloud, MidCloudLit, sun, phase, 0.62, 65, 193, 60, 0.86);
                DrawCloud(dc, LowCloud, LowCloudLit, sun, phase, 0.40, 109, 238, 58, 0.93);
                DrawFlock(dc, phase);
                dc.Pop();
                dc.Pop();
            }
            if (moon > 0)
            {
                dc.PushOpacity(moon);
                dc.PushTransform(new ScaleTransform(width / 288, height / 138));
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, 288, 138), 17, 17));
                dc.DrawEllipse(NightAir, null, new Point(150 + 26 * Math.Cos(wave), 73), 171, 85);
                dc.PushOpacity(0.81 + 0.12 * Math.Sin(wave - 0.6));
                dc.DrawImage(NightStarDust, new Rect(-12 + 7 * Math.Sin(wave), -8 + 3 * Math.Cos(wave), 312, 154));
                dc.Pop();
                for (int i = 0; i < Stars.GetLength(0); i++)
                {
                    double brightness = 0.40 + 0.48 * (0.5 + 0.5 * Math.Sin(wave * (2 + i % 3) + i * 2.399963));
                    var point = new Point(Stars[i, 0] + 2 * Math.Sin(wave + i), Stars[i, 1]);
                    dc.PushOpacity(brightness);
                    dc.DrawEllipse(Star, null, point, Stars[i, 2], Stars[i, 2]);
                    dc.Pop();
                }
                DrawMeteor(dc, phase, 0.31, new Point(181, 13), new Vector(99, 38), 1);
                DrawMeteor(dc, phase, 0.76, new Point(208, 12), new Vector(67, 26), 0.64);
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
        }

        private static void DrawCloud(DrawingContext dc, ImageSource texture, ImageSource lit, Point sun, double phase, double offset,
            double y, double width, double height, double opacity)
        {
            // Wrap outside the rounded viewport, where the entire texture is already out of sight.
            double cycle = phase + offset;
            cycle -= Math.Floor(cycle);
            double x = -width + cycle * (288 + width);
            dc.PushOpacity(opacity);
            var bounds = new Rect(x, y + 2.2 * Math.Sin(phase * Math.PI * 2 + offset * 8), width, height);
            dc.DrawImage(texture, bounds);
            double dx = (bounds.X + width * 0.5 - sun.X) / 115;
            double dy = (bounds.Y + height * 0.42 - sun.Y + 12) / 72;
            dc.PushOpacity(0.82 * Math.Exp(-dx * dx - dy * dy));
            dc.DrawImage(lit, bounds);
            dc.Pop();
            dc.Pop();
        }

        private static ImageSource CreateStarDust()
        {
            const int width = 312, height = 154;
            var pixels = new byte[width * height * 4];
            var random = new Random(1729);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double d = y - GalaxyHeight(x);
                double broad = Math.Exp(-Math.Pow(d / 19, 2));
                double core = Math.Exp(-Math.Pow((d - 5) / 10, 2));
                double patch = 0.56 + 0.21 * Math.Sin(x * 0.061 + y * 0.10)
                    + 0.15 * Math.Sin(x * 0.14 - y * 0.17) + 0.08 * Math.Cos(x * 0.31 + y * 0.27);
                double lane = Math.Exp(-Math.Pow((d + 2.5 * Math.Sin(x * 0.072)) / 3.1, 2));
                double edge = Math.Sin(Math.PI * x / (width - 1.0));
                double dust = (0.125 * broad + 0.22 * core) * patch * (1 - 0.72 * lane) * edge;
                double star = random.NextDouble() < 0.09 * broad * (1 - 0.8 * lane)
                    ? 0.11 + 0.26 * Math.Pow(random.NextDouble(), 3) : 0;
                double opacity = Math.Min(0.53, dust + star);
                int offset = (y * width + x) * 4;
                pixels[offset] = (byte)((202 + 24 * core) * opacity);
                pixels[offset + 1] = (byte)((163 + 34 * core) * opacity);
                pixels[offset + 2] = (byte)((130 + 67 * core) * opacity);
                pixels[offset + 3] = (byte)(255 * opacity);
            }
            var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
            image.Freeze();
            return image;
        }

        private static double GalaxyHeight(double x) { return 108 - x * 0.245 + 4 * Math.Sin(x * 0.028); }

        private static void DrawMeteor(DrawingContext dc, double phase, double start, Point origin, Vector flight, double intensity)
        {
            double age = phase - start;
            age -= Math.Floor(age);
            const double duration = 0.061;
            if (age >= duration) return;
            double progress = age / duration;
            double brightness = Math.Min(1, progress * 10) * Math.Pow(1 - progress, 0.65) * intensity;
            Point head = origin + flight * progress;
            Vector tail = flight * (0.37 * Math.Min(1, progress * 5));
            const int segments = 9;
            for (int i = 0; i < segments; i++)
            {
                double fraction = i / (double)segments;
                dc.PushOpacity(brightness * Math.Pow(1 - fraction, 2.1));
                dc.DrawLine(MeteorTrail, head - tail * fraction, head - tail * ((i + 1.0) / segments));
                dc.Pop();
            }
            dc.PushOpacity(brightness);
            dc.DrawEllipse(MeteorGlow, null, head, 4.4, 3.6);
            dc.DrawEllipse(MeteorCore, null, head, 0.94, 0.72);
            dc.Pop();
        }

        private static void DrawFlock(DrawingContext dc, double phase)
        {
            // Start the flock in view at the panel's initial phase, then let it leave naturally.
            double progress = phase + 0.08;
            progress -= Math.Floor(progress);
            const double flightEnd = 0.75;
            if (progress >= flightEnd) return;
            double travel = progress / flightEnd;
            double envelope = Math.Min(1, Math.Min(travel, 1 - travel) * 6);
            double x = 15 + 263 * travel;
            double y = 62 - 13 * Math.Sin(travel * Math.PI) + 3 * Math.Sin(travel * 11);
            dc.PushOpacity(envelope * 0.83);
            for (int i = 0; i < Formation.Length; i++)
            {
                Point offset = Formation[i];
                // Glide between wingbeats. Each bird is slightly out of phase with the others.
                double beat = progress * (29 + i * 2) + i * 0.17;
                beat -= Math.Floor(beat);
                double tilt = beat < 0.66 ? Math.Sin(2 * Math.PI * beat / 0.66) : 0;
                int frame = (int)Math.Round((tilt + 1) * (Wings.Length - 1) / 2);
                dc.PushTransform(new TranslateTransform(x + offset.X + 2.5 * Math.Sin(travel * 13 + i),
                    y + offset.Y + 1.6 * Math.Sin(travel * 17 + i)));
                var bank = new RotateTransform(9 * Math.Cos(travel * 5 + i * 0.27));
                dc.PushTransform(bank);
                dc.PushTransform(new ScaleTransform(1.27 - i * 0.09, 1.33 - i * 0.10));
                dc.DrawGeometry(null, BirdInk, Wings[frame]);
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
            dc.Pop();
        }

        private static Geometry[] CreateWings()
        {
            var frames = new Geometry[17];
            for (int i = 0; i < frames.Length; i++)
            {
                double tip = -1.5 + i * 3.2 / (frames.Length - 1);
                var shape = new StreamGeometry();
                using (StreamGeometryContext path = shape.Open())
                {
                    path.BeginFigure(new Point(-3.8, tip), false, false);
                    path.QuadraticBezierTo(new Point(-1.7, tip - 0.7), new Point(0, 0.9), true, true);
                    path.QuadraticBezierTo(new Point(1.6, tip - 0.9), new Point(3.6, tip - 0.35), true, true);
                }
                shape.Freeze();
                frames[i] = shape;
            }
            return frames;
        }

        private static Pen CreateBirdPen()
        {
            return MakePen(FrozenBrush(Color.FromRgb(49, 65, 85)), 1.15);
        }

        private static Brush FrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen MakePen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness)
            {
                StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round
            };
            pen.Freeze();
            return pen;
        }

        private static BitmapSource Cloud(double variation, bool dense, bool lit = false)
        {
            const int width = 232, height = 56;
            var pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double u = x / (width - 1.0), v = y / (height - 1.0);
                double density = 0, litDensity = 0;
                for (int strand = 0; strand < 5; strand++)
                {
                    double shift = variation + strand * 1.83;
                    double center = 0.31 + 0.084 * strand + 0.19 * (0.5 - u)
                        + 0.067 * Math.Sin(u * 6.7 + shift) + 0.024 * Math.Sin(u * 19 + shift);
                    double thickness = (dense ? 0.091 : 0.053) + 0.045 * (0.5 + 0.5 * Math.Cos(u * 9 + shift));
                    double distance = (v - center) / thickness;
                    double span = (u - (0.47 + 0.18 * Math.Sin(shift))) / (0.21 + 0.035 * strand);
                    double texture = 0.77 + 0.14 * Math.Sin(u * 27 + v * 13 + shift)
                        + 0.06 * Math.Sin(u * 59 + v * 21 - shift) + 0.03 * Math.Sin(u * 105 - v * 47 + shift);
                    double wisps = Math.Exp(-distance * distance - span * span) * texture;
                    density += wisps;
                    litDensity += wisps * (0.64 - 0.30 * Math.Tanh(distance + (strand - 2) * 0.35));
                }
                double fade = Math.Pow(Math.Sin(Math.PI * u), 0.83) * Math.Sin(Math.PI * v);
                double opacity = fade * (1 - Math.Exp(-density * (dense ? 2.15 : 1.65)));
                int offset = (y * width + x) * 4;
                // The muted blue underside separates wisps from the light gray panel.
                double light = litDensity / density;
                if (lit)
                {
                    opacity *= light;
                    pixels[offset] = (byte)(184 * opacity);
                    pixels[offset + 1] = (byte)(224 * opacity);
                    pixels[offset + 2] = (byte)(255 * opacity);
                    pixels[offset + 3] = (byte)(255 * opacity);
                    continue;
                }
                pixels[offset] = (byte)((137 + 118 * light) * opacity);
                pixels[offset + 1] = (byte)((115 + 139 * light) * opacity);
                pixels[offset + 2] = (byte)((96 + 157 * light) * opacity);
                pixels[offset + 3] = (byte)(255 * opacity);
            }
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
            bitmap.Freeze();
            return bitmap;
        }

        private static Brush Tint(double x, double y, byte red, byte green, byte blue, byte alpha)
        {
            var brush = new RadialGradientBrush(Color.FromArgb(alpha, red, green, blue),
                Color.FromArgb(0, red, green, blue)) { GradientOrigin = new Point(x, y) };
            brush.Freeze();
            return brush;
        }
    }
}
