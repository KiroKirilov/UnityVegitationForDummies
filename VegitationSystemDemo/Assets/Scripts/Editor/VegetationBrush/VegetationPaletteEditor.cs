using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

[CustomEditor(typeof(VegetationPalette))]
public class VegetationPaletteEditor : Editor
{
    static readonly Color DuplicateColor = new Color(1f, 0.3f, 0.3f, 0.25f);

    SerializedProperty _grassRatio;
    SerializedProperty _grassPrefabs;
    SerializedProperty _grassSettings;
    SerializedProperty _flowerPrefabs;
    SerializedProperty _flowerSettings;

    ReorderableList _grassList;
    ReorderableList _flowerList;

    HashSet<int> _grassDupeIndices = new();
    HashSet<int> _flowerDupeIndices = new();
    bool _hasCrossListDupes;

    void OnEnable()
    {
        _grassRatio = serializedObject.FindProperty("grassRatio");
        _grassPrefabs = serializedObject.FindProperty("grassPrefabs");
        _grassSettings = serializedObject.FindProperty("grassSettings");
        _flowerPrefabs = serializedObject.FindProperty("flowerPrefabs");
        _flowerSettings = serializedObject.FindProperty("flowerSettings");

        _grassList = CreatePrefabList(_grassPrefabs, "Grass Prefabs", _grassDupeIndices);
        _flowerList = CreatePrefabList(_flowerPrefabs, "Flower Prefabs", _flowerDupeIndices);
    }

    ReorderableList CreatePrefabList(SerializedProperty property, string header, HashSet<int> dupeIndices)
    {
        var list = new ReorderableList(serializedObject, property, true, true, true, true);

        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);

        list.elementHeightCallback = index =>
        {
            return EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing * 3;
        };

        list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (dupeIndices.Contains(index))
                EditorGUI.DrawRect(rect, DuplicateColor);

            var element = property.GetArrayElementAtIndex(index);
            var prefabProp = element.FindPropertyRelative("prefab");
            var weightProp = element.FindPropertyRelative("weight");

            rect.y += EditorGUIUtility.standardVerticalSpacing;
            float lineHeight = EditorGUIUtility.singleLineHeight;

            var prefabRect = new Rect(rect.x, rect.y, rect.width, lineHeight);
            var weightRect = new Rect(rect.x, rect.y + lineHeight + EditorGUIUtility.standardVerticalSpacing, rect.width, lineHeight);

            EditorGUI.PropertyField(prefabRect, prefabProp, new GUIContent("Prefab"));
            EditorGUI.PropertyField(weightRect, weightProp, new GUIContent("Weight"));
        };

        list.onAddCallback = reorderableList =>
        {
            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            var newElement = property.GetArrayElementAtIndex(index);
            newElement.FindPropertyRelative("prefab").objectReferenceValue = null;
            newElement.FindPropertyRelative("weight").floatValue = 1f;
        };

        return list;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        FindDuplicates();

        EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_grassRatio, new GUIContent("Grass Ratio", "0 = all flowers, 1 = all grass"));

        DrawRatioBar();

        EditorGUILayout.Space(15);

        EditorGUILayout.LabelField("Grass", EditorStyles.boldLabel);
        _grassList.DoLayoutList();
        if (_grassDupeIndices.Count > 0)
            EditorGUILayout.HelpBox("Duplicate prefabs detected in grass list.", MessageType.Warning);
        EditorGUILayout.PropertyField(_grassSettings, new GUIContent("Grass Settings"), true);

        EditorGUILayout.Space(15);

        EditorGUILayout.LabelField("Flowers", EditorStyles.boldLabel);
        _flowerList.DoLayoutList();
        if (_flowerDupeIndices.Count > 0)
            EditorGUILayout.HelpBox("Duplicate prefabs detected in flower list.", MessageType.Warning);
        EditorGUILayout.PropertyField(_flowerSettings, new GUIContent("Flower Settings"), true);

        if (_hasCrossListDupes)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Some prefabs appear in both the grass and flower lists.", MessageType.Warning);
        }

        serializedObject.ApplyModifiedProperties();
    }

    void FindDuplicates()
    {
        _grassDupeIndices.Clear();
        _flowerDupeIndices.Clear();
        _hasCrossListDupes = false;

        FindDupesInList(_grassPrefabs, _grassDupeIndices);
        FindDupesInList(_flowerPrefabs, _flowerDupeIndices);

        var grassObjects = CollectPrefabs(_grassPrefabs);
        var flowerObjects = CollectPrefabs(_flowerPrefabs);
        foreach (var obj in grassObjects)
        {
            if (obj != null && flowerObjects.Contains(obj))
            {
                _hasCrossListDupes = true;
                break;
            }
        }
    }

    void FindDupesInList(SerializedProperty listProp, HashSet<int> dupeIndices)
    {
        var seen = new Dictionary<Object, int>();

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var prefab = listProp.GetArrayElementAtIndex(i)
                .FindPropertyRelative("prefab").objectReferenceValue;

            if (prefab == null) continue;

            if (seen.TryGetValue(prefab, out int firstIndex))
            {
                dupeIndices.Add(firstIndex);
                dupeIndices.Add(i);
            }
            else
            {
                seen[prefab] = i;
            }
        }
    }

    HashSet<Object> CollectPrefabs(SerializedProperty listProp)
    {
        var set = new HashSet<Object>();
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var prefab = listProp.GetArrayElementAtIndex(i)
                .FindPropertyRelative("prefab").objectReferenceValue;
            if (prefab != null)
                set.Add(prefab);
        }
        return set;
    }

    void DrawRatioBar()
    {
        Rect rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
        rect = EditorGUI.IndentedRect(rect);

        float ratio = _grassRatio.floatValue;

        EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));

        var grassRect = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
        var flowerRect = new Rect(rect.x + rect.width * ratio, rect.y, rect.width * (1 - ratio), rect.height);

        EditorGUI.DrawRect(grassRect, new Color(0.3f, 0.6f, 0.3f));
        EditorGUI.DrawRect(flowerRect, new Color(0.7f, 0.4f, 0.5f));

        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;

        if (ratio > 0.15f)
            GUI.Label(grassRect, $"Grass {ratio * 100:F0}%", style);
        if (ratio < 0.85f)
            GUI.Label(flowerRect, $"Flowers {(1 - ratio) * 100:F0}%", style);
    }
}
