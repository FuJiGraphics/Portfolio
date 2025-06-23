#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace SkyDragonHunter.Editors
{
    public class TypeDropdownItem : AdvancedDropdownItem
    {
        public Type type;
        public TypeDropdownItem(string name, Type type) : base(name) => this.type = type;
    }

    public class ScriptableObjectTypeDropdown : AdvancedDropdown
    {
        public Action<Type> onTypeSelected;
        private List<Type> soTypes;

        public ScriptableObjectTypeDropdown(AdvancedDropdownState state) : base(state)
        {
            soTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm => asm.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ScriptableObject)))
                .OrderBy(t => t.FullName)
                .ToList();
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("ScriptableObject Types");
            soTypes.ForEach(t => root.AddChild(new TypeDropdownItem(t.FullName, t)));
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeDropdownItem typedItem)
                onTypeSelected?.Invoke(typedItem.type);
        }
    }

    public class GameDataCSVImporter : EditorWindow
    {
        private List<string[]> csvData = new();
        private AdvancedDropdownState dropdownState;
        private Type selectedType;
        private string saveFolderPath = "Assets/ScriptableObjects";
        private string fileNameFormat = "{type}_{column}";
        private int selectedColumnIndex;

        [MenuItem("SkyDragonHunter/CSV Importer")]
        public static void ShowWindow() => GetWindow<GameDataCSVImporter>("CSV Importer");

        private void OnEnable() => dropdownState ??= new AdvancedDropdownState();

        private void OnGUI()
        {
            DrawSeparator();
            DrawSaveSettings();
            DrawSeparator();
            DrawTypeDropdown();
            DrawSeparator();
            DrawCSVLoadButton();
            DrawCSVPreview();
            DrawGenerateButton();
        }

        private void DrawSeparator(float thickness = 1f, float padding = 6f)
        {
            GUILayout.Space(padding);
            var rect = GUILayoutUtility.GetRect(1, thickness, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 1f));
            GUILayout.Space(padding);
        }

        private void DrawSaveSettings()
        {
            GUILayout.Label("저장 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("저장 경로", GUILayout.Width(80));
            saveFolderPath = EditorGUILayout.TextField(saveFolderPath);
            if (GUILayout.Button("선택", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("폴더 선택", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                    saveFolderPath = "Assets" + path[Application.dataPath.Length..];
                else
                    EditorUtility.DisplayDialog("경고", "Assets 폴더 내부 경로만 선택할 수 있습니다.", "확인");
            }
            EditorGUILayout.EndHorizontal();

            fileNameFormat = EditorGUILayout.TextField("파일 이름 형식", fileNameFormat);
            if (csvData.Count > 0 && csvData[0].Length > 0)
                selectedColumnIndex = EditorGUILayout.Popup("파일 이름에 사용할 컬럼", selectedColumnIndex, csvData[0]);
        }

        private void DrawTypeDropdown()
        {
            GUILayout.Label("ScriptableObject 타입 선택", EditorStyles.boldLabel);
            if (GUILayout.Button(selectedType != null ? selectedType.FullName : "타입 선택"))
            {
                var dropdown = new ScriptableObjectTypeDropdown(dropdownState)
                {
                    onTypeSelected = type => { selectedType = type; Repaint(); }
                };
                dropdown.Show(new Rect(Event.current.mousePosition, Vector2.zero));
            }

            if (selectedType == null) return;

            GUILayout.Space(10);
            EditorGUILayout.HelpBox($"선택된 타입: {selectedType.FullName}", MessageType.Info);
            GUILayout.Space(10);
            GUILayout.Label("필드 목록:", EditorStyles.boldLabel);

            var fields = selectedType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("자료형", GUILayout.Width(150));
            GUILayout.Label("필드명");
            EditorGUILayout.EndHorizontal();
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), Color.gray);

            foreach (var field in fields)
            {
                string typeName = field.FieldType.Name;
                if (field.FieldType == typeof(int)) typeName = "int";
                else if (field.FieldType == typeof(float)) typeName = "float";
                else if (field.FieldType == typeof(double)) typeName = "double";
                else if (field.FieldType == typeof(bool)) typeName = "bool";
                else if (field.FieldType == typeof(string)) typeName = "string";

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(typeName, GUILayout.Width(150));
                GUILayout.Label(field.Name);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCSVLoadButton()
        {
            if (GUILayout.Button("CSV 불러오기"))
            {
                string path = EditorUtility.OpenFilePanel("CSV 파일 선택", "", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    csvData = File.ReadAllLines(path)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Split(',')).ToList();
                }
            }
        }

        private void DrawCSVPreview()
        {
            if (csvData.Count == 0)
            {
                GUILayout.Label("No CSV loaded.", EditorStyles.helpBox);
                return;
            }

            GUILayout.Space(10);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("자료형 미리보기", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), Color.gray);

            if (csvData.Count > 1)
            {
                var sampleRow = csvData[1];
                var typeNames = sampleRow.Select(cell =>
                {
                    if (int.TryParse(cell, out _)) return "int";
                    if (double.TryParse(cell, out _)) return "double";
                    if (float.TryParse(cell, out _)) return "float";
                    if (bool.TryParse(cell, out _)) return "bool";
                    return "string";
                });

                GUILayout.BeginHorizontal("box");
                foreach (var type in typeNames)
                    GUILayout.Label(type, GUILayout.MinWidth(80));
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("필드명 미리보기", GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), Color.gray);

            GUILayout.BeginHorizontal("box");
            foreach (var cell in csvData[0])
                GUILayout.Label(cell, GUILayout.MinWidth(80));
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateButton()
        {
            if (csvData.Count < 2 || selectedType == null) return;

            GUI.enabled = true;
            if (GUILayout.Button("ScriptableObject 생성"))
                GenerateScriptableObjects();
            GUI.enabled = true;
        }

        private void GenerateScriptableObjects()
        {
            var header = csvData[0];
            var dataRows = csvData.Skip(1);
            int index = 0;

            foreach (var row in dataRows)
            {
                string columnValue = row.Length > selectedColumnIndex ? row[selectedColumnIndex] : "Unnamed";
                string finalName = fileNameFormat
                    .Replace("{type}", selectedType.Name)
                    .Replace("{index}", index.ToString("D3"))
                    .Replace("{column}", columnValue)
                    .Replace("{guid}", Guid.NewGuid().ToString("N"));

                string fullPath = Path.Combine(saveFolderPath, finalName + ".asset");
                Directory.CreateDirectory(saveFolderPath);

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(fullPath);
                var instance = asset != null ? asset : ScriptableObject.CreateInstance(selectedType);

                if (asset == null)
                    AssetDatabase.CreateAsset(instance, fullPath);

                for (int i = 0; i < header.Length && i < row.Length; i++)
                {
                    var field = selectedType.GetField(header[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field == null) continue;

                    try
                    {
                        string cell = row[i];
                        object value = field.FieldType switch
                        {
                            var t when t == typeof(int) && int.TryParse(cell, out var iVal) => iVal,
                            var t when t == typeof(float) && float.TryParse(cell, out var fVal) => fVal,
                            var t when t == typeof(double) && double.TryParse(cell, out var dVal) => dVal,
                            var t when t == typeof(bool) && bool.TryParse(cell, out var bVal) => bVal,
                            var t when t == typeof(string) => cell,
                            _ => null
                        };
                        if (value != null)
                        {
                            field.SetValue(instance, value);
                            EditorUtility.SetDirty(instance);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[{field.Name}] 필드에 값 설정 중 예외 발생: {ex.Message}");
                    }
                }
                index++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("완료", "ScriptableObject 생성 완료!", "확인");
        }
    }
}
#endif
