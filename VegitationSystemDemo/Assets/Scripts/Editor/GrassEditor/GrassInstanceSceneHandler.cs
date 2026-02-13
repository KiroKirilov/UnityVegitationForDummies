using System.Collections.Generic;
using System.Linq;
using DaisyParty.Rendering;
using UnityEditor;
using UnityEngine;

class GrassInstanceSceneHandler
{
    const int MAX_MARKERS = 500;
    static readonly Color MarkerColor = new Color(1f, 0.8f, 0.2f, 0.7f);
    static readonly Color SelectedMarkerColor = new Color(0.2f, 1f, 0.2f, 1f);
    static readonly Color SelectedBoundsColor = new Color(0.2f, 1f, 0.2f, 0.5f);
    static readonly Color EditRadiusColor = new Color(0.3f, 0.7f, 1f, 0.3f);

    const float CYCLE_CLICK_THRESHOLD = 5f;

    Quaternion _handleRotation = Quaternion.identity;
    Vector3 _handleScale = Vector3.one;
    int _lastSelectionHash;

    Vector2 _lastClickPos;
    List<int> _cycleOverlaps;
    int _cycleIndex;

    public struct HandleResult
    {
        public bool hasPositionDelta;
        public Vector3 positionDelta;

        public bool hasRotationChange;
        public Quaternion newRotation;

        public bool hasScaleChange;
        public float scaleFactor;

        public bool deletePressed;
        public bool escapePressed;
    }

