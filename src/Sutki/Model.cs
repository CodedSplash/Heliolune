using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace Sutki
{
    public sealed class Settings
    {
        public int[] Starts = { 420, 1080 };
        public bool Folders;
        public bool English;
        public string LightFile = "", DarkFile = "", LightFolder = "", DarkFolder = "";

        internal Settings WithLanguage(bool english)
        {
            var copy = (Settings)MemberwiseClone();
            copy.English = english;
            return copy;
        }

        public string Source(bool dark)
        {
            return Folders ? (dark ? DarkFolder : LightFolder) : (dark ? DarkFile : LightFile);
        }
    }

    internal sealed class Schedule
    {
        public bool Dark;
        public DateTime Period;
        public DateTime Next;
        public double Progress;
        public double EveningMark;
        public double NightProgress;

        public static bool Valid(int[] starts)
        {
            return starts != null && starts.Length == 2 && starts[0] >= 0 && starts[0] < 1440
                && starts[1] >= 0 && starts[1] < 1440 && starts[0] != starts[1];
        }

        public static Schedule At(int[] starts, DateTime now)
        {
            DateTime morning = LastStart(starts[0], now);
            DateTime evening = LastStart(starts[1], now);
            bool dark = evening > morning;
            DateTime next = (dark ? morning : evening).AddDays(1);
            return new Schedule
            {
                Dark = dark,
                Period = dark ? evening : morning,
                Next = next,
                Progress = (now - morning).TotalDays,
                EveningMark = ((starts[1] - starts[0] + 1440) % 1440) / 1440.0,
                NightProgress = dark ? (now - evening).TotalMinutes / (next - evening).TotalMinutes : 0
            };
        }

        private static DateTime LastStart(int minutes, DateTime now)
        {
            DateTime today = now.Date.AddMinutes(minutes);
            return today > now ? today.AddDays(-1) : today;
        }

        public static string Format(int minutes)
        {
            return TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        public static bool Parse(string text, out int minutes)
        {
            DateTime result;
            bool valid = DateTime.TryParseExact(text.Trim(), new[] { "HH:mm", "H:mm" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
            minutes = result.Hour * 60 + result.Minute;
            return valid;
        }
    }

    internal sealed class Store
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "Sutki.Wallpaper";
        private readonly string path;
        private readonly XmlSerializer serializer = new XmlSerializer(typeof(Settings));

        public Store(string folder) { path = Path.Combine(folder, "settings.xml"); }

        public Settings Load()
        {
            if (!File.Exists(path)) return new Settings();
            using (FileStream stream = File.OpenRead(path))
            {
                Settings settings = (Settings)serializer.Deserialize(stream);
                if (settings.Starts != null && settings.Starts.Length == 4)
                    settings.Starts = new[] { settings.Starts[0], settings.Starts[2] };
                if (!Schedule.Valid(settings.Starts)) throw new LocalizedException("Проверьте расписание.", "Check the schedule.");
                return settings;
            }
        }

        public void Save(Settings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            try
            {
                using (FileStream stream = File.Create(temp)) serializer.Serialize(stream, settings);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static bool StartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath))
                return key != null && key.GetValue(RunName) != null;
        }

        public static void SetStartup(bool enabled)
        {
            string command = "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --startup";
            if (enabled && command.Length > 260) throw new LocalizedException("Переместите приложение в папку с более коротким путём.",
                "Move the application to a folder with a shorter path.");
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath))
            {
                if (enabled) key.SetValue(RunName, command);
                else key.DeleteValue(RunName, false);
            }
        }
    }

    internal interface IDesktop
    {
        string Current();
        void Set(string path);
    }

    internal sealed class Wallpaper
    {
        private readonly IDesktop desktop;
        private readonly Random random = new Random();
        private string previousLight, previousDark;

        public Wallpaper(IDesktop desktop) { this.desktop = desktop; }

        public static bool Supported(string path)
        {
            string ext = Path.GetExtension(path);
            return ext != null && new[] { ".jpg", ".jpeg", ".png", ".bmp" }
                .Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        public string Apply(Settings settings, bool dark, bool keepCurrent)
        {
            string source = settings.Source(dark);
            if (String.IsNullOrWhiteSpace(source)) return null;
            string current = desktop.Current();
            if (!settings.Folders)
            {
                if (!File.Exists(source)) throw new LocalizedException("Файл обоев недоступен.", "The wallpaper file is unavailable.");
                if (!Same(current, source)) desktop.Set(source);
                return source;
            }

            List<string> candidates = Directory.EnumerateFiles(source).Where(Supported).ToList();
            if (candidates.Count == 0) throw new LocalizedException("В папке нет JPG, PNG или BMP.", "The folder contains no JPG, PNG, or BMP images.");
            string chosen = candidates.FirstOrDefault(file => keepCurrent && Same(file, current));
            if (chosen == null)
            {
                string previous = dark ? previousDark : previousLight;
                List<string> preferred = candidates.Where(p => !Same(p, previous) && !Same(p, current)).ToList();
                List<string> remaining = candidates.Except(preferred).ToList();
                chosen = TryCandidates(preferred, current) ?? TryCandidates(remaining, current);
                if (chosen == null) throw new LocalizedException("Windows не удалось установить изображения из папки.",
                    "Windows could not apply any image from the folder.");
            }
            if (dark) previousDark = chosen;
            else previousLight = chosen;
            return chosen;
        }

        private string TryCandidates(List<string> files, string current)
        {
            while (files.Count > 0)
            {
                int index = random.Next(files.Count);
                string path = files[index];
                files.RemoveAt(index);
                try
                {
                    if (!Same(path, current)) desktop.Set(path);
                    return path;
                }
                catch (System.Runtime.InteropServices.COMException) { }
            }
            return null;
        }

        private static bool Same(string a, string b) { return String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
    }
}
