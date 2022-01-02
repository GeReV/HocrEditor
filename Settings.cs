using System;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace HocrEditor
{
    public static class Settings
    {
        public static string? TesseractPath
        {
            get => GetSetting(nameof(TesseractPath));
            set
            {
                Debug.Assert(value != null, nameof(value) + " != null");

                SetSetting(nameof(TesseractPath), value);
            }
        }

        private static string? GetSetting(string key) => ConfigurationManager.AppSettings[key];

        private static void SetSetting(string key, string value)
        {
            // try
            // {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var settings = configFile.AppSettings.Settings;

                if (settings[key] == null)
                {
                    settings.Add(key, value);
                }
                else
                {
                    settings[key].Value = value;
                }

                configFile.Save(ConfigurationSaveMode.Modified);

                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            // }
            // catch (ConfigurationErrorsException)
            // {
            //
            // }
        }
    }
}
