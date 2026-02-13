using System.Collections.Generic;
using System.Reflection;
using DaisyParty.Rendering;
using UnityEditor;
using UnityEngine;

static class GrassInstancePicker
{
    public struct InstanceWorldBounds
    {
        public Vector3 center;
        public float radius;
    }

    static readonly MethodInfo IntersectRayMeshMethod;

    static GrassInstancePicker()
    {
        IntersectRayMeshMethod = typeof(HandleUtility).GetMethod(
            "IntersectRayMesh",
            BindingFlags.Static | BindingFlags.NonPublic);
    }

    public static bool TryGetWorldBounds(GrassInstanceData data, GrassInstanceData.Entry entry,
        out InstanceWorldBounds result)
    {
        result = default;

        if (data.meshTypes == null || entry.meshType < 0 || entry.meshType >= data.meshTypes.Length)
            return false;

        var mesh = data.meshTypes[entry.meshType].mesh;
        if (mesh == null) return false;

        var meshBounds = mesh.bounds;
        result.center = entry.position + entry.rotation * (meshBounds.center * entry.scale);
        result.radius = meshBounds.extents.magnitude * entry.scale;
        return true;
    }

    public static int Pick(GrassInstanceData data, Ray ray, float editRadius,
        Vector3 cameraPosition, int filterMeshType)
    {
        var hits = PickAll(data, ray, editRadius, cameraPosition, filterMeshType);
        return hits.Count > 0 ? hits[0] : -1;
    }

    public static List<int> PickAll(GrassInstanceData data, Ray ray, float editRadius,
        Vector3 cameraPosition, int filterMeshType)
    {
        var candidates = CollectBoundingSphereHits(data, ray, editRadius, cameraPosition, filterMeshType);

        if (IntersectRayMeshMethod != null)
        {
            var triangleHits = RefineWithTriangleTest(data, ray, candidates);
            if (triangleHits.Count > 0)
                return triangleHits;
        }

        candidates.Sort((a, b) => a.rayT.CompareTo(b.rayT));
        var fallback = new List<int>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++)
            fallback.Add(candidates[i].index);
        return fallback;
    }

    struct BoundsCandidate
    {
        public int index;
        public float rayT;
    }

    static List<BoundsCandidate> CollectBoundingSphereHits(GrassInstanceData data, Ray ray,
        float editRadius, Vector3 cameraPosition, int filterMeshType)
    {
        var candidates = new List<BoundsCandidate>();

        for (int i = 0; i < data.instances.Length; i++)
        {
            var entry = data.instances[i];
            if (filterMeshType >= 0 && entry.meshType != filterMeshType) continue;

            float distToCam = Vector3.Distance(entry.position, cameraPosition);
            if (distToCam > editRadius) continue;

            Vector3 testCenter;
            float pickRadius;

            if (TryGetWorldBounds(data, entry, out var wb))
            {
                testCenter = wb.center;
                pickRadius = Mathf.Max(wb.radius, distToCam * 0.015f);
            }
            else
            {
                testCenter = entry.position;
                pickRadius = Mathf.Max(entry.scale * 0.5f, distToCam * 0.015f);
            }

            Vector3 v = testCenter - ray.origin;
            float t = Vector3.Dot(v, ray.direction);
            if (t < 0f) continue;

            Vector3 closestOnRay = ray.origin + ray.direction * t;
            float distToRay = Vector3.Distance(testCenter, closestOnRay);

            if (distToRay < pickRadius)
                candidates.Add(new BoundsCandidate { index = i, rayT = t });
        }

        return candidates;
    }

    static List<int> RefineWithTriangleTest(GrassInstanceData data, Ray ray,
        List<BoundsCandidate> candidates)
    {
        var hits = new List<(int index, float dist)>();
        var invokeParams = new object[4];
        invokeParams[0] = ray;

        for (int c = 0; c < candidates.Count; c++)
        {
            int i = candidates[c].index;
            var entry = data.instances[i];

            if (data.meshTypes == null || entry.meshType < 0 || entry.meshType >= data.meshTypes.Length)
                continue;

            var mesh = data.meshTypes[entry.meshType].mesh;
            if (mesh == null) continue;

            var trs = Matrix4x4.TRS(entry.position, entry.rotation, Vector3.one * entry.scale);

            invokeParams[1] = mesh;
            invokeParams[2] = trs;
            invokeParams[3] = null;

            bool didHit = (bool)IntersectRayMeshMethod.Invoke(null, invokeParams);

            if (didHit)
            {
                var hit = (RaycastHit)invokeParams[3];
                hits.Add((i, hit.distance));
            }
        }

        hits.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<int>(hits.Count);
        for (int i = 0; i < hits.Count; i++)
            result.Add(hits[i].index);
        return result;
    }

    public static List<int> GetIndicesInRadius(GrassInstanceData data, Vector3 center,
        float radius, int filterMeshType)
    {
        var result = new List<int>();

        for (int i = 0; i < data.instances.Length; i++)
        {
            var entry = data.instances[i];
            if (filterMeshType >= 0 && entry.meshType != filterMeshType) continue;

            float meshRadius = 0f;
            if (TryGetWorldBounds(data, entry, out var wb))
                meshRadius = wb.radius;

            float threshold = radius + meshRadius;
            if ((entry.position - center).sqrMagnitude <= threshold * threshold)
                result.Add(i);
        }

        return result;
    }
}
