using System.Collections.Specialized;
using System.Configuration;

namespace HocrEditor.Core
{
    public static class Settings
    {
        public static string? TesseractPath
        {
            get => GetSetting(nameof(TesseractPath));
            set
            {
                Ensure.IsNotNullOrWhitespace(nameof(value), value);

                SetSetting(nameof(TesseractPath), value!);
            }
        }

        public static bool AutoClean
        {
            get => GetSettingAs(nameof(AutoClean), true);
            set => SetSetting(nameof(AutoClean), value);
        }

        private static string? GetSetting(string key) => GetSettingAs<string>(key);

        private static T? GetSettingAs<T>(string key) => GetValueAs<T>(ConfigurationManager.AppSettings, key);

        private static T GetSettingAs<T>(string key, T defaultValue) => GetValueAs<T>(ConfigurationManager.AppSettings, key) ?? defaultValue;

        private static void SetSetting<T>(string key, T value) where T : notnull
        {
            // try
            // {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;

                if (settings[key] == null)
                {
                    settings.Add(key, value.ToString());
                }
                else
                {
                    settings[key].Value = value.ToString();
                }

                configFile.Save(ConfigurationSaveMode.Modified);

                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            // }
            // catch (ConfigurationErrorsException)
            // {
            //
            // }
        }

        /// <summary>
        /// Gets the value associated with the specified key from the <see cref='NameValueCollection'/>.
        /// </summary>
        /// <typeparam name="T">The type to cast the value to.</typeparam>
        /// <param name="collection">The <see cref="NameValueCollection"/> to retrieve the value from.</param>
        /// <param name="key">The key associated with the value being retrieved.</param>
        /// <returns>The value associated with the specified key.</returns>
        private static T? GetValueAs<T>(NameValueCollection collection, string key)
        {
            Ensure.IsNotNullOrWhitespace(nameof(key), key);

            var stringValue = collection[key];

            return Converter.ConvertValue<T>(stringValue);
        }
    }
}
