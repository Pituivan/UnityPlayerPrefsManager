using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR_WIN
using Microsoft.Win32;
using System.Text;
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
using System.IO;
using System.Xml.Linq;
#endif

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

#if UNITY_EDITOR_WIN
        private static readonly string registryPath;
#elif UNITY_EDITOR_OSX
    private static readonly string plistPath;
#elif UNITY_EDITOR_LINUX
    private static readonly string playerPrefsPath;
#endif

        // ----- Constructors

        static PlayerPrefsReader()
        {
#if UNITY_EDITOR_WIN
            registryPath = $@"Software\Unity\UnityEditor\{Application.companyName}\{Application.productName}";
#elif UNITY_EDITOR_OSX
            plistPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                $"Library/Preferences/unity.{Application.companyName}.{Application.productName}.plist");
#elif UNITY_EDITOR_LINUX
            playerPrefsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                $".config/unity3d/{Application.companyName}/{Application.productName}/prefs");
#endif
        }

        // ----- Public Methods

        public static IList<PlayerPref> ListPlayerPrefs()
        {
#if UNITY_EDITOR_WIN
            return ListPlayerPrefsWin();
#elif UNITY_EDITOR_OSX
            return ListPlayerPrefsMac();
#elif UNITY_EDITOR_LINUX
            return ListPlayerPrefsLinux();
#endif
        }

        // ----- Private Methods

#if UNITY_EDITOR_WIN
        private static IList<PlayerPref> ListPlayerPrefsWin()
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
#endif

#if UNITY_EDITOR_OSX
        private static IList<PlayerPref> ListPlayerPrefsMac()
        {
            XDocument plist = XDocument.Load(plistPath) ?? throw new InvalidOperationException("Couldn't access .plist file.");
            XElement dict = plist.Descendants("dict").First() ?? throw new InvalidOperationException("Invalid .plist file.");

            var playerPrefs = new List<PlayerPref>();

            List<XElement> elements = dict.Elements("key").ToList();
            for (int i = 0; i < elements.Count - 1; i += 2)
            {
                XElement keyElement = elements[i];
                string key = keyElement.Value;

                if (defaultPlayerPrefs.Contains(key)) continue;

                var valueElement = keyElement.NextNode as XElement;
                object value = valueElement.Name.LocalName switch
                {
                    "integer" => int.Parse(valueElement.Value),
                    "real" => float.Parse(valueElement.Value, System.Globalization.CultureInfo.InvariantCulture),
                    "string" => valueElement.Value,
                    _ => throw new InvalidOperationException("Unexpected Player Pref type.")
                };

                playerPrefs.Add(new PlayerPref(key, value));
            }

            return playerPrefs;
        }
#endif

#if UNITY_EDITOR_LINUX
        private static IList<PlayerPref> ListPlayerPrefsLinux()
        {
            byte[] playerPrefsData = File.ReadAllBytes(playerPrefsPath);

            var playerPrefs = new List<PlayerPref>();
            int i = 0;
            while (i < playerPrefsData.Length)
            {
                int keyLength = BitConverter.ToInt32(playerPrefsData, i);
                i += 4;
                string key = Encoding.UTF8.GetString(playerPrefsData, i, keyLength);
                i += keyLength;

                byte type = playerPrefsData[i];
                i++;

                object value;
                switch (type)
                {
                    case 0: // Int
                        value = BitConverter.ToInt32(playerPrefsData, i);
                        i += 4;
                        break;

                    case 1: // Float
                        value = BitConverter.ToSingle(playerPrefsData, i);
                        i += 4;
                        break;

                    case 2: // String
                        int length = BitConverter.ToInt32(playerPrefsData, i);
                        i += 4;
                        value = Encoding.UTF8.GetString(playerPrefsData, i, length);
                        i += length;
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected Player Pref type.");
                }

                playerPrefs.Add(new PlayerPref(key, value));
            }

            return playerPrefs;
        }
#endif
    }
}