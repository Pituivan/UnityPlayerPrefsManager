using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.PlayerPrefs;

namespace Pituivan.EditorTools.PlayerPrefsManager
{
    [Serializable]
    internal class PlayerPref
    {
        // ----- Serialized Fields

        [SerializeField] private string typeName;
        [SerializeField] private string key;

        // ----- Private Fields

        private static readonly IReadOnlyDictionary<Type, (Func<string, object> Getter, Action<string, object> Setter)> getSetValueMapping =
            new Dictionary<Type, (Func<string, object>, Action<string, object>)>()
            {
                { typeof(int), (key => GetInt(key), (key, value) => SetInt(key, (int)value)) },
                { typeof(float), (key => GetFloat(key), (key, val) => SetFloat(key, (float)val)) },
                { typeof(string), (key => GetString(key), (key, val) => SetString(key, (string)val)) }
            };

        // ----- Properties

        public Type Type { get; private set; }

        public string Key
        {
            get => key;
            set
            {
                if (value == key) return;

                DeleteKey(key);
                key = value;
            }
        }

        public object Value
        {
            get => getSetValueMapping[Type].Getter.Invoke(key);
            set
            {
                if (value.GetType() != Type) throw new ArgumentException("Value type and Player Pref type don't match!");
                getSetValueMapping[Type].Setter.Invoke(key, value);
            }
        }

        // ----- Constructors

        public PlayerPref(string key, object value) => Init(key, value);

        // ----- Public Methods

        public void InitFromSerializedData()
        {
            var type = Type.GetType(typeName);
            object value = getSetValueMapping[type].Getter.Invoke(key);

            Init(key, value);
        }

        // ----- Private Methods

        private void Init(string key, object value)
        {
            value ??= "value";
            Type = value.GetType();
            Value = value;

            if (!getSetValueMapping.ContainsKey(Type)) throw new ArgumentException("Value type is not a valid type for a Player Pref!", nameof(value));

            typeName = Type.FullName;
            this.key = key;
        }
    }
}