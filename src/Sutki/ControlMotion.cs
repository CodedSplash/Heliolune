using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Sutki
{
    internal static class ControlMotion
    {
        public static void To(Animatable target, DependencyProperty property, double value, int milliseconds, bool animate)
        {
            double current = (double)target.GetValue(property);
            target.SetValue(property, value);
            target.BeginAnimation(property, Transition(current, value, milliseconds, animate));
        }

        public static void To(UIElement target, DependencyProperty property, double value, int milliseconds, bool animate)
        {
            double current = (double)target.GetValue(property);
            target.SetValue(property, value);
            target.BeginAnimation(property, Transition(current, value, milliseconds, animate));
        }

        private static DoubleAnimation Transition(double from, double to, int milliseconds, bool animate)
        {
            if (!animate || from == to) return null;
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
        }
    }

    internal sealed class AnimatedButton : Button
    {
        private ScaleTransform scale;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var body = GetTemplateChild("Body") as FrameworkElement;
            scale = null;
            if (body == null) return;
            scale = new ScaleTransform(1, 1);
            body.RenderTransform = scale;
            UpdatePress(false);
        }

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
            UpdatePress(IsVisible);
        }

        private void UpdatePress(bool animate)
        {
            if (scale == null) return;
            double size = IsPressed ? 0.90 : 1;
            int duration = IsPressed ? 80 : 160;
            ControlMotion.To(scale, ScaleTransform.ScaleXProperty, size, duration, animate);
            ControlMotion.To(scale, ScaleTransform.ScaleYProperty, size, duration, animate);
        }
    }

    internal sealed class AnimatedSwitch : CheckBox
    {
        private TranslateTransform thumb;
        private ScaleTransform press;
        private UIElement accent;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            var knob = GetTemplateChild("Knob") as FrameworkElement;
            accent = GetTemplateChild("TrackAccent") as UIElement;
            thumb = null;
            press = null;
            if (knob != null)
            {
                press = new ScaleTransform(1, 1);
                thumb = new TranslateTransform();
                var transform = new TransformGroup();
                transform.Children.Add(press);
                transform.Children.Add(thumb);
                knob.RenderTransform = transform;
            }
            UpdateSelection(false);
            UpdatePress(false);
        }

        protected override void OnChecked(RoutedEventArgs e) { base.OnChecked(e); UpdateSelection(IsVisible); }
        protected override void OnUnchecked(RoutedEventArgs e) { base.OnUnchecked(e); UpdateSelection(IsVisible); }

        protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnIsPressedChanged(e);
            UpdatePress(IsVisible);
        }

        private void UpdateSelection(bool animate)
        {
            if (thumb == null || accent == null) return;
            bool enabled = IsChecked == true;
            ControlMotion.To(thumb, TranslateTransform.XProperty, enabled ? 14 : 0, 195, animate);
            ControlMotion.To(accent, OpacityProperty, enabled ? 1 : 0, 195, animate);
        }

        private void UpdatePress(bool animate)
        {
            if (press == null) return;
            int duration = IsPressed ? 85 : 145;
            ControlMotion.To(press, ScaleTransform.ScaleXProperty, IsPressed ? 1.18 : 1, duration, animate);
        }
    }
}
