using System.Collections.Generic;
using System.Configuration;

namespace Foldda.Automation.HandlerDevKit
{
    class ConfigSettings
    {
        internal static string Get1(string key)
        {
            string[] values = GetN(key);
            return (values == null || values.Length == 0) ? null : values[0];
        }

        internal static string[] GetN(string key)
        {
            return Get(key)?.Split('\t') ?? new string[] { };
        }

        internal static void SaveN(string key, string[] values)
        {
            Save(key, string.Join("\t", values));
        }

        internal static void Add(string key, string value)
        {
            var existing = new List<string>(GetN(key));
            existing.Add(value);
            SaveN(key, existing.ToArray());
        }

        internal static string Get(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        internal static void Save(string key, string value)
        {
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
        }

        internal static void Remove(string keyToRemove)
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var settings = configFile.AppSettings.Settings;
            if (settings[keyToRemove] != null)
            {
                settings.Remove(keyToRemove);
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }


            //// Access the AppSettings section
            //AppSettingsSection appSettings = config.AppSettings;

            //// Check if the key exists
            //if (appSettings.Settings[keyToRemove] != null)
            //{
            //    // Remove the key
            //    appSettings.Settings.Remove(keyToRemove);

            //    // Save the configuration file
            //    config.Save(ConfigurationSaveMode.Modified);

            //    // Refresh the AppSettings section to reflect changes
            //    ConfigurationManager.RefreshSection("appSettings");

            //    //Console.WriteLine($"Key '{keyToRemove}' has been removed.");
            //}
        }
    }
}
