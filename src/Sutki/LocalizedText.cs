using System.IO;

namespace Sutki
{
    internal struct LocalizedText
    {
        private readonly string russian;
        private readonly string english;

        public LocalizedText(string russian, string english)
        {
            this.russian = russian;
            this.english = english;
        }

        public string In(bool useEnglish)
        {
            return (useEnglish ? english : russian) ?? "";
        }

        public LocalizedText WithError(System.Exception error)
        {
            var localized = error as LocalizedException;
            return new LocalizedText(In(false) + (localized == null ? error.Message : localized.Text.In(false)),
                In(true) + (localized == null ? error.Message : localized.Text.In(true)));
        }

        public static readonly LocalizedText AppName = new LocalizedText("Сутки", "Heliolune");
        public static readonly LocalizedText Settings = new LocalizedText("Настройки", "Settings");
        public static readonly LocalizedText OpenDial = new LocalizedText("Открыть шкалу", "Open dial");
        public static readonly LocalizedText Morning = new LocalizedText("Утро", "Morning");
        public static readonly LocalizedText Evening = new LocalizedText("Вечер", "Evening");
        public static readonly LocalizedText Startup = new LocalizedText("Автозапуск", "Start with Windows");

        public static LocalizedText Wallpaper(bool dark)
        {
            return dark ? new LocalizedText("Тёмные обои", "Dark wallpaper")
                : new LocalizedText("Светлые обои", "Light wallpaper");
        }
    }

    internal sealed class LocalizedException : IOException
    {
        public readonly LocalizedText Text;

        public LocalizedException(string russian, string english) : base(russian)
        {
            Text = new LocalizedText(russian, english);
        }
    }
}
