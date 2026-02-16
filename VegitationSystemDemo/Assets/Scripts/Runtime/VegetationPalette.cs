using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "VegetationPalette", menuName = "Vegeration For Dummies/Vegetation Palette")]
public class VegetationPalette : ScriptableObject
{
    [System.Serializable]
    public class PrefabItem
    {
        public GameObject prefab;
        [Tooltip("Leave at 1 for even distribution. Increase to make this item more common.")]
        public float weight = 1f;
    }

    [System.Serializable]
    public class VegetationSettings
    {
        [Header("Rotation")]
        public bool randomYRotation = true;
        [Tooltip("Random tilt applied to X and Z axes")]
        public Vector2 tiltRange = new Vector2(-5f, 5f);

        [Header("Scale")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
    }

    [Header("Distribution")]
    [Range(0f, 1f)]
    [Tooltip("0 = all flowers, 1 = all grass")]
    public float grassRatio = 0.8f;

    [Header("Grass")]
    public List<PrefabItem> grassPrefabs = new();
    public VegetationSettings grassSettings = new()
    {
        randomYRotation = true,
        tiltRange = new Vector2(-8f, 8f),
        scaleRange = new Vector2(0.7f, 1.3f)
    };

    [Header("Flowers")]
    public List<PrefabItem> flowerPrefabs = new();
    public VegetationSettings flowerSettings = new()
    {
        randomYRotation = true,
        tiltRange = new Vector2(-3f, 3f),
        scaleRange = new Vector2(0.85f, 1.15f)
    };

    public struct SpawnResult
    {
        public GameObject prefab;
        public VegetationSettings settings;
    }

    public SpawnResult GetRandomPrefab()
    {
        bool useGrass = Random.value < grassRatio;

        if (useGrass && HasValidGrass())
            return new SpawnResult { prefab = GetWeightedFromList(grassPrefabs), settings = grassSettings };

        if (!useGrass && HasValidFlowers())
            return new SpawnResult { prefab = GetWeightedFromList(flowerPrefabs), settings = flowerSettings };

        if (HasValidGrass())
            return new SpawnResult { prefab = GetWeightedFromList(grassPrefabs), settings = grassSettings };

        if (HasValidFlowers())
            return new SpawnResult { prefab = GetWeightedFromList(flowerPrefabs), settings = flowerSettings };

        return default;
    }

    GameObject GetWeightedFromList(List<PrefabItem> items)
    {
        float totalWeight = 0f;
        foreach (var item in items)
        {
            if (item.prefab != null)
                totalWeight += item.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float random = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var item in items)
        {
            if (item.prefab == null) continue;
            cumulative += item.weight;
            if (random <= cumulative)
                return item.prefab;
        }

        return items[items.Count - 1].prefab;
    }

    public bool HasValidGrass()
    {
        foreach (var item in grassPrefabs)
            if (item.prefab != null) return true;
        return false;
    }

    public bool HasValidFlowers()
    {
        foreach (var item in flowerPrefabs)
            if (item.prefab != null) return true;
        return false;
    }

    public bool HasValidEntries() => HasValidGrass() || HasValidFlowers();
}
