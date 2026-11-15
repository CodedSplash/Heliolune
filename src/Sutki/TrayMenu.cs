using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Drawing = System.Drawing;

namespace Sutki
{
    internal sealed class TrayMenu : Window
    {
        private readonly AnimatedButton[] actions = new AnimatedButton[3];
        private readonly Border surface;
        private bool choosing;

        public TrayMenu(bool dark, Action<TrayCommand> choose, bool english)
        {
            Width = 223;
            SizeToContent = SizeToContent.Height;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;
            Topmost = true;
            Language = XmlLanguage.GetLanguage(english ? "en" : "ru");
            Title = english ? "Heliolune menu" : "Меню «Суток»";
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            FontSize = 13;
            UseLayoutRounding = true;
            using (var stream = typeof(TrayMenu).Assembly.GetManifestResourceStream("Styles.xaml"))
                Resources = (ResourceDictionary)XamlReader.Load(stream);
            Resources["Text"] = Brush(dark ? "#EDF0F6" : "#202939");
            Resources["Muted"] = Brush(dark ? "#A9B4C7" : "#343E50");
            Resources["Accent"] = Brush(dark ? "#2E4461" : "#96AEC8");

            var items = new StackPanel();
            string[] labels = { LocalizedText.OpenDial.In(english), LocalizedText.Settings.In(english),
                english ? "Exit application" : "Закрыть приложение" };
            string[] glyphs = { "\uE121", "\uE713", "\uE8BB" };
            TrayCommand[] commands = { TrayCommand.Glance, TrayCommand.Settings, TrayCommand.Exit };
            Brush line = Brush(dark ? "#363E4C" : "#95A3B4");
            for (int i = 0; i < actions.Length; i++)
            {
                if (i == 2) items.Children.Add(new Border { Height = 1, Background = line, Margin = new Thickness(6, 3, 6, 3) });
                TrayCommand command = commands[i];
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = glyphs[i], Width = 24, VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets") });
                row.Children.Add(new TextBlock { Text = labels[i], VerticalAlignment = VerticalAlignment.Center });
                var button = new AnimatedButton { Style = (Style)Resources["QuietButton"], Height = 34,
                    HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(9, 0, 9, 0), Content = row };
                System.Windows.Automation.AutomationProperties.SetName(button, labels[i]);
                button.Click += delegate
                {
                    choosing = true;
                    Close();
                    choose(command);
                };
                actions[i] = button;
                items.Children.Add(button);
            }
            surface = new Border { Child = items, Padding = new Thickness(5), CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1), BorderBrush = line, Background = Brush(dark ? "#1C212B" : "#C4CDD7") };
            Content = surface;
            PreviewKeyDown += Navigate;
            Deactivated += delegate { if (!choosing && IsVisible) Close(); };
        }

        public void Open(Drawing.Point point)
        {
            IntPtr handle = new WindowInteropHelper(this).EnsureHandle();
            SetWindowPos(handle, IntPtr.Zero, point.X, point.Y, 0, 0, 0x15);
            var bounds = System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
            double scale = GetDpiForWindow(handle) / 96.0;
            if (scale <= 0) scale = 1;
            surface.Measure(new Size(Width, double.PositiveInfinity));
            int width = (int)Math.Ceiling(Width * scale), height = (int)Math.Ceiling(surface.DesiredSize.Height * scale);
            int x = Math.Max(bounds.Left, Math.Min(point.X, bounds.Right - width));
            int y = Math.Max(bounds.Top, Math.Min(point.Y - height - 4, bounds.Bottom - height));
            SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x15);
            Show();
            SetForegroundWindow(handle);
            Activate();
            actions[0].Focus();
            var fade = new DoubleAnimation(0.65, 1, TimeSpan.FromMilliseconds(155)) { FillBehavior = FillBehavior.Stop };
            surface.BeginAnimation(OpacityProperty, fade);
        }

        private void Navigate(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape) Close();
            else if (args.Key == Key.Up || args.Key == Key.Down || args.Key == Key.Home || args.Key == Key.End)
            {
                int current = Array.FindIndex(actions, button => button.IsKeyboardFocusWithin);
                int index;
                if (args.Key == Key.Home) index = 0;
                else if (args.Key == Key.End) index = actions.Length - 1;
                else index = (Math.Max(0, current) + (args.Key == Key.Up ? -1 : 1) + actions.Length) % actions.Length;
                actions[index].Focus();
            }
            else return;
            args.Handled = true;
        }

        private static Brush Brush(string hex) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
