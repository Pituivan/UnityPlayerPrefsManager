using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pituivan.EditorTools.PlayerPrefsManager
{
    [FilePath("ProjectSettings/Packages/com.pituivan.playerprefsmanager/Data.asset", FilePathAttribute.Location.ProjectFolder)]
    internal class RegisteredPlayerPrefs : ScriptableSingleton<RegisteredPlayerPrefs>
    {
        // ----- Serialized Fields

        [SerializeField] private List<PlayerPref> playerPrefs = new();

        // ----- Properties

        public IList<PlayerPref> PlayerPrefs => playerPrefs;

        // ----- Unity Callbacks

        void OnEnable()
        {
            foreach (var playerPref in playerPrefs)
            {
                playerPref.InitFromSerializedData();
            }
        }

        // ----- Public Methods

        public void Save() => Save(saveAsText: true);
    }
}