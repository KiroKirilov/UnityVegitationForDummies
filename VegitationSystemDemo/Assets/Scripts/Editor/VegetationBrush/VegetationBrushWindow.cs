using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

public class VegetationBrushWindow : EditorWindow
{
    enum BrushMode { Circle, Line }
    enum OutputMode { Scene, BakedData }

    VegetationPalette _palette;
    BrushMode _brushMode;
    OutputMode _outputMode;
    float _brushSize = 3f;
    int _density = 5;
    float _lineThickness = 2f;
    LayerMask _paintMask = ~0;
    bool _onlyUpFace;
    float _maxSlopeAngle = 30f;
    Transform _container;
    bool _isPaintModeActive;

    GrassInstanceData _bakedData;
    BakedDataPaintTarget _bakedTarget;
    VegetationPalette _lastConfiguredPalette;
    GrassInstanceData _lastConfiguredData;

    VegetationBrushSceneHandler _sceneHandler;
    VegetationBrushPainter _painter;

    [MenuItem("Tools/Vegetation Brush")]
    public static void ShowWindow()
    {
        var window = GetWindow<VegetationBrushWindow>("Vegetation Brush");
        window.minSize = new Vector2(280, 400);
    }

    void OnEnable()
    {
        _sceneHandler = new VegetationBrushSceneHandler();
        _sceneHandler.onStrokeEnd = OnStrokeEnd;
        _painter = new VegetationBrushPainter();
        _bakedTarget = new BakedDataPaintTarget();
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
    }

    void OnDisable()
    {
        if (_bakedTarget != null && _bakedTarget.IsStrokeActive)
            _bakedTarget.EndStroke();

        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    void OnUndoRedo()
    {
        if (_outputMode == OutputMode.BakedData && _bakedData != null)
        {
            _bakedTarget.RefreshAfterUndo();
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Vegetation Brush", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        DrawPaletteSection();
        EditorGUILayout.Space(10);

        DrawOutputModeSection();
        EditorGUILayout.Space(10);

        DrawBrushSettingsSection();
        EditorGUILayout.Space(10);

        DrawSurfaceSection();
        EditorGUILayout.Space(10);

        if (_outputMode == OutputMode.Scene)
        {
            DrawContainerSection();
            EditorGUILayout.Space(15);
        }

        DrawPaintModeSection();
        EditorGUILayout.Space(10);

        DrawHelpSection();
    }

    void DrawPaletteSection()
    {
        EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);

        var newPalette = (VegetationPalette)EditorGUILayout.ObjectField(
            "Vegetation Palette", _palette, typeof(VegetationPalette), false);

        if (newPalette != _palette)
        {
            _palette = newPalette;
            InvalidateBakedConfig();
        }

        if (_palette == null)
        {
            EditorGUILayout.HelpBox("Assign a palette to start painting.", MessageType.Info);
        }
        else if (!_palette.HasValidEntries())
        {
            EditorGUILayout.HelpBox("Palette has no valid prefab entries.", MessageType.Warning);
        }
    }

    void DrawOutputModeSection()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        _outputMode = (OutputMode)EditorGUILayout.EnumPopup("Target", _outputMode);

        if (_outputMode != OutputMode.BakedData) return;

        var newData = (GrassInstanceData)EditorGUILayout.ObjectField(
            "Grass Instance Data", _bakedData, typeof(GrassInstanceData), false);

        if (newData != _bakedData)
        {
            _bakedData = newData;
            InvalidateBakedConfig();
        }

        if (_bakedData == null)
        {
            EditorGUILayout.HelpBox("Assign a GrassInstanceData asset.", MessageType.Info);
            return;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to paint into baked data.\n" +
                "The GrassRenderer must be active to see results.",
                MessageType.Warning);
            return;
        }

        EnsureBakedConfig();

        var unmapped = _bakedTarget.UnmappedPrefabs;
        if (unmapped.Count > 0)
            EditorGUILayout.HelpBox(
                $"Unmapped prefabs (will be skipped):\n{string.Join(", ", unmapped)}",
                MessageType.Warning);

