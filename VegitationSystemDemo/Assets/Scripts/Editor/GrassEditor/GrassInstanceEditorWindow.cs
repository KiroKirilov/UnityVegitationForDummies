using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class GrassInstanceEditorWindow : EditorWindow
{
    [SerializeField] GrassInstanceData _data;
    [SerializeField] bool _editModeActive;
    float _editRadius = 30f;
    int _filterMeshType = -1;

    HashSet<int> _selection = new HashSet<int>();
    GrassInstanceSceneHandler _handler = new GrassInstanceSceneHandler();
    GrassRenderer _grassRenderer;

    [MenuItem("Tools/Grass Instance Editor")]
    static void ShowWindow()
    {
        var window = GetWindow<GrassInstanceEditorWindow>("Grass Instance Editor");
        window.minSize = new Vector2(280, 400);
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnUndoRedo()
    {
        RefreshRenderer();
        ValidateSelection();
        SceneView.RepaintAll();
        Repaint();
    }

    GrassRenderer FindGrassRenderer()
    {
        if (_grassRenderer != null) return _grassRenderer;
        _grassRenderer = Object.FindFirstObjectByType<GrassRenderer>();
        return _grassRenderer;
    }

    void RefreshRenderer()
    {
        var renderer = FindGrassRenderer();
        if (renderer != null)
            renderer.RefreshGPUData();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Grass Instance Editor", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        DrawDataSection();
        EditorGUILayout.Space(10);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to edit grass instances.\n\n" +
                "The grass renderer handles all rendering during play mode. " +
                "Changes to the data asset persist after exiting play mode.",
                MessageType.Info);
            return;
        }

        DrawEditModeSection();
        EditorGUILayout.Space(10);

        if (_editModeActive && _data != null)
        {
            DrawSettingsSection();
            EditorGUILayout.Space(10);

            DrawSelectionSection();
            EditorGUILayout.Space(10);
        }

        DrawHelpSection();
    }

    void DrawDataSection()
    {
        var newData = (GrassInstanceData)EditorGUILayout.ObjectField(
            "Grass Data", _data, typeof(GrassInstanceData), false);

        if (newData != _data)
        {
            _data = newData;
            _selection.Clear();
            _grassRenderer = null;
        }

        if (_data != null && _data.instances != null)
        {
            int meshTypeCount = _data.meshTypes != null ? _data.meshTypes.Length : 0;
            EditorGUILayout.LabelField($"Instances: {_data.instances.Length}    Mesh Types: {meshTypeCount}");
        }
    }

    void DrawEditModeSection()
    {
        bool canEdit = _data != null && _data.instances != null && _data.instances.Length > 0;

        if (canEdit && FindGrassRenderer() == null)
        {
            EditorGUILayout.HelpBox(
                "No GrassRenderer found in scene. Ensure one exists with the same data asset.",
                MessageType.Warning);
        }

        if (canEdit)
        {
            var renderer = FindGrassRenderer();
            if (renderer != null && renderer.instanceData != _data)
            {
                EditorGUILayout.HelpBox(
                    "GrassRenderer is using a different data asset than the one selected here.",
                    MessageType.Warning);
            }
        }

        EditorGUI.BeginDisabledGroup(!canEdit);

        Color originalBg = GUI.backgroundColor;
        if (_editModeActive)
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);

        string buttonText = _editModeActive ? "Disable Edit Mode" : "Enable Edit Mode";
        if (GUILayout.Button(buttonText, GUILayout.Height(30)))
        {
            _editModeActive = !_editModeActive;
            if (!_editModeActive)
                _selection.Clear();
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = originalBg;
        EditorGUI.EndDisabledGroup();
    }

    void DrawSettingsSection()
    {
        EditorGUILayout.LabelField("Edit Settings", EditorStyles.boldLabel);
        _editRadius = EditorGUILayout.Slider("Edit Radius", _editRadius, 5f, 100f);

        string[] typeOptions = BuildMeshTypeOptions();
        _filterMeshType = EditorGUILayout.Popup("Filter Type", _filterMeshType + 1, typeOptions) - 1;
    }

    void DrawSelectionSection()
    {
        EditorGUILayout.LabelField(
            _selection.Count > 0 ? $"Selection ({_selection.Count})" : "Selection (none)",
            EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All in Radius"))
        {
            var cam = SceneView.lastActiveSceneView?.camera;
            if (cam != null)
            {
                var nearby = GrassInstancePicker.GetIndicesInRadius(
                    _data, cam.transform.position, _editRadius, _filterMeshType);
                _selection.UnionWith(nearby);
                SceneView.RepaintAll();
            }
        }

        if (GUILayout.Button("Deselect All"))
        {
            _selection.Clear();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(_selection.Count == 0);
        Color originalBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button($"Delete Selected ({_selection.Count})", GUILayout.Height(25)))
        {
            DeleteSelected();
        }
        GUI.backgroundColor = originalBg;
        EditorGUI.EndDisabledGroup();
    }

    void DrawHelpSection()
    {
        EditorGUILayout.HelpBox(
            "Use Scene View to edit grass instances.\n\n" +
            "LMB = Select instance\n" +
            "Ctrl+LMB = Add/remove from selection\n" +
            "W / E / R = Move / Rotate / Scale\n" +
            "Delete = Delete selected\n" +
            "Escape = Deselect all\n" +
            "Alt+LMB = Orbit camera (always works)",
            MessageType.Info);
    }

    string[] BuildMeshTypeOptions()
    {
        var options = new List<string> { "All Types" };
        if (_data?.meshTypes != null)
        {
            for (int i = 0; i < _data.meshTypes.Length; i++)
                options.Add($"[{i}] {_data.meshTypes[i].name}");
        }
        return options.ToArray();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!_editModeActive || _data == null || _data.instances == null) return;
        if (!EditorApplication.isPlaying) return;

        var result = _handler.OnSceneGUI(_data, sceneView, _selection, _editRadius, _filterMeshType);
        ProcessResult(result);
    }

    void ProcessResult(GrassInstanceSceneHandler.HandleResult result)
    {
        if (result.hasPositionDelta)
        {
            Undo.RecordObject(_data, "Move Grass Instances");
            foreach (int idx in _selection)
                _data.instances[idx].position += result.positionDelta;
            EditorUtility.SetDirty(_data);
            RefreshRenderer();
        }

        if (result.hasRotationChange)
        {
            Undo.RecordObject(_data, "Rotate Grass Instances");
            if (_selection.Count == 1)
            {
                int idx = _selection.First();
                _data.instances[idx].rotation = result.newRotation;
            }
            else
            {
                Vector3 center = ComputeSelectionCenter();
                foreach (int idx in _selection)
                {
                    Vector3 offset = _data.instances[idx].position - center;
                    _data.instances[idx].position = center + result.newRotation * offset;
                    _data.instances[idx].rotation = result.newRotation * _data.instances[idx].rotation;
                }
            }
            EditorUtility.SetDirty(_data);
            RefreshRenderer();
        }

        if (result.hasScaleChange)
        {
            Undo.RecordObject(_data, "Scale Grass Instances");
            foreach (int idx in _selection)
                _data.instances[idx].scale *= result.scaleFactor;
            EditorUtility.SetDirty(_data);
            RefreshRenderer();
        }

        if (result.deletePressed)
            DeleteSelected();

        if (result.escapePressed)
        {
            _selection.Clear();
            Repaint();
        }
    }

    void DeleteSelected()
    {
        if (_selection.Count == 0) return;

        Undo.RecordObject(_data, "Delete Grass Instances");

        var toDelete = new HashSet<int>(_selection);
        _data.instances = _data.instances
            .Where((_, i) => !toDelete.Contains(i))
            .ToArray();

        RecalculateBounds();
        EditorUtility.SetDirty(_data);

        _selection.Clear();
        RefreshRenderer();

        Debug.Log($"[GrassInstanceEditor] Deleted {toDelete.Count} instances. {_data.instances.Length} remaining.");
        Repaint();
    }

    void RecalculateBounds()
    {
        if (_data.instances.Length == 0)
        {
            _data.worldBounds = new Bounds(Vector3.zero, Vector3.zero);
            return;
        }

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (var entry in _data.instances)
        {
            min = Vector3.Min(min, entry.position);
            max = Vector3.Max(max, entry.position);
        }

        _data.worldBounds = new Bounds((min + max) * 0.5f, max - min + Vector3.one * 2f);
    }

    void ValidateSelection()
    {
        if (_data == null || _data.instances == null)
        {
            _selection.Clear();
            return;
        }

        _selection.RemoveWhere(i => i < 0 || i >= _data.instances.Length);
    }

    Vector3 ComputeSelectionCenter()
    {
        Vector3 sum = Vector3.zero;
        foreach (int idx in _selection)
            sum += _data.instances[idx].position;
        return sum / _selection.Count;
    }
}
