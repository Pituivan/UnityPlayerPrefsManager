using System;
using System.Collections;
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
        [SerializeField] private VisualTreeAsset keyCell, valueCell;
        [SerializeField] private VisualTreeAsset noPlayerPrefsMsg;

        // ----- Private Fields

        private static readonly IReadOnlyDictionary<Type, string> typeMapping = new Dictionary<Type, string>
        {
            { typeof(int), "Int" },
            { typeof(float), "Float "},
            { typeof(string), "String" }
        };

        private IList<PlayerPref> playerPrefs;

        // ----- Unity Callbacks

        [MenuItem("Tools/Pituivan/Player Prefs Manager")]
        static void ShowWindow()
        {
            GetWindow<PlayerPrefsManagerWindow>("Player Prefs Manager");
        }

        void CreateGUI()
        {
            rootVisualElement.Add(ui.Instantiate());
            var table = rootVisualElement.Q<MultiColumnListView>();

            playerPrefs = PlayerPrefsReader.ListPlayerPrefs();

            table.itemsSource = (IList)playerPrefs;
            table.makeNoneElement = noPlayerPrefsMsg.Instantiate;
            table.overridingAddButtonBehavior = (table, _) => AddBtnBehaviour(table);
            table.onRemove = RemoveLastPlayerPref;

            Column typeColumn = table.columns["type"];
            typeColumn.bindCell = BindTypeCell;

            Column keyColumn = table.columns["key"];
            keyColumn.makeCell = keyCell.Instantiate;
            keyColumn.bindCell = BindKeyCell;
            keyColumn.unbindCell = (cell, _) => UnbindCell(cell);
            
            Column valueColumn = table.columns["value"];
            valueColumn.makeCell = valueCell.Instantiate;
            valueColumn.bindCell = BindValueCell;
            valueColumn.unbindCell = (cell, _) => UnbindCell(cell);
        }

        // ----- Private Methods

        private void AddBtnBehaviour(BaseListView table)
        {
            var dropdownMenu = new GenericMenu();

            void AddOptionForType<T>()
            {
                string name = typeMapping[typeof(T)];
                dropdownMenu.AddItem(
                    content: new GUIContent(name),
                    on: false,
                    func: () => CreatePlayerPref<T>(table)
                    );
            }

            AddOptionForType<int>();
            AddOptionForType<float>();
            AddOptionForType<string>();

            var dropdownPos = new Rect(Event.current.mousePosition, Vector2.zero);
            dropdownMenu.DropDown(dropdownPos);
        }

        private void CreatePlayerPref<T>(BaseListView table)
        {
            string key = FilterKey(
                input: null,
                playerPrefType: typeof(T),
                alreadyRegistered: false
                );
            var playerPref = new PlayerPref(key, default(T));

            playerPrefs.Add(playerPref);

            table.RefreshItems();
            table.ScrollToItem(playerPrefs.Count - 1);
        }

        private string FilterKey(string input, Type playerPrefType, bool alreadyRegistered)
        {
            if (string.IsNullOrEmpty(input)) input = DefaultKey(playerPrefType);

            var keys = (from item in playerPrefs
                        select item.Key).ToList();
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

        private string DefaultKey(Type playerPrefType) => $"My{typeMapping[playerPrefType]}";

        private void RemoveLastPlayerPref(BaseListView table)
        {
            var lastPlayerPref = playerPrefs[^1];

            playerPrefs.Remove(lastPlayerPref);
            lastPlayerPref.Delete();

            table.RefreshItems();
        }

        private void BindTypeCell(VisualElement cell, int index)
        {
            var typeLabel = cell.Q<Label>();
            PlayerPref playerPref = playerPrefs[index];

            typeLabel.text = typeMapping[playerPref.Type];
        }

        private void BindKeyCell(VisualElement cell, int index)
        {
            var keyField = cell.Q<TextField>();
            PlayerPref playerPref = playerPrefs[index];

            EventCallback<FocusOutEvent> saveKeyCallback = _ =>
            {
                keyField.value = FilterKey(keyField.value, playerPref.Type, alreadyRegistered: true);
                playerPref.Key = keyField.value;
            };
            BindCallback(keyField, saveKeyCallback);
        }

        private void BindCallback<T>(VisualElement target, EventCallback<T> callback) where T : EventBase<T>, new()
        {
            target.RegisterCallback(callback);

            var unbindingHandle = new UnbindingHandle(unbind: () => target.UnregisterCallback(callback));
            target.userData = unbindingHandle;
        }

        private void UnbindCell(VisualElement cell)
        {
            foreach (VisualElement element in cell.Query().Build())
            {
                if (element.userData is UnbindingHandle unbindingHandle)
                {
                    unbindingHandle.Unbind();
                }
            }
        }

        private void BindValueCell(VisualElement cell, int index)
        {
            PlayerPref playerPref = playerPrefs[index];

            Action<VisualElement, PlayerPref> bindValueField = playerPref.Type switch
            {
                Type type when type == typeof(int) => BindValueField<int>,
                var t when t == typeof(float) => BindValueField<float>,
                var t when t == typeof(string) => BindValueField<string>,
                _ => throw new ArgumentNullException($"Player Pref type is null; it is not initialized!")
            };
            bindValueField.Invoke(cell, playerPref);
        }

        private void BindValueField<T>(VisualElement cell, PlayerPref playerPref)
        {
            var targetValueField = cell.Q<BaseField<T>>();
            var valueFields = cell.Query("value-field").Build();

            foreach (var field in valueFields)
            {
                field.style.display = DisplayStyle.None;
            }
            targetValueField.style.display = DisplayStyle.Flex;

            targetValueField.value = (T)playerPref.Value;

            EventCallback<ChangeEvent<T>> editValueCallback = evt => playerPref.Value = evt.newValue;
            BindCallback(targetValueField, editValueCallback);
        }
    }
}