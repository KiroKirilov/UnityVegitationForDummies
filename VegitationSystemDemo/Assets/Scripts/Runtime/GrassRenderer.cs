using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

    public class GrassRenderer : MonoBehaviour
    {
        [Header("Data")]
        public GrassInstanceData instanceData;

        [Header("Mesh Variants")]
        [Tooltip("Auto-configured by baker. One entry per meshType index.")]
        public MeshVariant[] meshVariants;

        [Header("Culling")]
        public ComputeShader cullingShader;
        [Range(50f, 500f)] public float maxRenderDistance = 150f;
        [Range(20f, 300f)] public float densityFalloffStart = 60f;

        [Header("Rendering")]
        public ShadowCastingMode shadowMode = ShadowCastingMode.On;
        public bool receiveShadows = true;

        [System.Serializable]
        public struct MeshVariant
        {
            public Mesh mesh;
            public Material material;
        }

        struct MeshGroup
        {
            public int meshType;
            public int instanceCount;
            public GraphicsBuffer instanceBuffer;
            public GraphicsBuffer visibleBuffer;
            public GraphicsBuffer argsBuffer;
            public MaterialPropertyBlock mpb;
        }

        struct GpuGrassInstance
        {
            public Vector3 position;
            public Vector4 rotation;
            public float scale;
        }

        List<MeshGroup> groups;
        int cullKernel;
        Bounds renderBounds;
        Vector4[] frustumPlaneCache = new Vector4[6];
        Plane[] planeCache = new Plane[6];

        static readonly int AllInstancesId = Shader.PropertyToID("_AllInstances");
        static readonly int VisibleIndicesId = Shader.PropertyToID("_VisibleIndices");
        static readonly int FrustumPlanesId = Shader.PropertyToID("_FrustumPlanes");
        static readonly int CameraPositionId = Shader.PropertyToID("_CameraPosition");
        static readonly int MaxDistanceId = Shader.PropertyToID("_MaxDistance");
        static readonly int DensityFalloffStartId = Shader.PropertyToID("_DensityFalloffStart");
        static readonly int InstanceCountId = Shader.PropertyToID("_InstanceCount");

        const int THREAD_GROUP_SIZE = 128;

        void OnEnable()
        {
            if (instanceData == null || instanceData.instances == null || instanceData.instances.Length == 0)
            {
                Debug.LogWarning("[GrassRenderer] No instance data assigned.");
                enabled = false;
                return;
            }

            if (cullingShader == null)
            {
                Debug.LogWarning("[GrassRenderer] No culling compute shader assigned.");
                enabled = false;
                return;
            }

            ConfigureFromBakedData();

            if (meshVariants == null || meshVariants.Length == 0)
            {
                Debug.LogWarning("[GrassRenderer] No mesh variants available. Re-bake with updated baker.");
                enabled = false;
                return;
            }

            ValidateConfiguration();

            cullKernel = cullingShader.FindKernel("CullGrass");

            var padding = Vector3.one * maxRenderDistance;
            renderBounds = new Bounds(instanceData.worldBounds.center, instanceData.worldBounds.size + padding * 2f);

            BuildGroups();
        }

        void ConfigureFromBakedData()
        {
            if (instanceData.meshTypes == null || instanceData.meshTypes.Length == 0)
                return;

            bool needsRebuild = meshVariants == null || meshVariants.Length != instanceData.meshTypes.Length;

            if (!needsRebuild)
            {
                for (int i = 0; i < instanceData.meshTypes.Length; i++)
                {
                    var info = instanceData.meshTypes[i];
                    if (meshVariants[i].mesh != info.mesh || meshVariants[i].material != info.material)
                    {
                        needsRebuild = true;
                        break;
                    }
                }
            }

            if (!needsRebuild)
                return;

            meshVariants = new MeshVariant[instanceData.meshTypes.Length];
            for (int i = 0; i < instanceData.meshTypes.Length; i++)
            {
                meshVariants[i] = new MeshVariant
                {
                    mesh = instanceData.meshTypes[i].mesh,
                    material = instanceData.meshTypes[i].material
                };
            }
        }

        void ValidateConfiguration()
        {
            if (instanceData.meshTypes == null || instanceData.meshTypes.Length == 0)
            {
                Debug.LogWarning("[GrassRenderer] Instance data has no meshType metadata. Re-bake with updated baker.");
                return;
            }

            for (int i = 0; i < meshVariants.Length; i++)
            {
                if (meshVariants[i].mesh == null)
                    Debug.LogWarning($"[GrassRenderer] meshVariant[{i}] has null mesh.");
                if (meshVariants[i].material == null)
                    Debug.LogWarning($"[GrassRenderer] meshVariant[{i}] has null material.");
            }
        }

        void OnDisable()
        {
            ReleaseGroups();
        }

        void BuildGroups()
        {
            var grouped = new Dictionary<int, List<GpuGrassInstance>>();

            foreach (var entry in instanceData.instances)
            {
                if (entry.meshType < 0 || entry.meshType >= meshVariants.Length)
                    continue;

                if (!grouped.TryGetValue(entry.meshType, out var list))
                {
                    list = new List<GpuGrassInstance>();
                    grouped[entry.meshType] = list;
                }

                list.Add(new GpuGrassInstance
                {
                    position = entry.position,
                    rotation = new Vector4(entry.rotation.x, entry.rotation.y, entry.rotation.z, entry.rotation.w),
                    scale = entry.scale
                });
            }

            groups = new List<MeshGroup>();

            foreach (var kvp in grouped)
            {
                int meshType = kvp.Key;
                var instances = kvp.Value;

                if (meshVariants[meshType].mesh == null || meshVariants[meshType].material == null)
                    continue;

                int count = instances.Count;
                int stride = sizeof(float) * 8; // 3 pos + 4 rot + 1 scale

                var instanceBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    count, stride);
                instanceBuffer.SetData(instances);

                var visibleBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Append,
                    count, sizeof(uint));

                var argsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1, GraphicsBuffer.IndirectDrawIndexedArgs.size);

                var args = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                args[0].indexCountPerInstance = meshVariants[meshType].mesh.GetIndexCount(0);
                args[0].startIndex = meshVariants[meshType].mesh.GetIndexStart(0);
                args[0].baseVertexIndex = meshVariants[meshType].mesh.GetBaseVertex(0);
                args[0].instanceCount = 0;
                args[0].startInstance = 0;
                argsBuffer.SetData(args);

                var mpb = new MaterialPropertyBlock();
                mpb.SetBuffer(AllInstancesId, instanceBuffer);
                mpb.SetBuffer(VisibleIndicesId, visibleBuffer);

                groups.Add(new MeshGroup
                {
                    meshType = meshType,
                    instanceCount = count,
                    instanceBuffer = instanceBuffer,
                    visibleBuffer = visibleBuffer,
                    argsBuffer = argsBuffer,
                    mpb = mpb
                });
            }
        }

        void ReleaseGroups()
        {
            if (groups == null) return;

            foreach (var g in groups)
            {
                g.instanceBuffer?.Release();
                g.visibleBuffer?.Release();
                g.argsBuffer?.Release();
            }

            groups = null;
        }

        public void RefreshGPUData()
        {
            ReleaseGroups();

            if (instanceData == null || instanceData.instances == null || instanceData.instances.Length == 0)
            {
                groups = null;
                return;
            }

            var padding = Vector3.one * maxRenderDistance;
            renderBounds = new Bounds(instanceData.worldBounds.center, instanceData.worldBounds.size + padding * 2f);

            BuildGroups();
        }

        void Update()
        {
            if (groups == null || groups.Count == 0) return;

            var cam = Camera.main;
            if (cam == null) return;

            ExtractFrustumPlanes(cam);

            foreach (var group in groups)
            {
                group.visibleBuffer.SetCounterValue(0);

                cullingShader.SetBuffer(cullKernel, AllInstancesId, group.instanceBuffer);
                cullingShader.SetBuffer(cullKernel, VisibleIndicesId, group.visibleBuffer);
                cullingShader.SetVectorArray(FrustumPlanesId, frustumPlaneCache);
                cullingShader.SetVector(CameraPositionId, cam.transform.position);
                cullingShader.SetFloat(MaxDistanceId, maxRenderDistance);
                cullingShader.SetFloat(DensityFalloffStartId, densityFalloffStart);
                cullingShader.SetInt(InstanceCountId, group.instanceCount);

                int threadGroups = (group.instanceCount + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
                cullingShader.Dispatch(cullKernel, threadGroups, 1, 1);

                GraphicsBuffer.CopyCount(group.visibleBuffer, group.argsBuffer,
                    sizeof(uint)); // instanceCount is at byte offset 4 (second field)

                var variant = meshVariants[group.meshType];

                var rp = new RenderParams(variant.material)
                {
                    worldBounds = renderBounds,
                    shadowCastingMode = shadowMode,
                    receiveShadows = receiveShadows,
                    matProps = group.mpb
                };

                Graphics.RenderMeshIndirect(in rp, variant.mesh, group.argsBuffer, 1);
            }
        }

        void ExtractFrustumPlanes(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, planeCache);
            for (int i = 0; i < 6; i++)
                frustumPlaneCache[i] = new Vector4(planeCache[i].normal.x, planeCache[i].normal.y, planeCache[i].normal.z, planeCache[i].distance);
        }

        public struct RenderStats
        {
            public int totalInstances;
            public int meshGroupCount;
            public int totalVertices;
            public int totalTriangles;
            public bool castsShadows;
        }

        public RenderStats GetStats()
        {
            var stats = new RenderStats();

            if (groups == null)
                return stats;

            stats.meshGroupCount = groups.Count;
            stats.castsShadows = shadowMode != ShadowCastingMode.Off;

            foreach (var group in groups)
            {
                stats.totalInstances += group.instanceCount;

                var mesh = meshVariants[group.meshType].mesh;
                if (mesh == null) continue;

                stats.totalTriangles += (int)(mesh.GetIndexCount(0) / 3) * group.instanceCount;
                stats.totalVertices += mesh.vertexCount * group.instanceCount;
            }

            return stats;
        }
    }
