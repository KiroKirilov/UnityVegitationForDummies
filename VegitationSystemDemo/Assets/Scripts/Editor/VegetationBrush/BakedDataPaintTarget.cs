using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class BakedDataPaintTarget
{
    GrassInstanceData _data;
    GrassRenderer _renderer;
    Dictionary<string, int> _prefabNameToMeshType = new();
    List<string> _unmappedPrefabs = new();
    List<GrassInstanceData.Entry> _pendingEntries = new();
    bool _strokeActive;

    public int PendingCount => _pendingEntries.Count;
    public bool IsConfigured => _data != null && _prefabNameToMeshType.Count > 0;
    public bool IsStrokeActive => _strokeActive;
    public List<string> UnmappedPrefabs => _unmappedPrefabs;

    public void Configure(GrassInstanceData data, VegetationPalette palette)
    {
        _data = data;
        _renderer = null;
        _prefabNameToMeshType.Clear();
        _unmappedPrefabs.Clear();

        if (data == null || data.meshTypes == null || palette == null)
            return;

        var meshTypeByName = new Dictionary<string, int>();
        for (int i = 0; i < data.meshTypes.Length; i++)
            meshTypeByName[data.meshTypes[i].name] = i;

        MapPrefabs(palette.grassPrefabs, meshTypeByName);
        MapPrefabs(palette.flowerPrefabs, meshTypeByName);
    }

    void MapPrefabs(List<VegetationPalette.PrefabItem> prefabs, Dictionary<string, int> meshTypeByName)
    {
        if (prefabs == null) return;

        foreach (var item in prefabs)
        {
            if (item.prefab == null) continue;

            string name = item.prefab.name;
            if (_prefabNameToMeshType.ContainsKey(name))
                continue;

            if (meshTypeByName.TryGetValue(name, out int meshType))
                _prefabNameToMeshType[name] = meshType;
            else if (!_unmappedPrefabs.Contains(name))
                _unmappedPrefabs.Add(name);
        }
    }

    public void BeginStroke()
    {
        if (_strokeActive) return;

        _strokeActive = true;
        _pendingEntries.Clear();
    }

    public bool TryAddEntry(VegetationPalette.SpawnResult spawn, Vector3 position,
                            Vector3 surfaceNormal)
    {
        if (spawn.prefab == null) return false;
        if (!_prefabNameToMeshType.TryGetValue(spawn.prefab.name, out int meshType))
            return false;

        Quaternion rotation = CalculateRotation(spawn.settings, surfaceNormal);
        float scale = Random.Range(spawn.settings.scaleRange.x, spawn.settings.scaleRange.y);

        _pendingEntries.Add(new GrassInstanceData.Entry
        {
            position = position,
            rotation = rotation,
            scale = scale,
            meshType = meshType
        });

        return true;
    }

    public void EndStroke()
    {
        if (!_strokeActive) return;
        _strokeActive = false;

        if (_pendingEntries.Count == 0) return;
        if (_data == null) return;

        Flush();
    }

    void Flush()
    {
        Undo.RecordObject(_data, "Paint Baked Vegetation");

        var existing = _data.instances ?? System.Array.Empty<GrassInstanceData.Entry>();
        var newArray = new GrassInstanceData.Entry[existing.Length + _pendingEntries.Count];
        System.Array.Copy(existing, newArray, existing.Length);

        for (int i = 0; i < _pendingEntries.Count; i++)
            newArray[existing.Length + i] = _pendingEntries[i];

        _data.instances = newArray;
        _pendingEntries.Clear();

        RecalculateBounds();
        EditorUtility.SetDirty(_data);

        FindRenderer();
        if (_renderer != null)
            _renderer.RefreshGPUData();

        Debug.Log($"[VegetationBrush] Added {newArray.Length - existing.Length} baked instances. Total: {newArray.Length}");
    }

    void RecalculateBounds()
    {
        if (_data.instances == null || _data.instances.Length == 0)
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

    void FindRenderer()
    {
        if (_renderer != null) return;
        _renderer = Object.FindFirstObjectByType<GrassRenderer>();
    }

    public void RefreshAfterUndo()
    {
        FindRenderer();
        if (_renderer != null)
            _renderer.RefreshGPUData();
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
}
