using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sutki
{
    internal sealed class SkyDial : FrameworkElement
    {
        internal static readonly DependencyProperty PositionProperty = Animated("Position", 0);
        internal static readonly DependencyProperty MoonProperty = Animated("Moon", 0);
        private static readonly DependencyProperty CrescentProperty = Animated("Crescent", 0);
        private static readonly DependencyProperty MarkerOpacityProperty = Animated("MarkerOpacity", 1);
        private static readonly DependencyProperty GlowProperty = Animated("Glow", 0.90);
        private static readonly DependencyProperty DriftProperty = Animated("Drift", 0.34);
        internal static readonly DependencyProperty BackdropPhaseProperty = Animated("BackdropPhase", 0.15);
        private static readonly Pen DayTrack = MakePen(BrushOf("#68768D"), 1.05);
        private static readonly Pen NightTrack = MakePen(BrushOf("#586881"), 1.05);
        private static readonly Pen DayArc = MakePen(BrushOf("#91592F"), 1.4);
        private static readonly Pen NightArc = MakePen(BrushOf("#B0C1E6"), 1.4);
        private static readonly Brush DayTick = BrushOf("#596981");
        private static readonly Brush NightTick = BrushOf("#99AAC7");
        private static readonly Brush Ember = BrushOf("#9E512F");
        private static readonly Brush Gold = BrushOf("#AC671D");
        private static readonly Brush HotCore = BrushOf("#F7CA81");
        private static readonly Brush EmberGlow = FrozenGlow(Color.FromArgb(85, 225, 126, 42));
        private static readonly Brush Ice = BrushOf("#AABAD5");
        private static readonly Brush Blue = BrushOf("#7994BE");
        private static readonly Pen EmberTrail = MakePen(Ember, 1.3);
        private static readonly Pen GoldTrail = MakePen(Gold, 1.15);
        private static readonly Pen IceTrail = MakePen(Ice, 1.1);
        private static readonly Brush MoonSparkGlow = FrozenGlow(Color.FromArgb(75, 129, 160, 215));
        private static readonly Brush Halo = new RadialGradientBrush(Color.FromArgb(65, 255, 237, 201), Colors.Transparent);
        private static readonly Brush MoonHalo = new RadialGradientBrush(Color.FromArgb(31, 155, 176, 219), Colors.Transparent);
        private double eveningMark;
        private bool ready;
        private double lastPosition;
        private bool lastDark;
        private bool glowing;
        private double trackWidth = -1, drawnPosition = -1;
        private Geometry track, traveled;

        private static DependencyProperty Animated(string name, double value)
        {
            return DependencyProperty.Register(name, typeof(double), typeof(SkyDial),
                new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));
        }

        public void Update(Schedule schedule, bool animate)
        {
            if (glowing != animate)
            {
                glowing = animate;
                BeginAnimation(GlowProperty, animate ? new DoubleAnimation(0.84, 1, TimeSpan.FromSeconds(2.7))
                {
                    AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                } : null);
                BeginAnimation(DriftProperty, animate ? new DoubleAnimation(0, 1, TimeSpan.FromSeconds(8))
                    { RepeatBehavior = RepeatBehavior.Forever } : null);
                BeginAnimation(BackdropPhaseProperty, animate ? new DoubleAnimation(0.15, 1.15, TimeSpan.FromSeconds(22))
                    { RepeatBehavior = RepeatBehavior.Forever } : null);
            }
            bool marksChanged = eveningMark != schedule.EveningMark;
            eveningMark = schedule.EveningMark;
            // Once a minute is enough for this scale. The phase still updates at its exact boundary.
            double position = Math.Floor(schedule.Progress * 1440 + 0.000001) / 1440;
            if (ready && position == lastPosition && lastDark == schedule.Dark && !marksChanged) return;
            bool wrap = Math.Abs(position - lastPosition) > 0.5;
            if (!ready || position != lastPosition)
            {
                Move(PositionProperty, position, animate && !wrap, 500);
                if (ready && animate && wrap && !schedule.Dark)
                    BeginAnimation(MarkerOpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600)));
                lastPosition = position;
            }
            Move(MoonProperty, schedule.Dark ? 1 : 0, animate, 680);
            Move(CrescentProperty, 1 - 0.62 * schedule.NightProgress, animate, 720);
            ready = true;
            lastDark = schedule.Dark;
            InvalidateVisual();
        }

        private void Move(DependencyProperty property, double target, bool animate, int duration)
        {
            if (ready && (double)GetAnimationBaseValue(property) == target) return;
            double from = (double)GetValue(property);
            SetValue(property, target);
            BeginAnimation(property, ready && animate && from != target
                ? new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(duration))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop
                } : null);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double width = ActualWidth;
            double rx = Math.Max(5, (width - 74) / 2);
            const double ry = 69;
            var center = new Point(width / 2, 106);
            double position = (double)GetValue(PositionProperty);
            double moon = (double)GetValue(MoonProperty);
            double glow = (double)GetValue(GlowProperty);
            if (width != trackWidth)
            {
                trackWidth = width;
                track = Arc(center, rx, ry, 1);
                drawnPosition = -1;
            }
            if (drawnPosition != position)
            {
                drawnPosition = position;
                traveled = position > 0 ? Arc(center, rx, ry, position) : null;
            }
            if (moon < 1) DrawTrack(dc, track, traveled, center, rx, ry, false, 1 - moon);
            if (moon > 0) DrawTrack(dc, track, traveled, center, rx, ry, true, moon);

            Point body = At(center, rx, ry, position);
            double size = 12.5 + moon;
            var bounds = new Rect(body.X - size, body.Y - size, 2 * size, 2 * size);
            dc.PushOpacity((double)GetValue(MarkerOpacityProperty));

            if (moon < 1)
            {
                dc.PushOpacity(1 - moon);
                dc.DrawEllipse(Halo, null, body, 33 * glow, 33 * glow);
                DrawEmbers(dc, body, (double)GetValue(DriftProperty));
                dc.DrawImage(Celestial.Sun, bounds);
                dc.Pop();
            }
            if (moon > 0)
            {
                dc.PushOpacity(moon);
                dc.DrawEllipse(MoonHalo, null, body, 25 * glow, 25 * glow);
                DrawMoonSparks(dc, body, (double)GetValue(DriftProperty));
                dc.PushOpacity(0.19);
                dc.DrawImage(Celestial.Moon, bounds);
                dc.Pop();

                double phase = (double)GetValue(CrescentProperty);
                var circle = new EllipseGeometry(body, size, size);
                var terminator = new EllipseGeometry(new Point(body.X + size * phase, body.Y - size * 0.26), size, size);
                dc.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude, circle, terminator));
                dc.PushOpacity(0.78);
                dc.DrawImage(Celestial.Moon, bounds);
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
            dc.Pop();
        }

        private void DrawTrack(DrawingContext dc, Geometry track, Geometry traveled, Point center,
            double rx, double ry, bool night, double opacity)
        {
            dc.PushOpacity(opacity);
            dc.DrawGeometry(null, night ? NightTrack : DayTrack, track);
            if (traveled != null) dc.DrawGeometry(null, night ? NightArc : DayArc, traveled);
            Brush tick = night ? NightTick : DayTick;
            dc.DrawEllipse(tick, null, At(center, rx, ry, 0), 1.8, 1.8);
            dc.DrawEllipse(tick, null, At(center, rx, ry, eveningMark), 1.8, 1.8);
            dc.DrawEllipse(tick, null, At(center, rx, ry, 1), 1.8, 1.8);
            dc.Pop();
        }

        private static void DrawEmbers(DrawingContext dc, Point body, double drift)
        {
            for (int i = 0; i < 21; i++)
            {
                double time = drift * (i % 3 + 3) + i * 0.618034;
                double life = time - Math.Floor(time);
                double envelope = Math.Min(1, life * 12) * Math.Pow(1 - life, 0.78);
                Point point = EmberPosition(body, i, life);
                double size = (i % 3 == 0 ? 1.75 : 1.18) * (1 - life * 0.46);
                Pen trail = i % 3 == 0 ? GoldTrail : EmberTrail;
                Point front = point;
                for (int j = 1; j <= 4; j++)
                {
                    Point tail = EmberPosition(body, i, Math.Max(0, life - j * 0.028));
                    dc.PushOpacity(envelope * (0.59 - j * 0.11));
                    dc.DrawLine(trail, front, tail);
                    dc.Pop();
                    front = tail;
                }
                dc.PushOpacity(envelope);
                dc.DrawEllipse(EmberGlow, null, point, size * 3, size * 3);
                dc.DrawEllipse(i % 3 == 0 ? Gold : Ember, null, point, size, size);
                dc.DrawEllipse(HotCore, null, point, size * 0.54, size * 0.54);
                dc.Pop();
            }
        }

        private static Point EmberPosition(Point body, int ember, double life)
        {
            double distance = life * (1.6 - 0.6 * life);
            double radius = 12 + (26 + ember % 6 * 3.1) * distance;
            double curl = (ember % 2 == 0 ? 0.95 : -0.72) * life * life;
            return Orbit(body, ember * 2.399963 + curl, radius, 0.76);
        }

        private static void DrawMoonSparks(DrawingContext dc, Point body, double drift)
        {
            double turn = 2 * Math.PI * drift;
            for (int i = 0; i < 14; i++)
            {
                double angle = turn * (i % 2 == 0 ? 1 : -1) + i * 2.399963;
                double radius = 19 + i % 5 * 5 + 3 * Math.Sin(turn * 2 + i);
                double twinkle = 0.5 + 0.5 * Math.Sin(turn * (i % 3 + 2) + i * 1.31);
                double opacity = 0.23 + 0.49 * twinkle * twinkle;
                Point point = Orbit(body, angle, radius, 0.66);
                double size = (i % 4 == 0 ? 1.65 : 1.15) + twinkle * 0.35;
                dc.PushOpacity(opacity);
                dc.DrawEllipse(MoonSparkGlow, null, point, size * 2.5, size * 2.5);
                dc.DrawEllipse(i % 3 == 0 ? Blue : Ice, null, point, size, size);
                if (i % 4 == 0 && twinkle > 0.78)
                {
                    double arm = (twinkle - 0.78) * 10;
                    dc.DrawLine(IceTrail, new Point(point.X - arm, point.Y), new Point(point.X + arm, point.Y));
                    dc.DrawLine(IceTrail, new Point(point.X, point.Y - arm * 1.3), new Point(point.X, point.Y + arm * 1.3));
                }
                dc.Pop();
            }
        }

        private static Point Orbit(Point center, double angle, double radius, double flatten)
        {
            return new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle) * flatten);
        }

        private static Brush FrozenGlow(Color color)
        {
            var brush = new RadialGradientBrush(color, Colors.Transparent);
            brush.Freeze();
            return brush;
        }

        private static Geometry Arc(Point center, double rx, double ry, double end)
        {
            var path = new StreamGeometry();
            using (StreamGeometryContext context = path.Open())
            {
                context.BeginFigure(At(center, rx, ry, 0), false, false);
                context.ArcTo(At(center, rx, ry, end), new Size(rx, ry), 0, false, SweepDirection.Clockwise, true, false);
            }
            path.Freeze();
            return path;
        }

        private static Point At(Point center, double rx, double ry, double fraction)
        {
            double angle = fraction * Math.PI;
            return new Point(center.X - rx * Math.Cos(angle), center.Y - ry * Math.Sin(angle));
        }

        private static SolidColorBrush BrushOf(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        private static Pen MakePen(Brush brush, double width)
        {
            var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            return pen;
        }
    }
}