    public HandleResult OnSceneGUI(GrassInstanceData data, SceneView sceneView,
        HashSet<int> selection, float editRadius, int filterMeshType)
    {
        var result = new HandleResult();
        Event e = Event.current;
        Vector3 camPos = sceneView.camera.transform.position;

        if (e.type == EventType.Layout)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);
        }

        if (selection.Count > 0)
            DrawTransformHandles(data, selection, ref result);

        HandleMouseInput(data, e, selection, editRadius, camPos, filterMeshType);
        HandleKeyboard(e, selection, ref result);

        if (e.type == EventType.Repaint)
        {
            DrawEditRadius(camPos, editRadius);
            DrawMarkers(data, selection, editRadius, camPos, filterMeshType);
        }

        int selHash = ComputeSelectionHash(selection);
        if (selHash != _lastSelectionHash)
        {
            OnSelectionChanged(data, selection);
            _lastSelectionHash = selHash;
        }

        return result;
    }

    void HandleMouseInput(GrassInstanceData data, Event e, HashSet<int> selection,
        float editRadius, Vector3 camPos, int filterMeshType)
    {
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool isCycleClick = _cycleOverlaps != null
            && _cycleOverlaps.Count > 0
            && Vector2.Distance(e.mousePosition, _lastClickPos) < CYCLE_CLICK_THRESHOLD;

        int hitIndex;

        if (isCycleClick)
        {
            _cycleIndex = (_cycleIndex + 1) % _cycleOverlaps.Count;
            hitIndex = _cycleOverlaps[_cycleIndex];
        }
        else
        {
            var allHits = GrassInstancePicker.PickAll(data, ray, editRadius, camPos, filterMeshType);
            _cycleOverlaps = allHits;
            _cycleIndex = 0;
            _lastClickPos = e.mousePosition;
            hitIndex = allHits.Count > 0 ? allHits[0] : -1;
        }

        if (hitIndex >= 0)
        {
            if (e.control)
            {
                if (!selection.Remove(hitIndex))
                    selection.Add(hitIndex);
            }
            else
            {
                selection.Clear();
                selection.Add(hitIndex);
            }
            e.Use();
        }
    }

    void HandleKeyboard(Event e, HashSet<int> selection, ref HandleResult result)
    {
        if (e.type != EventType.KeyDown) return;

        if (e.keyCode == KeyCode.Delete && selection.Count > 0)
        {
            result.deletePressed = true;
            e.Use();
        }
        else if (e.keyCode == KeyCode.Escape && selection.Count > 0)
        {
            result.escapePressed = true;
            e.Use();
        }
    }

    void DrawEditRadius(Vector3 camPos, float editRadius)
    {
        Handles.color = EditRadiusColor;
        Handles.DrawWireDisc(camPos, Vector3.up, editRadius);
    }

    void DrawMarkers(GrassInstanceData data, HashSet<int> selection,
        float editRadius, Vector3 camPos, int filterMeshType)
    {
        foreach (int i in selection)
        {
            if (i < 0 || i >= data.instances.Length) continue;

            var entry = data.instances[i];
            Handles.color = SelectedMarkerColor;

            float size = HandleUtility.GetHandleSize(entry.position) * 0.06f;
            Handles.DotHandleCap(0, entry.position, Quaternion.identity, size, EventType.Repaint);

            DrawSelectionBounds(data, entry);
        }
    }

    void DrawSelectionBounds(GrassInstanceData data, GrassInstanceData.Entry entry)
    {
        if (data.meshTypes == null || entry.meshType < 0 || entry.meshType >= data.meshTypes.Length)
            return;

        var mesh = data.meshTypes[entry.meshType].mesh;
        if (mesh == null) return;

        var meshBounds = mesh.bounds;
        Matrix4x4 trs = Matrix4x4.TRS(entry.position, entry.rotation, Vector3.one * entry.scale);

        Handles.color = SelectedBoundsColor;
        var prevMatrix = Handles.matrix;
        Handles.matrix = trs;
        Handles.DrawWireCube(meshBounds.center, meshBounds.size);
        Handles.matrix = prevMatrix;
    }

    void DrawTransformHandles(GrassInstanceData data, HashSet<int> selection,
        ref HandleResult result)
    {
        ValidateSelection(data, selection);
        if (selection.Count == 0) return;

        Vector3 center = ComputeSelectionCenter(data, selection);

        switch (Tools.current)
        {
            case Tool.Move:
            case Tool.Transform:
                DrawMoveHandle(center, ref result);
                break;

            case Tool.Rotate:
                DrawRotateHandle(data, selection, center, ref result);
                break;

            case Tool.Scale:
                DrawScaleHandle(center, ref result);
                break;
        }
    }

    void DrawMoveHandle(Vector3 center, ref HandleResult result)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newPos = Handles.PositionHandle(center, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            result.hasPositionDelta = true;
            result.positionDelta = newPos - center;
        }
    }

    void DrawRotateHandle(GrassInstanceData data, HashSet<int> selection,
        Vector3 center, ref HandleResult result)
    {
        if (selection.Count == 1)
        {
            int idx = selection.First();
            EditorGUI.BeginChangeCheck();
            Quaternion newRot = Handles.RotationHandle(data.instances[idx].rotation, center);
            if (EditorGUI.EndChangeCheck())
            {
                result.hasRotationChange = true;
                result.newRotation = newRot;
            }
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            Quaternion newRot = Handles.RotationHandle(_handleRotation, center);
            if (EditorGUI.EndChangeCheck())
            {
                Quaternion delta = newRot * Quaternion.Inverse(_handleRotation);
                _handleRotation = newRot;
                result.hasRotationChange = true;
                result.newRotation = delta;
            }
        }
    }

    void DrawScaleHandle(Vector3 center, ref HandleResult result)
    {
        EditorGUI.BeginChangeCheck();
        float handleSize = HandleUtility.GetHandleSize(center);
        Vector3 newScale = Handles.ScaleHandle(_handleScale, center,
            Quaternion.identity, handleSize);
        if (EditorGUI.EndChangeCheck())
        {
            float factor = newScale.x / _handleScale.x;
            if (Mathf.Abs(factor) > 0.001f && Mathf.Abs(factor - 1f) > 0.0001f)
            {
                result.hasScaleChange = true;
                result.scaleFactor = factor;
                _handleScale = newScale;
            }
        }
    }

    void OnSelectionChanged(GrassInstanceData data, HashSet<int> selection)
    {
        _handleRotation = Quaternion.identity;
        _handleScale = Vector3.one;

        if (selection.Count == 0)
        {
            _cycleOverlaps = null;
            _cycleIndex = 0;
        }
    }

    void ValidateSelection(GrassInstanceData data, HashSet<int> selection)
    {
        if (data.instances == null)
        {
            selection.Clear();
            return;
        }

        selection.RemoveWhere(i => i < 0 || i >= data.instances.Length);
    }

    static Vector3 ComputeSelectionCenter(GrassInstanceData data, HashSet<int> selection)
    {
        Vector3 sum = Vector3.zero;
        foreach (int idx in selection)
            sum += data.instances[idx].position;
        return sum / selection.Count;
    }

    static int ComputeSelectionHash(HashSet<int> selection)
    {
        int hash = selection.Count;
        foreach (int i in selection)
            hash = hash * 31 + i;
        return hash;
    }
}
