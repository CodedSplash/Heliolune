using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Drawing = System.Drawing;

namespace Sutki
{
    internal sealed class Panel : Window
    {
        private const double PanelWidth = 306;
        private const double HostHeight = 310;
        private const double ShortHeight = 140;
        private const int ScreenDuration = 290;
        private readonly StackPanel preferences = new StackPanel();
        private readonly SkyDial dial = new SkyDial { IsHitTestVisible = false };
        private readonly Grid glance = new Grid { Height = 138 };
        private readonly TextBox[] times = new TextBox[2];
        private readonly TextBlock[] periodLabels = new TextBlock[2];
        private readonly MenuItem[] clearSources = new MenuItem[2];
        private readonly TextBlock settingsTitle, wallpaperTitle, languageLabel;
        private readonly AnimatedSwitch startupToggle;
        private readonly Button russianButton, englishButton;
        private readonly Button[] sources = new Button[2];
        private readonly string[] paths = new string[4];
        private readonly TextBlock status;
        private readonly Button files, folders, settingsButton, saveButton, backButton;
        private readonly TranslateTransform modePosition = new TranslateTransform();
        private readonly Border surface;
        private readonly RectangleGeometry viewport = new RectangleGeometry(Rect.Empty, 19, 19);
        private readonly ScrollViewer scroll;
        private readonly TranslateTransform slide = new TranslateTransform();
        private readonly Action<Settings> save;
        private readonly Action<bool> setStartup;
        private readonly Action<bool> setLanguage;
        private bool english;
        private bool folderMode, picking, startup, unsaved, editing, dragging;
        private bool dragArmed;
        private Drawing.Point dragPointer, dragOrigin;
        private bool? darkTheme;
        private int screenVersion;
        private Drawing.Rectangle anchor;
        private Drawing.Point? draggedBottom;
        private Schedule schedule;
        private LocalizedText reported, displayedStatus;
        private bool reportedError, displayedError;

