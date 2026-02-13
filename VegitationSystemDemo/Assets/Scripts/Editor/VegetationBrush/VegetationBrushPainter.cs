using UnityEngine;
using UnityEditor;

public class VegetationBrushPainter
{
    public void Paint(VegetationPalette palette, Vector3 center, Vector3 normal,
                      float brushSize, int density, Transform container, LayerMask surfaceMask,
                      bool onlyUpFace, float maxSlopeAngle)
    {
        if (palette == null || !palette.HasValidEntries()) return;

        Undo.SetCurrentGroupName("Vegetation Brush Paint");
        int undoGroup = Undo.GetCurrentGroup();

        Vector3 tangent = GetTangent(normal);
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        for (int i = 0; i < density; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * brushSize;
            Vector3 offset = tangent * randomOffset.x + bitangent * randomOffset.y;
            Vector3 testPos = center + offset + normal * 2f;

            if (Physics.Raycast(testPos, -normal, out RaycastHit hit, 10f, surfaceMask))
            {
                if (onlyUpFace && Vector3.Angle(Vector3.up, hit.normal) > maxSlopeAngle)
                    continue;

                var spawnResult = palette.GetRandomPrefab();
                if (spawnResult.prefab != null)
                    SpawnPrefab(spawnResult, hit.point, hit.normal, container);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    public void PaintLine(VegetationPalette palette, Vector3 start, Vector3 startNormal,
                          Vector3 end, Vector3 endNormal, float lineThickness, int densityPerUnit,
                          Transform container, LayerMask surfaceMask,
                          bool onlyUpFace, float maxSlopeAngle)
    {
        if (palette == null || !palette.HasValidEntries()) return;

        float lineLength = Vector3.Distance(start, end);
        if (lineLength < 0.01f) return;

        Undo.SetCurrentGroupName("Vegetation Brush Line Paint");
        int undoGroup = Undo.GetCurrentGroup();

        Vector3 lineDir = (end - start).normalized;
        Vector3 avgNormal = ((startNormal + endNormal) * 0.5f).normalized;
        Vector3 perpendicular = Vector3.Cross(lineDir, avgNormal).normalized;

        int spawnCount = Mathf.Max(1, Mathf.RoundToInt(lineLength * densityPerUnit));
        float halfThickness = lineThickness * 0.5f;

        for (int i = 0; i < spawnCount; i++)
        {
            float t = Random.value;
            Vector3 pointOnLine = Vector3.Lerp(start, end, t);
            float lateralOffset = Random.Range(-halfThickness, halfThickness);
            Vector3 spawnPos = pointOnLine + perpendicular * lateralOffset;

            Vector3 normal = Vector3.Lerp(startNormal, endNormal, t).normalized;

            if (Physics.Raycast(spawnPos + normal * 2f, -normal, out RaycastHit hit, 10f, surfaceMask))
            {
                if (onlyUpFace && Vector3.Angle(Vector3.up, hit.normal) > maxSlopeAngle)
                    continue;

                var spawnResult = palette.GetRandomPrefab();
                if (spawnResult.prefab != null)
                    SpawnPrefab(spawnResult, hit.point, hit.normal, container);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    void SpawnPrefab(VegetationPalette.SpawnResult spawn, Vector3 position, Vector3 surfaceNormal, Transform parent)
    {
        Quaternion rotation = CalculateRotation(spawn.settings, surfaceNormal);
        float scale = Random.Range(spawn.settings.scaleRange.x, spawn.settings.scaleRange.y);

        GameObject instance = PrefabUtility.InstantiatePrefab(spawn.prefab) as GameObject;
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.transform.localScale = Vector3.one * scale;

        if (parent != null)
            instance.transform.SetParent(parent);

        Undo.RegisterCreatedObjectUndo(instance, "Spawn Vegetation");
    }

    public void PaintToBakedData(VegetationPalette palette, Vector3 center, Vector3 normal,
                                 float brushSize, int density, LayerMask surfaceMask,
                                 bool onlyUpFace, float maxSlopeAngle, BakedDataPaintTarget target)
    {
        if (palette == null || !palette.HasValidEntries()) return;

        Vector3 tangent = GetTangent(normal);
        Vector3 bitangent = Vector3.Cross(normal, tangent);

        for (int i = 0; i < density; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * brushSize;
            Vector3 offset = tangent * randomOffset.x + bitangent * randomOffset.y;
            Vector3 testPos = center + offset + normal * 2f;

            if (Physics.Raycast(testPos, -normal, out RaycastHit hit, 10f, surfaceMask))
            {
                if (onlyUpFace && Vector3.Angle(Vector3.up, hit.normal) > maxSlopeAngle)
                    continue;

                var spawnResult = palette.GetRandomPrefab();
                target.TryAddEntry(spawnResult, hit.point, hit.normal);
            }
        }
    }

    public void PaintLineToBakedData(VegetationPalette palette, Vector3 start, Vector3 startNormal,
                                     Vector3 end, Vector3 endNormal, float lineThickness, int densityPerUnit,
                                     LayerMask surfaceMask, bool onlyUpFace, float maxSlopeAngle,
                                     BakedDataPaintTarget target)
    {
        if (palette == null || !palette.HasValidEntries()) return;

        float lineLength = Vector3.Distance(start, end);
        if (lineLength < 0.01f) return;

        Vector3 lineDir = (end - start).normalized;
        Vector3 avgNormal = ((startNormal + endNormal) * 0.5f).normalized;
        Vector3 perpendicular = Vector3.Cross(lineDir, avgNormal).normalized;

        int spawnCount = Mathf.Max(1, Mathf.RoundToInt(lineLength * densityPerUnit));
        float halfThickness = lineThickness * 0.5f;

        for (int i = 0; i < spawnCount; i++)
        {
            float t = Random.value;
            Vector3 pointOnLine = Vector3.Lerp(start, end, t);
            float lateralOffset = Random.Range(-halfThickness, halfThickness);
            Vector3 spawnPos = pointOnLine + perpendicular * lateralOffset;

            Vector3 normal = Vector3.Lerp(startNormal, endNormal, t).normalized;

            if (Physics.Raycast(spawnPos + normal * 2f, -normal, out RaycastHit hit, 10f, surfaceMask))
            {
                if (onlyUpFace && Vector3.Angle(Vector3.up, hit.normal) > maxSlopeAngle)
                    continue;

                var spawnResult = palette.GetRandomPrefab();
                target.TryAddEntry(spawnResult, hit.point, hit.normal);
            }
        }
    }

    Quaternion CalculateRotation(VegetationPalette.VegetationSettings settings, Vector3 surfaceNormal)
    {
        Quaternion surfaceAlign = Quaternion.FromToRotation(Vector3.up, surfaceNormal);

        float yRotation = settings.randomYRotation ? Random.Range(0f, 360f) : 0f;
        float xTilt = Random.Range(settings.tiltRange.x, settings.tiltRange.y);
        float zTilt = Random.Range(settings.tiltRange.x, settings.tiltRange.y);

        Quaternion randomRotation = Quaternion.Euler(xTilt, yRotation, zTilt);

        return surfaceAlign * randomRotation;
    }

    Vector3 GetTangent(Vector3 normal)
    {
        Vector3 tangent = Vector3.Cross(normal, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(normal, Vector3.right);
        return tangent.normalized;
    }
}
