using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Pituivan.EditorTools.PlayerPrefsManager
{
    internal static class PlayerPrefsReader
    {
        // ----- Private Fields

        private static readonly string[] defaultPlayerPrefs = new[] 
        {
            "unity.cloud_userid", 
            "unity.player_sessionid",
            "unity.player_session_count",
            "UnityGraphicsQuality"
        };

        private static readonly string registryPath;

        // ----- Constructors

        static PlayerPrefsReader()
        {
            registryPath = $@"Software\Unity\UnityEditor\{Application.companyName}\{Application.productName}";
        }

        // ----- Private Methods

        public static IList<PlayerPref> ListPlayerPrefs()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(registryPath) 
                ?? throw new InvalidOperationException("Couldn't access Player Prefs keys in registry.");

            var playerPrefs = new List<PlayerPref>();
            foreach (string valueName in registryKey.GetValueNames())
            {
                string key = Regex.Replace(valueName, @"_h\d+$", string.Empty); // Removes Unity's suffix from value name

                if (defaultPlayerPrefs.Contains(key)) continue;

                object internalValue = registryKey.GetValue(valueName);
                object value = internalValue.GetType() switch
                {
                    Type type when type == typeof(int) => internalValue,
                    var t when t == typeof(long) => BitConverter.ToSingle(BitConverter.GetBytes((long)internalValue)),
                    var t when t == typeof(byte[]) => Encoding.UTF8.GetString((byte[])internalValue),
                    _ => throw new InvalidOperationException("Unexpected Player Pref type.")
                };

                playerPrefs.Add(new PlayerPref(key, value));
            }

            return playerPrefs;
        }
    }
}