        public Panel(Settings settings, bool startup, Action<Settings> save, Action<bool> setStartup, Action<bool> setLanguage)
        {
            this.save = save;
            this.setStartup = setStartup;
            this.setLanguage = setLanguage;
            english = settings.English;
            this.startup = startup;
            Width = PanelWidth;
            Height = HostHeight;
            AllowsTransparency = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = WindowsUi.TextFont;
            FontSize = 13;
            UseLayoutRounding = true;
            using (var stream = typeof(Panel).Assembly.GetManifestResourceStream("Styles.xaml"))
                Resources = (ResourceDictionary)XamlReader.Load(stream);
            Resources["IconFont"] = WindowsUi.IconFont;

            glance.Children.Add(dial);
            glance.Cursor = Cursors.SizeAll;
            settingsButton = IconButton("\uE713", () => View(true));
            settingsButton.HorizontalAlignment = HorizontalAlignment.Center;
            settingsButton.VerticalAlignment = VerticalAlignment.Bottom;
            settingsButton.Margin = new Thickness(0, 0, 0, 5);
            settingsButton.Style = (Style)Resources["Gear"];
            glance.Children.Add(settingsButton);

            var header = Row(1, 0, 0);
            settingsTitle = Label("", 17, "Text", true);
            settingsTitle.VerticalAlignment = VerticalAlignment.Center;
            Put(header, settingsTitle, 0);
            saveButton = IconButton("\uE73E", Save);
            Put(header, saveButton, 1);
            backButton = IconButton("\uE72B", () => View(false));
            Put(header, backButton, 2);
            preferences.Children.Add(header);

            var timeRow = Row(1, 1);
            timeRow.Margin = new Thickness(0, 7, 0, 0);
            for (int i = 0; i < times.Length; i++)
            {
                var field = Row(1, 0);
                var label = Label("", 12, "Text", false);
                periodLabels[i] = label;
                label.Margin = new Thickness(9, 0, 0, 0);
                label.VerticalAlignment = VerticalAlignment.Center;
                Put(field, label, 0);
                var input = new TextBox { Style = (Style)Resources["Time"], MaxLength = 5,
                    Text = Schedule.Format(settings.Starts[i]), Width = 56 };
                times[i] = input;
                input.GotKeyboardFocus += delegate { input.SelectAll(); };
                input.TextChanged += delegate { MarkDirty(); };
                input.PreviewKeyDown += (sender, args) =>
                {
                    if (args.Key != Key.Up && args.Key != Key.Down) return;
                    int minute;
                    if (Schedule.Parse(input.Text, out minute))
                    {
                        int step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 15;
                        input.Text = Schedule.Format((minute + (args.Key == Key.Up ? step : -step) + 1440) % 1440);
                        input.SelectAll();
                    }
                    args.Handled = true;
                };
                Put(field, input, 1);
                var card = Card(field);
                card.Margin = i == 0 ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
                Put(timeRow, card, i);
            }
            preferences.Children.Add(timeRow);

            var wallpaperHeader = Row(1, 0);
            wallpaperHeader.Margin = new Thickness(0, 8, 0, 0);
            wallpaperTitle = Label("", 13, "Text", true);
            wallpaperTitle.VerticalAlignment = VerticalAlignment.Center;
            Put(wallpaperHeader, wallpaperTitle, 0);
            files = IconButton("\uEB9F", () => SetMode(false));
            folders = IconButton("\uE8B7", () => SetMode(true));
            var modes = Row(1, 1);
            modes.Width = 60;
            var selection = new Border { Margin = new Thickness(1), CornerRadius = new CornerRadius(9),
                IsHitTestVisible = false, RenderTransform = modePosition };
            selection.SetResourceReference(Border.BackgroundProperty, "Accent");
            Put(modes, selection, 0);
            Put(modes, files, 0);
            Put(modes, folders, 1);
            Put(wallpaperHeader, modes, 1);
            preferences.Children.Add(wallpaperHeader);
            for (int i = 0; i < sources.Length; i++) AddSource(i);

            startupToggle = new AnimatedSwitch { Style = (Style)Resources["Switch"],
                IsChecked = startup, Margin = new Thickness(0, 9, 0, 0) };
            startupToggle.Click += delegate
            {
                try
                {
                    bool enabled = startupToggle.IsChecked == true;
                    this.setStartup(enabled);
                    this.startup = enabled;
                }
                catch (Exception error)
                {
                    startupToggle.IsChecked = this.startup;
                    ShowStatus(new LocalizedText("Автозапуск: ", "Startup: ").WithError(error), true);
                }
            };
            preferences.Children.Add(startupToggle);

            var languages = Row(1, 0, 0);
            languages.Margin = new Thickness(0, 5, 0, 0);
            languageLabel = Label("", 13, "Text", false);
            languageLabel.VerticalAlignment = VerticalAlignment.Center;
            Put(languages, languageLabel, 0);
            russianButton = LanguageButton("Русский", false);
            englishButton = LanguageButton("English", true);
            Put(languages, russianButton, 1);
            Put(languages, englishButton, 2);
            preferences.Children.Add(languages);

            status = Label("", 11, "Muted", false);
            status.TextWrapping = TextWrapping.Wrap;
            status.Margin = new Thickness(0, 7, 0, 0);
            status.Visibility = Visibility.Collapsed;
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
            preferences.Children.Add(status);

            scroll = new ScrollViewer { Content = preferences, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(12, 8, 12, 10) };
            glance.VerticalAlignment = VerticalAlignment.Bottom;
            var layers = new Grid();
            layers.Children.Add(new SkyBackgroundLayer(dial));
            layers.Children.Add(glance);
            layers.Children.Add(scroll);
            var outline = new System.Windows.Shapes.Path { Data = viewport, IsHitTestVisible = false, StrokeThickness = 2 };
            outline.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Line");
            // A Canvas keeps the animated outline out of the measure/arrange path.
            layers.Children.Add(new Canvas { IsHitTestVisible = false, Children = { outline } });
            surface = new Border { Child = layers, Clip = viewport,
                RenderTransform = slide, Margin = new Thickness(8, 7, 8, 9) };
            Content = new Grid { Children = { surface } };

            paths[0] = settings.LightFile ?? "";
            paths[1] = settings.DarkFile ?? "";
            paths[2] = settings.LightFolder ?? "";
            paths[3] = settings.DarkFolder ?? "";
            folderMode = settings.Folders;
            ApplyLanguage();
            UpdateSaveButton();
            scroll.Visibility = Visibility.Collapsed;
            glance.Opacity = 1;
            ApplyTheme();
            ResizeSurface(false);

            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    if (editing) View(false);
                    else Hide();
                    args.Handled = true;
                }
                else if (editing && args.Key == Key.Enter && Keyboard.FocusedElement is TextBox)
                {
                    Save();
                    args.Handled = true;
                }
            };
            PreviewMouseLeftButtonDown += BeginPanelDrag;
            PreviewMouseMove += MovePanelDrag;
            PreviewMouseLeftButtonUp += delegate { EndPanelDrag(); };
            LostMouseCapture += delegate { EndPanelDrag(); };
            Deactivated += delegate
            {
                if (dragArmed) EndPanelDrag();
                if (!picking) Hide();
            };
            IsVisibleChanged += delegate { if (schedule != null) Refresh(schedule); };
        }

        private void AddSource(int index)
        {
            var button = new AnimatedButton { Style = (Style)Resources["Source"], Margin = new Thickness(0, 3, 0, 0) };
            button.Click += delegate { Browse(index); };
            button.ContextMenu = new ContextMenu();
            var clear = new MenuItem();
            clearSources[index] = clear;
            clear.Click += delegate
            {
                paths[Slot(index)] = "";
                UpdateSources();
                MarkDirty();
            };
            button.ContextMenu.Items.Add(clear);
            button.ContextMenu.Opened += delegate { picking = true; clear.IsEnabled = paths[Slot(index)].Length != 0; };
            button.ContextMenu.Closed += delegate { picking = false; };
            sources[index] = button;
            preferences.Children.Add(button);
        }

        private int Slot(int index) { return index + (folderMode ? 2 : 0); }

        private void Browse(int index)
        {
            int slot = Slot(index);
            picking = true;
            try
            {
                string selected;
                if (folderMode)
                    selected = FolderPicker.Pick(new WindowInteropHelper(this).Handle, paths[slot], Tr("Выберите папку с обоями", "Choose a wallpaper folder"));
                else
                {
                    var picker = new Microsoft.Win32.OpenFileDialog { Title = Tr("Выберите обои", "Choose a wallpaper"),
                        Filter = Tr("Изображения", "Images") + " (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", CheckFileExists = true };
                    if (File.Exists(paths[slot])) picker.InitialDirectory = Path.GetDirectoryName(paths[slot]);
                    selected = picker.ShowDialog(this) == true ? picker.FileName : null;
                }
                if (selected != null && selected != paths[slot])
                {
                    paths[slot] = selected;
                    UpdateSources();
                    MarkDirty();
                }
            }
            catch (Exception error) { ShowStatus(default(LocalizedText).WithError(error), true); }
            finally { Activate(); picking = false; }
        }

        private void SetMode(bool value)
        {
            if (folderMode == value) return;
            folderMode = value;
            UpdateModeSelection(IsVisible);
            UpdateSources();
            double shift = value ? 4 : -4;
            foreach (Button source in sources)
            {
                var content = (Grid)source.Content;
                content.RenderTransform = new TranslateTransform(shift, 0);
                ControlMotion.To(content.RenderTransform, TranslateTransform.XProperty, 0, 185, IsVisible);
                content.Opacity = 0.52;
                ControlMotion.To(content, OpacityProperty, 1, 185, IsVisible);
            }
            MarkDirty();
        }

        private void UpdateModeSelection(bool animate)
        {
            ControlMotion.To(modePosition, TranslateTransform.XProperty, folderMode ? 30 : 0, 220, animate);
            files.SetResourceReference(Control.ForegroundProperty, folderMode ? "Text" : "AccentText");
            folders.SetResourceReference(Control.ForegroundProperty, folderMode ? "AccentText" : "Text");
            string selected = Tr("Выбрано", "Selected");
            AutomationProperties.SetItemStatus(files, folderMode ? "" : selected);
            AutomationProperties.SetItemStatus(folders, folderMode ? selected : "");
        }

        private void UpdateSources()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                string path = paths[Slot(i)];
                string title = i == 0 ? Tr("Светлые", "Day") : Tr("Тёмные", "Night");
                string name = path.Length == 0 ? Tr("Выбрать", "Choose") : Path.GetFileName(path.TrimEnd('\\', '/'));
                if (String.IsNullOrEmpty(name)) name = path;
                var info = Row(0, 1, 0);
                var kind = Label(title, 12, "Text", true);
                kind.Margin = new Thickness(0, 0, 11, 0);
                Put(info, kind, 0);
                var chosen = Label(name, 12, "Muted", false);
                chosen.TextTrimming = TextTrimming.CharacterEllipsis;
                Put(info, chosen, 1);
                var arrow = Label("\uE76C", 10, "Muted", false);
                arrow.FontFamily = WindowsUi.IconFont;
                arrow.VerticalAlignment = VerticalAlignment.Center;
                arrow.Margin = new Thickness(6, 0, 0, 0);
                Put(info, arrow, 2);
                sources[i].Content = info;
                string wallpaper = LocalizedText.Wallpaper(i != 0).In(english);
                sources[i].ToolTip = path.Length == 0 ? wallpaper : path;
                AutomationProperties.SetName(sources[i], wallpaper + " · " + name);
            }
        }

        private void MarkDirty()
        {
            unsaved = true;
            UpdateSaveButton();
            ShowStatus(default(LocalizedText), false);
        }

        private void UpdateSaveButton()
        {
            saveButton.IsEnabled = unsaved || reportedError;
            SetSelected(saveButton, unsaved);
        }

        public void Report(LocalizedText message, bool error)
        {
            reported = message;
            reportedError = error;
            UpdateSaveButton();
            if (!unsaved) ShowStatus(error ? message : default(LocalizedText), error);
        }

        private void ShowStatus(LocalizedText text, bool error)
        {
            displayedStatus = text;
            displayedError = error;
            string message = text.In(english);
            status.Text = message;
            status.Visibility = String.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            status.SetResourceReference(TextBlock.ForegroundProperty, error ? "Error" : "Muted");
            if (editing && IsVisible) ResizeSurface(true);
        }

        private void Save()
        {
            if (!unsaved && !reportedError) return;
            var starts = new int[2];
            for (int i = 0; i < starts.Length; i++)
            {
                if (Schedule.Parse(times[i].Text, out starts[i])) continue;
                ShowStatus(new LocalizedText("Укажите время в формате ЧЧ:ММ.", "Enter the time in HH:MM format."), true);
                times[i].Focus();
                return;
            }
            if (!Schedule.Valid(starts))
            {
                ShowStatus(new LocalizedText("Время утра и вечера должно различаться.", "Morning and evening times must differ."), true);
                return;
            }

            var settings = new Settings { Starts = starts, Folders = folderMode, LightFile = paths[0],
                DarkFile = paths[1], LightFolder = paths[2], DarkFolder = paths[3], English = english };
            try
            {
                save(settings);
                for (int i = 0; i < starts.Length; i++) times[i].Text = Schedule.Format(starts[i]);
                unsaved = false;
                UpdateSaveButton();
                ShowStatus(reportedError ? reported : default(LocalizedText), reportedError);
                View(false);
            }
            catch (Exception error) { ShowStatus(default(LocalizedText).WithError(error), true); }
        }

        public void Refresh(Schedule current)
        {
            bool nextChanged = schedule == null || schedule.Next != current.Next;
            schedule = current;
            dial.Update(current, IsVisible);
            if (darkTheme != current.Dark)
            {
                darkTheme = current.Dark;
                ApplyTheme();
            }
            if (nextChanged) UpdateScheduleHint();
        }

        private void UpdateScheduleHint()
        {
            if (schedule == null) return;
            string next = (schedule.Dark ? LocalizedText.Morning : LocalizedText.Evening).In(english);
            string hint = LocalizedText.Wallpaper(schedule.Dark).In(english) + " · "
                + next + Tr(" в ", " at ") + schedule.Next.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            SetHint(glance, hint);
        }

        private void ApplyLanguage()
        {
            Language = System.Windows.Markup.XmlLanguage.GetLanguage(english ? "en" : "ru");
            Title = LocalizedText.AppName.In(english);
            settingsTitle.Text = LocalizedText.Settings.In(english);
            SetHint(settingsButton, settingsTitle.Text);
            SetHint(backButton, LocalizedText.OpenDial.In(english));
            SetHint(saveButton, Tr("Сохранить и применить", "Save and apply"));
            wallpaperTitle.Text = Tr("Обои", "Wallpaper");
            SetHint(files, Tr("Два файла", "Two files"));
            SetHint(folders, Tr("Две папки", "Two folders"));
            startupToggle.Content = LocalizedText.Startup.In(english);
            SetHint(startupToggle, Tr("Запускать при входе в Windows", "Start when signing in to Windows"));
            languageLabel.Text = Tr("Язык", "Language");
            SetSelected(russianButton, !english);
            SetSelected(englishButton, english);
            string selected = Tr("Выбрано", "Selected");
            AutomationProperties.SetItemStatus(russianButton, english ? "" : selected);
            AutomationProperties.SetItemStatus(englishButton, english ? selected : "");
            for (int i = 0; i < times.Length; i++)
            {
                string period = (i == 0 ? LocalizedText.Morning : LocalizedText.Evening).In(english);
                periodLabels[i].Text = period;
                AutomationProperties.SetName(times[i], period);
                times[i].ToolTip = Tr("ЧЧ:ММ · ↑ / ↓ 15 минут · Shift 1 минута", "HH:MM · ↑ / ↓ 15 minutes · Shift 1 minute");
                clearSources[i].Header = Tr("Очистить", "Clear");
            }
            UpdateSources();
            UpdateModeSelection(false);
            UpdateScheduleHint();
            ShowStatus(displayedStatus, displayedError);
        }

        private Button LanguageButton(string label, bool useEnglish)
        {
            var button = new AnimatedButton { Style = (Style)Resources["QuietButton"], Content = label,
                Height = 26, Width = 65, Margin = new Thickness(3, 0, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += delegate
            {
                if (english == useEnglish) return;
                try { setLanguage(useEnglish); }
                catch (Exception error)
                {
                    ShowStatus(new LocalizedText("Не удалось сохранить язык: ", "Could not save the language: ").WithError(error), true);
                    return;
                }
                english = useEnglish;
                ApplyLanguage();
            };
            return button;
        }

        private string Tr(string russian, string translation) { return english ? translation : russian; }

        private static void SetHint(FrameworkElement element, string text)
        {
            element.ToolTip = text;
            AutomationProperties.SetName(element, text);
        }

        private void ApplyTheme()
        {
            bool dark = darkTheme == true;
            string[] keys = { "Card", "Text", "Muted", "Accent", "AccentText", "Error", "Line" };
            string[] colors = dark
                ? new[] { "#282D37", "#EDF0F6", "#A9B4C7", "#2E4461", "#D7E8FF", "#FFB6C1", "#363E4C" }
                : new[] { "#AFBBC9", "#202939", "#344254", "#96AEC8", "#1D2A40", "#681D34", "#95A3B4" };
            for (int i = 0; i < keys.Length; i++) Resources[keys[i]] = Brush(colors[i]);

            var target = Brush(dark ? "#1C212B" : "#C4CDD7");
            // Animate the tint independently of the controls.
            var old = surface.Background as SolidColorBrush;
            surface.Background = target;
            if (old != null && IsVisible && SystemParameters.ClientAreaAnimation)
                target.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(old.Color, target.Color,
                    TimeSpan.FromMilliseconds(240)) { FillBehavior = FillBehavior.Stop });
        }

        private void View(bool settings, bool animate = true)
        {
            if (editing == settings) return;
            editing = settings;
            Cursor = settings ? Cursors.Arrow : null;
            bool motion = animate && IsVisible;
            int version = ++screenVersion;
            double glanceOpacity = glance.Visibility == Visibility.Visible ? glance.Opacity : 0;
            double settingsOpacity = scroll.Visibility == Visibility.Visible ? scroll.Opacity : 0;
            glance.Visibility = Visibility.Visible;
            scroll.Visibility = Visibility.Visible;
            glance.IsHitTestVisible = !settings;
            scroll.IsHitTestVisible = settings;

            var previous = settings ? (FrameworkElement)glance : scroll;
            var next = settings ? (FrameworkElement)scroll : glance;
            double nextOpacity = settings ? settingsOpacity : glanceOpacity;
            double previousOpacity = settings ? glanceOpacity : settingsOpacity;
            previous.BeginAnimation(OpacityProperty, null);
            previous.Opacity = previousOpacity;
            next.BeginAnimation(OpacityProperty, null);
            next.Opacity = nextOpacity;
            ResizeSurface(motion);
            if (motion)
            {
                next.Opacity = 1;
                var reveal = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                reveal.KeyFrames.Add(new DiscreteDoubleKeyFrame(nextOpacity, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                reveal.KeyFrames.Add(new LinearDoubleKeyFrame(nextOpacity, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(85))));
                reveal.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(ScreenDuration)),
                    new SineEase { EasingMode = EasingMode.EaseInOut }));
                next.BeginAnimation(OpacityProperty, reveal);
                var fade = new DoubleAnimation(previousOpacity, 0, TimeSpan.FromMilliseconds(135))
                {
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                };
                previous.Opacity = 0;
                fade.Completed += delegate { if (version == screenVersion) previous.Visibility = Visibility.Hidden; };
                previous.BeginAnimation(OpacityProperty, fade);
            }
            else
            {
                previous.Opacity = 0;
                previous.Visibility = Visibility.Hidden;
                next.Opacity = 1;
            }
        }

        private void ResizeSurface(bool animate)
        {
            double width = Math.Max(1, Width - surface.Margin.Left - surface.Margin.Right);
            double height = Math.Max(1, Height - surface.Margin.Top - surface.Margin.Bottom);
            double target = Math.Min(height, ShortHeight);
            if (editing)
            {
                var padding = scroll.Padding;
                preferences.Measure(new Size(Math.Max(1, width - padding.Left - padding.Right), double.PositiveInfinity));
                double contentHeight = Math.Min(height - 2, preferences.DesiredSize.Height + padding.Top + padding.Bottom);
                scroll.Height = Math.Max(0, contentHeight);
                target = contentHeight + 2;
            }
            var destination = new Rect(0, height - target, width, target);
            if ((Rect)viewport.GetAnimationBaseValue(RectangleGeometry.RectProperty) == destination && animate) return;
            Rect current = viewport.Rect;
            viewport.Rect = destination;
            viewport.BeginAnimation(RectangleGeometry.RectProperty, animate && !current.IsEmpty && current != destination
                ? new RectAnimation(current, destination, TimeSpan.FromMilliseconds(ScreenDuration))
                {
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                } : null);
        }

        public void Open(Drawing.Rectangle target, bool preferences = false)
        {
            anchor = target;
            if (IsVisible)
            {
                View(preferences);
                Activate();
                return;
            }
            View(preferences, false);
            IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
            if (!IsVisible)
            {
                Drawing.Point point = draggedBottom ?? target.Location;
                SetWindowPos(hwnd, IntPtr.Zero, point.X, point.Y, 0, 0, PositionFlags);
            }
            Position();
            Show();
            Activate();
            if (schedule != null) Refresh(schedule);
            if (SystemParameters.ClientAreaAnimation)
            {
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                surface.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(155)) { EasingFunction = ease });
                slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            }
        }

        private void BeginPanelDrag(object sender, MouseButtonEventArgs args)
        {
            DependencyObject element = args.OriginalSource as DependencyObject;
            while (element != null && element != surface)
            {
                if (element is ButtonBase || element is TextBoxBase || element is ScrollBar) return;
                var text = element as FrameworkContentElement;
                element = text != null ? text.Parent : VisualTreeHelper.GetParent(element);
            }
            if (element != surface) return;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            NativeRect rect;
            if (!GetWindowRect(hwnd, out rect)) return;
            dragOrigin = new Drawing.Point(rect.Left, rect.Top);
            GetCursorPos(out dragPointer);
            dragArmed = CaptureMouse();
            args.Handled = dragArmed;
        }

        private void MovePanelDrag(object sender, MouseEventArgs args)
        {
            if (!dragArmed) return;
            if (args.LeftButton != MouseButtonState.Pressed) { EndPanelDrag(); return; }
            Drawing.Point pointer;
            if (!GetCursorPos(out pointer)) return;
            int dx = pointer.X - dragPointer.X, dy = pointer.Y - dragPointer.Y;
            if (!dragging && Math.Abs(dx) < 4 && Math.Abs(dy) < 4) return;
            dragging = true;
            MoveTo(new Drawing.Point(dragOrigin.X + dx, dragOrigin.Y + dy));
            args.Handled = true;
        }

        private void EndPanelDrag()
        {
            if (!dragArmed) return;
            dragArmed = false;
            bool moved = dragging;
            dragging = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            NativeRect rect;
            if (moved && GetWindowRect(new WindowInteropHelper(this).Handle, out rect))
            {
                draggedBottom = new Drawing.Point(rect.Left + (rect.Right - rect.Left) / 2, rect.Bottom);
                Position();
            }
        }

        private void Position()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            Drawing.Point target = draggedBottom ?? new Drawing.Point(anchor.Left + anchor.Width / 2, anchor.Top);
            Drawing.Rectangle bounds = System.Windows.Forms.Screen.FromPoint(target).WorkingArea;
            double scale = WindowsUi.DpiScale(hwnd);
            Width = Math.Min(PanelWidth, (bounds.Width - 10) / scale);
            Height = Math.Min(HostHeight, (bounds.Height - 8) / scale);
            ResizeSurface(false);
            int width = (int)Math.Round(Width * scale);
            int height = (int)Math.Round(Height * scale);
            int left = Math.Max(bounds.Left, Math.Min(target.X - width / 2, bounds.Right - width));
            int top = Math.Max(bounds.Top, Math.Min(target.Y - height, bounds.Bottom - height));
            MoveTo(new Drawing.Point(left, top));
        }

        private void MoveTo(Drawing.Point point)
        {
            SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, point.X, point.Y, 0, 0, PositionFlags);
        }

        private const uint PositionFlags = 0x0015;
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out Drawing.Point point);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr w, IntPtr after, int x, int y, int width, int height, uint flags);

        private Button IconButton(string glyph, Action click)
        {
            var button = new AnimatedButton { Style = (Style)Resources["Icon"], Content = glyph };
            button.Click += delegate { click(); };
            return button;
        }
        private static void SetSelected(Button button, bool selected)
        {
            button.SetResourceReference(Control.ForegroundProperty, selected ? "AccentText" : "Text");
            if (selected) button.SetResourceReference(Control.BackgroundProperty, "Accent");
            else button.Background = Brushes.Transparent;
        }
        private static Border Card(UIElement child)
        {
            var border = new Border { Child = child, CornerRadius = new CornerRadius(7) };
            border.SetResourceReference(Border.BackgroundProperty, "Card");
            return border;
        }
        private static Grid Row(params int[] widths)
        {
            var row = new Grid();
            foreach (int width in widths)
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = width == 0 ? GridLength.Auto : new GridLength(width, GridUnitType.Star) });
            return row;
        }
        private static void Put(Grid grid, UIElement element, int column) { Grid.SetColumn(element, column); grid.Children.Add(element); }
        private static TextBlock Label(string text, int size, string color, bool bold)
        {
            var label = new TextBlock { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
            label.SetResourceReference(TextBlock.ForegroundProperty, color);
            return label;
        }
        private static Color ColorOf(string color) { return (Color)ColorConverter.ConvertFromString(color); }
        private static SolidColorBrush Brush(string color) { return new SolidColorBrush(ColorOf(color)); }
    }
}
