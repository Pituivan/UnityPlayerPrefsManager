using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pituivan.EditorTools.PlayerPrefsManager
{
    internal class PlayerPrefsManagerWindow : EditorWindow
    {
        // ----- Nested Members

        private class UnbindingHandle
        {
            // ----- Private Fields

            private readonly Action unbind;

            // ----- Constructors

            public UnbindingHandle(Action unbind)
            {
                this.unbind = unbind;
            }

            // ----- Public Methods

            public void Unbind() => unbind.Invoke();
        }

        // ----- Serialized Fields

        [SerializeField] private VisualTreeAsset ui;
        [SerializeField] private VisualTreeAsset noPlayerPrefsMsg;

        // ----- Private Fields

        private static readonly IReadOnlyDictionary<Type, string> typeNamesMapping = new Dictionary<Type, string>
        {
            { typeof(int), "Int" },
            { typeof(float), "Float" },
            { typeof(string), "String" }
        };

        private readonly List<(PlayerPref, UnbindingHandle)> items = new();

        // ----- Unity Callbacks

        [MenuItem("Tools/Pituivan/Player Prefs Manager")]
        static void ShowWindow()
        {
            GetWindow<PlayerPrefsManagerWindow>("Player Prefs Manager");
        }

        void CreateGUI()
        {
            rootVisualElement.Add(ui.Instantiate());
            var listView = rootVisualElement.Q<ListView>();

            foreach (var playerPref in RegisteredPlayerPrefs.instance.PlayerPrefs)
            {
                items.Add((playerPref, null));
            }

            listView.itemsSource = items;
            listView.bindItem = BindItem;
            listView.unbindItem = (_, i) => UnbindItem(i);
            listView.overridingAddButtonBehavior = (view, _) => OverrideAddBtnBehaviour(view);
            listView.onRemove = DeleteItem;
            listView.makeNoneElement = noPlayerPrefsMsg.Instantiate;
        }

        // ----- Private Methods

        private void BindItem(VisualElement ve, int index)
        {
            PlayerPref playerPref = items[index].Item1;
            var keyField = ve.Q<TextField>("key-field");

            UnbindingHandle keyUnbindingHandle = BindKey(keyField, playerPref);
            UnbindingHandle valueUnbindingHandle = BindValue(ve, playerPref);
            UnbindingHandle unbindingHandle = new(() =>
            {
                keyUnbindingHandle.Unbind();
                valueUnbindingHandle.Unbind();
            });

            items[index] = (playerPref, unbindingHandle);
        }

        private UnbindingHandle BindKey(TextField keyField, PlayerPref playerPref)
        {
            keyField.Q<Label>("type-label").text = typeNamesMapping[playerPref.Type];
            keyField.value = playerPref.Key;

            EventCallback<FocusOutEvent> focusOutCallback = _ =>
            {
                keyField.value = FilterKey(keyField.value, playerPref.Type, alreadyRegistered: true);
                playerPref.Key = keyField.value;

                RegisteredPlayerPrefs.instance.Save();
            };

            keyField.RegisterCallback(focusOutCallback);
            return new UnbindingHandle(() => keyField.UnregisterCallback(focusOutCallback));
        }

        private string FilterKey(string input, Type playerPrefType, bool alreadyRegistered)
        {
            if (string.IsNullOrEmpty(input)) input = DefaultKey(playerPrefType);

            var keys = (from item in items
                       select item.Item1.Key).ToList();
            if (alreadyRegistered)
            {
                keys.Remove(input);
            }

            const string numericSuffixPattern = @"\d+$";
            string suffix = Regex.Match(input, numericSuffixPattern).Value;

            int numericSuffix = suffix == string.Empty ? 0 : int.Parse(suffix);
            string baseName = input[..^suffix.Length];

            string output = input;
            while (keys.Contains(output))
            {
                numericSuffix++;
                output = baseName + numericSuffix;
            }

            return output;
        }

        private string DefaultKey(Type playerPrefType) => $"My{typeNamesMapping[playerPrefType]}";

        private UnbindingHandle BindValue(VisualElement ve, PlayerPref playerPref)
        {
            UnbindingHandle BindValue<T>(BaseField<T> valueField) => this.BindValue<T>(ve, valueField, playerPref);

            return playerPref.Type switch
            {
                Type type when type == typeof(int) => BindValue(ve.Q<IntegerField>()),
                var t when t == typeof(float) => BindValue(ve.Q<FloatField>()),
                var t when t == typeof(string) => BindValue(ve.Q<TextField>("value-field")),
                _ => throw new ArgumentNullException($"Player Pref type is null; it is not initialized!")
            };
        }

        private UnbindingHandle BindValue<T>(VisualElement ve, BaseField<T> valueField, PlayerPref playerPref)
        {
            foreach (var field in ve.Query("value-field").Build())
            {
                field.style.display = DisplayStyle.None;
            }
            valueField.style.display = DisplayStyle.Flex;

            valueField.value = (T)playerPref.Value;

            EventCallback<ChangeEvent<T>> valueChangedCallback = evt =>
            {
                playerPref.Value = evt.newValue;
            };

            valueField.RegisterValueChangedCallback(valueChangedCallback);
            return new UnbindingHandle(() => valueField.UnregisterValueChangedCallback(valueChangedCallback));
        }

        private void UnbindItem(int index)
        {
            if (index < items.Count)
            {
                items[index].Item2?.Unbind();
            }
        }

        private void OverrideAddBtnBehaviour(BaseListView view)
        {
            var dropdownMenu = new GenericMenu();

            void AddOptionForType<T>()
            {
                string name = typeNamesMapping[typeof(T)];
                dropdownMenu.AddItem(
                    content: new GUIContent(name),
                    on: false,
                    func: () => AddPlayerPref<T>(view)
                    );
            }

            AddOptionForType<int>();
            AddOptionForType<float>();
            AddOptionForType<string>();

            dropdownMenu.DropDown(new Rect(Event.current.mousePosition, Vector2.zero));
        }

        private void AddPlayerPref<T>(BaseListView view)
        {
            string key = FilterKey(
                input: null, 
                playerPrefType: typeof(T),
                alreadyRegistered: false
                );
            var playerPref = new PlayerPref(key, default(T));

            RegisteredPlayerPrefs.instance.PlayerPrefs.Add(playerPref);
            items.Add((playerPref, null));

            ApplyChanges(view);
            view.ScrollToItem(items.Count - 1);
        }

        private void DeleteItem(BaseListView view)
        {
            var itemToRemove = items[^1];
            RegisteredPlayerPrefs.instance.PlayerPrefs.Remove(itemToRemove.Item1);
            items.Remove(itemToRemove);
            itemToRemove.Item1.Delete();

            ApplyChanges(view);
        }

        private void ApplyChanges(BaseListView view)
        {
            RegisteredPlayerPrefs.instance.Save();
            view.RefreshItems();
        }
    }
}