        int count = _bakedData.instances?.Length ?? 0;
        int pending = _bakedTarget.PendingCount;
        string label = $"Instances: {count}";
        if (pending > 0) label += $" (+{pending} pending)";
        EditorGUILayout.LabelField(label);
    }

    void DrawBrushSettingsSection()
    {
        EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

        _brushMode = (BrushMode)EditorGUILayout.EnumPopup("Mode", _brushMode);

        if (_brushMode == BrushMode.Circle)
        {
            _brushSize = EditorGUILayout.Slider("Brush Size", _brushSize, 0.5f, 20f);
        }
        else
        {
            _lineThickness = EditorGUILayout.Slider("Line Thickness", _lineThickness, 0.5f, 10f);
        }

        _density = EditorGUILayout.IntSlider("Density", _density, 1, 100);
    }

    void DrawSurfaceSection()
    {
        EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);

        _paintMask = EditorGUILayout.MaskField("Paint Mask",
            InternalEditorUtility.LayerMaskToConcatenatedLayersMask(_paintMask),
            InternalEditorUtility.layers);
        _paintMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(_paintMask);

        _onlyUpFace = EditorGUILayout.Toggle("Only Up Faces", _onlyUpFace);

        if (_onlyUpFace)
        {
            EditorGUI.indentLevel++;
            _maxSlopeAngle = EditorGUILayout.Slider("Max Slope Angle", _maxSlopeAngle, 0f, 90f);
            EditorGUI.indentLevel--;
        }
    }

    void DrawContainerSection()
    {
        EditorGUILayout.LabelField("Container", EditorStyles.boldLabel);

        _container = (Transform)EditorGUILayout.ObjectField(
            "Parent Container", _container, typeof(Transform), true);

        if (GUILayout.Button("Create New Container"))
        {
            CreateNewContainer();
        }
    }

    void DrawPaintModeSection()
    {
        bool canPaint = _palette != null && _palette.HasValidEntries();

        if (_outputMode == OutputMode.BakedData)
            canPaint = canPaint && _bakedData != null && EditorApplication.isPlaying && _bakedTarget.IsConfigured;

        EditorGUI.BeginDisabledGroup(!canPaint);

        Color originalBg = GUI.backgroundColor;
        if (_isPaintModeActive)
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);

        string buttonText = _isPaintModeActive ? "Disable Paint Mode" : "Enable Paint Mode";
        if (GUILayout.Button(buttonText, GUILayout.Height(30)))
        {
            if (_isPaintModeActive && _bakedTarget != null && _bakedTarget.IsStrokeActive)
                _bakedTarget.EndStroke();

            _isPaintModeActive = !_isPaintModeActive;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = originalBg;
        EditorGUI.EndDisabledGroup();

        if (_isPaintModeActive)
        {
            string modeLabel = _brushMode == BrushMode.Circle ? "Circle" : "Line";
            string targetLabel = _outputMode == OutputMode.BakedData ? " → Baked Data" : "";
            EditorGUILayout.HelpBox(
                $"Paint mode is ACTIVE ({modeLabel}{targetLabel}). Click in Scene view to paint.",
                MessageType.None);
        }
    }

    void DrawHelpSection()
    {
        if (_brushMode == BrushMode.Circle)
        {
            EditorGUILayout.HelpBox(
                "LMB = Paint\n" +
                "LMB + Drag = Continuous paint\n" +
                "Alt + LMB = Orbit (normal Unity behavior)",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "LMB = Set start point\n" +
                "Drag = Preview line\n" +
                "Release = Fill line with vegetation\n" +
                "Alt + LMB = Orbit (normal Unity behavior)",
                MessageType.Info);
        }

        if (_outputMode == OutputMode.BakedData)
        {
            EditorGUILayout.HelpBox(
                "Baked Data mode: instances appear when stroke completes (mouse-up).\n" +
                "Changes persist after exiting Play Mode.",
                MessageType.Info);
        }
    }

    void CreateNewContainer()
    {
        GameObject containerGO = new GameObject("Vegetation_Container");
        Undo.RegisterCreatedObjectUndo(containerGO, "Create Vegetation Container");
        _container = containerGO.transform;
        Selection.activeGameObject = containerGO;
    }

    void EnsureBakedConfig()
    {
        if (_palette == _lastConfiguredPalette && _bakedData == _lastConfiguredData)
            return;

        _bakedTarget.Configure(_bakedData, _palette);
        _lastConfiguredPalette = _palette;
        _lastConfiguredData = _bakedData;
    }

    void InvalidateBakedConfig()
    {
        _lastConfiguredPalette = null;
        _lastConfiguredData = null;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!_isPaintModeActive) return;
        if (_palette == null || !_palette.HasValidEntries()) return;

        if (_outputMode == OutputMode.BakedData)
        {
            if (!EditorApplication.isPlaying || _bakedData == null) return;
            EnsureBakedConfig();
        }

        if (_brushMode == BrushMode.Circle)
        {
            _sceneHandler.SetMinPaintDistance(_brushSize * 0.3f);
            _sceneHandler.HandleCircleMode(sceneView, _brushSize, _paintMask,
                _outputMode == OutputMode.BakedData ? OnCirclePaintBaked : OnCirclePaint);
        }
        else
        {
            _sceneHandler.HandleLineMode(sceneView, _lineThickness, _paintMask,
                _outputMode == OutputMode.BakedData ? OnLinePaintBaked : OnLinePaint);
        }
    }

    void OnCirclePaint(Vector3 center, Vector3 normal)
    {
        _painter.Paint(_palette, center, normal, _brushSize, _density, _container, _paintMask,
                       _onlyUpFace, _maxSlopeAngle);
    }

    void OnLinePaint(Vector3 start, Vector3 startNormal, Vector3 end, Vector3 endNormal)
    {
        _painter.PaintLine(_palette, start, startNormal, end, endNormal,
                           _lineThickness, _density, _container, _paintMask,
                           _onlyUpFace, _maxSlopeAngle);
    }

    void OnCirclePaintBaked(Vector3 center, Vector3 normal)
    {
        _bakedTarget.BeginStroke();
        _painter.PaintToBakedData(_palette, center, normal, _brushSize, _density, _paintMask,
                                  _onlyUpFace, _maxSlopeAngle, _bakedTarget);
    }

    void OnLinePaintBaked(Vector3 start, Vector3 startNormal, Vector3 end, Vector3 endNormal)
    {
        _bakedTarget.BeginStroke();
        _painter.PaintLineToBakedData(_palette, start, startNormal, end, endNormal,
                                      _lineThickness, _density, _paintMask,
                                      _onlyUpFace, _maxSlopeAngle, _bakedTarget);
        _bakedTarget.EndStroke();
    }

    void OnStrokeEnd()
    {
        if (_outputMode == OutputMode.BakedData && _bakedTarget != null && _bakedTarget.IsStrokeActive)
        {
            _bakedTarget.EndStroke();
            Repaint();
        }
    }
}
