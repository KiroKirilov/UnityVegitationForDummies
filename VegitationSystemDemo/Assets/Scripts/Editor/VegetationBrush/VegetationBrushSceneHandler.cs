using UnityEngine;
using UnityEditor;
using System;

public class VegetationBrushSceneHandler
{
    static readonly Color BrushColor = new Color(0.2f, 1f, 0.2f, 0.8f);
    static readonly Color BrushFillColor = new Color(0.2f, 1f, 0.2f, 0.15f);
    static readonly Color LineColor = new Color(0.3f, 0.7f, 1f, 0.8f);
    static readonly Color LineFillColor = new Color(0.3f, 0.7f, 1f, 0.15f);

    const int CapsuleSegments = 24;

    Vector3 _lastPaintPosition;
    float _minPaintDistance = 0.5f;
    bool _isPainting;

    public Action onStrokeEnd;

    bool _isDraggingLine;
    Vector3 _lineStart;
    Vector3 _lineStartNormal;
    Vector3 _lineEnd;
    Vector3 _lineEndNormal;

    public void HandleCircleMode(SceneView sceneView, float brushSize, LayerMask mask,
                                 Action<Vector3, Vector3> onPaint)
    {
        Event e = Event.current;
        ConsumeDefaultControl(e);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 1000f, mask);

        if (hasHit)
        {
            DrawCircleBrush(hit.point, hit.normal, brushSize);
            HandleCircleInput(e, hit.point, hit.normal, onPaint);
        }
        else if (_isPainting)
        {
            _isPainting = false;
            onStrokeEnd?.Invoke();
        }

        if (e.type == EventType.MouseUp && e.button == 0 && _isPainting)
        {
            _isPainting = false;
            onStrokeEnd?.Invoke();
        }

        sceneView.Repaint();
    }

    public void HandleLineMode(SceneView sceneView, float lineThickness, LayerMask mask,
                               Action<Vector3, Vector3, Vector3, Vector3> onLinePaint)
    {
        Event e = Event.current;
        ConsumeDefaultControl(e);

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, 1000f, mask);

        if (hasHit)
        {
            if (_isDraggingLine)
            {
                _lineEnd = hit.point;
                _lineEndNormal = hit.normal;
                DrawLineBrush(_lineStart, _lineStartNormal, _lineEnd, _lineEndNormal, lineThickness);
            }
            else
            {
                DrawCircleBrush(hit.point, hit.normal, lineThickness * 0.5f);
            }

            HandleLineInput(e, hit, lineThickness, onLinePaint);
        }

        if (e.type == EventType.MouseUp && e.button == 0 && _isDraggingLine)
        {
            if (Vector3.Distance(_lineStart, _lineEnd) > 0.1f)
                onLinePaint?.Invoke(_lineStart, _lineStartNormal, _lineEnd, _lineEndNormal);

            _isDraggingLine = false;
            e.Use();
        }

        sceneView.Repaint();
    }

    void ConsumeDefaultControl(Event e)
    {
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(controlId);
    }

    void HandleCircleInput(Event e, Vector3 hitPoint, Vector3 hitNormal, Action<Vector3, Vector3> onPaint)
    {
        if (e.alt) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            _isPainting = true;
            _lastPaintPosition = hitPoint;
            onPaint?.Invoke(hitPoint, hitNormal);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && _isPainting)
        {
            float distance = Vector3.Distance(hitPoint, _lastPaintPosition);
            if (distance >= _minPaintDistance)
            {
                _lastPaintPosition = hitPoint;
                onPaint?.Invoke(hitPoint, hitNormal);
            }
            e.Use();
        }
    }

    void HandleLineInput(Event e, RaycastHit hit, float lineThickness,
                         Action<Vector3, Vector3, Vector3, Vector3> onLinePaint)
    {
        if (e.alt) return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            _isDraggingLine = true;
            _lineStart = hit.point;
            _lineStartNormal = hit.normal;
            _lineEnd = hit.point;
            _lineEndNormal = hit.normal;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && e.button == 0 && _isDraggingLine)
        {
            e.Use();
        }
    }

    void DrawCircleBrush(Vector3 center, Vector3 normal, float radius)
    {
        Handles.color = BrushFillColor;
        Handles.DrawSolidDisc(center, normal, radius);

        Handles.color = BrushColor;
        Handles.DrawWireDisc(center, normal, radius);

        float dotSize = HandleUtility.GetHandleSize(center) * 0.04f;
        Handles.color = Color.white;
        Handles.DrawSolidDisc(center, normal, dotSize);
    }

    void DrawLineBrush(Vector3 start, Vector3 startNormal, Vector3 end, Vector3 endNormal,
                       float thickness)
    {
        float halfThickness = thickness * 0.5f;
        Vector3 lineDir = end - start;
        float lineLength = lineDir.magnitude;

        if (lineLength < 0.01f)
        {
            DrawCircleBrush(start, startNormal, halfThickness);
            return;
        }

        lineDir /= lineLength;
        Vector3 avgNormal = ((startNormal + endNormal) * 0.5f).normalized;
        Vector3 perp = Vector3.Cross(lineDir, avgNormal).normalized;

        DrawCapsuleFill(start, end, avgNormal, perp, halfThickness);
        DrawCapsuleOutline(start, end, startNormal, endNormal, perp, halfThickness);

        Handles.color = LineColor;
        Handles.DrawDottedLine(start, end, 4f);

        float dotSize = HandleUtility.GetHandleSize(start) * 0.04f;
        Handles.color = Color.white;
        Handles.DrawSolidDisc(start, startNormal, dotSize);
        Handles.DrawSolidDisc(end, endNormal, dotSize);
    }

    void DrawCapsuleFill(Vector3 start, Vector3 end, Vector3 normal, Vector3 perp, float radius)
    {
        Handles.color = LineFillColor;

        Handles.DrawSolidDisc(start, normal, radius);
        Handles.DrawSolidDisc(end, normal, radius);

        Vector3 s1 = start + perp * radius;
        Vector3 s2 = start - perp * radius;
        Vector3 e1 = end + perp * radius;
        Vector3 e2 = end - perp * radius;
        Handles.DrawAAConvexPolygon(s1, e1, e2, s2);
    }

    void DrawCapsuleOutline(Vector3 start, Vector3 end, Vector3 startNormal, Vector3 endNormal,
                            Vector3 perp, float radius)
    {
        Handles.color = LineColor;

        Handles.DrawWireDisc(start, startNormal, radius);
        Handles.DrawWireDisc(end, endNormal, radius);

        Vector3 s1 = start + perp * radius;
        Vector3 s2 = start - perp * radius;
        Vector3 e1 = end + perp * radius;
        Vector3 e2 = end - perp * radius;
        Handles.DrawLine(s1, e1);
        Handles.DrawLine(s2, e2);
    }

    public void SetMinPaintDistance(float distance)
    {
        _minPaintDistance = Mathf.Max(0.1f, distance);
    }
}
