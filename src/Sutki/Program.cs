using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Sutki
{
    internal sealed class Program
    {
        private readonly Store store = new Store(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sutki"));
        private readonly Wallpaper wallpaper = new Wallpaper(new WindowsDesktop());
        private Settings settings;
        private Panel panel;
        private Tray tray;
        private TrayMenu menu;
        private DateTime? appliedPeriod;
        private string appliedSource;
        private bool appliedFolderMode, busy, pending;
        private DateTime retryAt;
        private bool? shownDark;
        private LocalizedText state;
        private bool stateError;

        [STAThread]
        private static void Main(string[] args)
        {
            bool startup = Array.IndexOf(args, "--startup") >= 0;
            using (var activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Sutki.Wallpaper.Activate"))
            using (var mutex = new Mutex(false, @"Local\Sutki.Wallpaper.Instance"))
            {
                bool acquired;
                try { acquired = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired)
                {
                    if (!startup) activate.Set();
                    return;
                }
                var program = new Program();
                try { program.Run(activate, startup); }
                catch (Exception ex)
                {
                    bool english = program.settings != null && program.settings.English;
                    MessageBox.Show(default(LocalizedText).WithError(ex).In(english), LocalizedText.AppName.In(english),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private void Run(EventWaitHandle activate, bool startup)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            try { settings = store.Load(); }
            catch (Exception error)
            {
                settings = new Settings();
                SetState(new LocalizedText("Не удалось загрузить настройки: ", "Could not load settings: ").WithError(error), true);
            }
            using (tray = new Tray(settings.English))
            {
                tray.Selected += (anchor, context) =>
                {
                    if (context)
                    {
                        if (panel != null) panel.Hide();
                        if (menu != null) menu.Close();
                        bool dark = Schedule.At(settings.Starts, DateTime.Now).Dark;
                        var popup = new TrayMenu(dark, command =>
                        {
                            switch (command)
                            {
                                case TrayCommand.Glance: Open(anchor); break;
                                case TrayCommand.Settings: Open(anchor, true); break;
                                case TrayCommand.Exit: app.Shutdown(); break;
                            }
                        }, settings.English);
                        menu = popup;
                        popup.Closed += delegate { if (menu == popup) menu = null; };
                        popup.Open(tray.MenuAnchor);
                    }
                    else
                    {
                        if (menu != null) menu.Close();
                        Open(anchor);
                    }
                };
                tray.ClockChanged += delegate { Update(false); };
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                timer.Tick += delegate { tray.EnsureRegistered(); Update(false); };
                var registration = ThreadPool.RegisterWaitForSingleObject(activate,
                    (ignored, timeout) => app.Dispatcher.BeginInvoke(new Action(() => Open(tray.Anchor()))),
                    null, Timeout.Infinite, false);
                try
                {
                    app.Startup += delegate
                    {
                        timer.Start();
                        Update(false);
                        if (!startup) Open(tray.Anchor());
                    };
                    app.Run();
                }
                finally
                {
                    timer.Stop();
                    registration.Unregister(null);
                }
            }
        }

        private void Open(Rectangle anchor, bool preferences = false)
        {
            if (panel == null)
            {
                bool startup = false;
                try { startup = Store.StartupEnabled(); }
                catch (Exception error) { SetState(new LocalizedText("Автозапуск: ", "Startup: ").WithError(error), true); }
                panel = new Panel(settings, startup, Save, Store.SetStartup, SetLanguage);
            }
            panel.Refresh(Schedule.At(settings.Starts, DateTime.Now));
            panel.Report(state, stateError);
            panel.Open(anchor, preferences);
            Update(false);
        }

        private void Save(Settings value)
        {
            store.Save(value);
            settings = value;
            shownDark = null;
            Update(true);
        }

        private void SetLanguage(bool english)
        {
            Settings value = settings.WithLanguage(english);
            store.Save(value);
            settings = value;
            RefreshTrayText(Schedule.At(settings.Starts, DateTime.Now).Dark);
        }

        private void RefreshTrayText(bool dark)
        {
            bool english = settings.English;
            tray.Tooltip(LocalizedText.AppName.In(english) + " · " + LocalizedText.Wallpaper(dark).In(english));
        }

        private async void Update(bool force)
        {
            DateTime now = DateTime.Now;
            Settings snapshot = settings;
            Schedule schedule = Schedule.At(snapshot.Starts, now);
            if (shownDark != schedule.Dark)
            {
                retryAt = DateTime.MinValue;
                shownDark = schedule.Dark;
                RefreshTrayText(schedule.Dark);
            }
            if (panel != null && panel.IsVisible) panel.Refresh(schedule);

            if (busy) { pending |= force; return; }
            string source = snapshot.Source(schedule.Dark);
            bool alreadyApplied = appliedPeriod == schedule.Period && appliedSource == source && appliedFolderMode == snapshot.Folders;
            if (!force && (alreadyApplied || now < retryAt)) return;
            if (String.IsNullOrWhiteSpace(source))
            {
                Remember(snapshot, schedule, source);
                SetState(schedule.Dark
                    ? new LocalizedText("Выберите тёмные обои.", "Choose a dark wallpaper.")
                    : new LocalizedText("Выберите светлые обои.", "Choose a light wallpaper."), false);
                return;
            }

            busy = true;
            SetState(new LocalizedText("Применяем обои…", "Applying wallpaper…"), false);
            try
            {
                bool keepCurrent = !appliedPeriod.HasValue || alreadyApplied;
                string path = await Task.Run(() => wallpaper.Apply(snapshot, schedule.Dark, keepCurrent));
                Remember(snapshot, schedule, source);
                SetState(new LocalizedText("Обои · " + Path.GetFileName(path), "Wallpaper · " + Path.GetFileName(path)), false);
            }
            catch (Exception error)
            {
                retryAt = DateTime.Now.AddSeconds(30);
                SetState(new LocalizedText("Не удалось сменить обои: ", "Could not change wallpaper: ").WithError(error), true);
            }
            finally
            {
                busy = false;
                if (pending) { pending = false; Update(true); }
            }
        }

        private void Remember(Settings snapshot, Schedule schedule, string source)
        {
            appliedPeriod = schedule.Period;
            appliedSource = source;
            appliedFolderMode = snapshot.Folders;
            retryAt = DateTime.MinValue;
        }

        private void SetState(LocalizedText message, bool error)
        {
            state = message;
            stateError = error;
            if (panel != null) panel.Report(message, error);
        }

    }
}
