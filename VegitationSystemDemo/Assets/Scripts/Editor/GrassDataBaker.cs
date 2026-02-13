using System.Collections.Generic;
using System.Linq;
using DaisyParty.Rendering;
using UnityEditor;
using UnityEngine;

public class GrassDataBaker : EditorWindow
{
    Transform grassContainer;
    GrassInstanceData existingData;
    GrassRenderer grassRenderer;
    Material grassIndirectMaterial;
    Material flowerIndirectMaterial;

    [MenuItem("Tools/Grass Data Baker")]
    static void ShowWindow()
    {
        GetWindow<GrassDataBaker>("Grass Data Baker");
    }

    void OnGUI()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Grass Data Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        grassContainer = (Transform)EditorGUILayout.ObjectField(
            "Grass Container", grassContainer, typeof(Transform), true);

        grassRenderer = (GrassRenderer)EditorGUILayout.ObjectField(
            "Grass Renderer", grassRenderer, typeof(GrassRenderer), true);

        existingData = (GrassInstanceData)EditorGUILayout.ObjectField(
            "Existing Data", existingData, typeof(GrassInstanceData), false);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Indirect Materials", EditorStyles.boldLabel);

        grassIndirectMaterial = (Material)EditorGUILayout.ObjectField(
            "Grass Indirect", grassIndirectMaterial, typeof(Material), false);

        flowerIndirectMaterial = (Material)EditorGUILayout.ObjectField(
            "Flower Indirect", flowerIndirectMaterial, typeof(Material), false);

        EditorGUILayout.Space(10);

        if (grassContainer == null)
        {
            EditorGUILayout.HelpBox("Assign the container that holds all grass GameObjects.", MessageType.Info);
            return;
        }

        int childCount = grassContainer.childCount;
        EditorGUILayout.LabelField($"Children found: {childCount}");

        EditorGUILayout.Space(10);

        bool canBake = grassIndirectMaterial != null && flowerIndirectMaterial != null;
        EditorGUI.BeginDisabledGroup(!canBake);
        if (GUILayout.Button("Bake Grass Data", GUILayout.Height(30)))
            Bake();
        EditorGUI.EndDisabledGroup();

        if (!canBake)
            EditorGUILayout.HelpBox("Assign both indirect materials before baking.", MessageType.Warning);

        EditorGUILayout.Space(5);

        EditorGUI.BeginDisabledGroup(existingData == null);
        if (GUILayout.Button("Unbake (Re-enable GameObjects)", GUILayout.Height(25)))
            Unbake();
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "Bake: Extracts transforms from grass GameObjects into a data asset, then deactivates them.\n" +
            "Unbake: Re-enables the GameObjects for brush editing.",
            MessageType.Info);
    }

    struct CollectedMeshType
    {
        public string name;
        public Mesh mesh;
        public Material originalMaterial;
    }

    void Bake()
    {
        var meshTypeMap = new Dictionary<string, CollectedMeshType>();
        var rawEntries = new List<(Vector3 pos, Quaternion rot, float scale, string typeName)>();

        var boundsMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var boundsMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        int skipped = 0;

        for (int i = 0; i < grassContainer.childCount; i++)
        {
            var child = grassContainer.GetChild(i);
            var mf = child.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                skipped++;
                continue;
            }

            string meshName = GetMeshTypeName(child.gameObject, mf);

            if (!meshTypeMap.ContainsKey(meshName))
            {
                var mr = child.GetComponent<MeshRenderer>();
                meshTypeMap[meshName] = new CollectedMeshType
                {
                    name = meshName,
                    mesh = mf.sharedMesh,
                    originalMaterial = mr != null ? mr.sharedMaterial : null
                };
            }

            var pos = child.position;
            rawEntries.Add((pos, child.rotation, child.lossyScale.x, meshName));

            boundsMin = Vector3.Min(boundsMin, pos);
            boundsMax = Vector3.Max(boundsMax, pos);
        }

        if (rawEntries.Count == 0)
        {
            EditorUtility.DisplayDialog("Grass Baker", "No valid grass meshes found under the container.", "OK");
            return;
        }

        var sortedTypes = meshTypeMap.Values.OrderBy(t => t.name).ToList();
        var nameToIndex = new Dictionary<string, int>();
        for (int i = 0; i < sortedTypes.Count; i++)
            nameToIndex[sortedTypes[i].name] = i;

        var entries = new GrassInstanceData.Entry[rawEntries.Count];
        for (int i = 0; i < rawEntries.Count; i++)
        {
            var raw = rawEntries[i];
            entries[i] = new GrassInstanceData.Entry
            {
                position = raw.pos,
                rotation = raw.rot,
                scale = raw.scale,
                meshType = nameToIndex[raw.typeName]
            };
        }

        var meshTypes = sortedTypes.Select(t => new GrassInstanceData.MeshTypeInfo
        {
            name = t.name,
            mesh = t.mesh,
            material = ResolveIndirectMaterial(t.originalMaterial)
        }).ToArray();

        var center = (boundsMin + boundsMax) * 0.5f;
        var size = boundsMax - boundsMin + Vector3.one * 2f;

        GrassInstanceData data = existingData;
        bool creatingNew = data == null;

        if (creatingNew)
        {
            data = CreateInstance<GrassInstanceData>();
        }
        else
        {
            Undo.RecordObject(data, "Bake Grass Data");
        }

        data.instances = entries;
        data.worldBounds = new Bounds(center, size);
        data.meshTypes = meshTypes;

        if (creatingNew)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Grass Instance Data", "GrassInstanceData", "asset",
                "Choose where to save the grass data asset");

            if (string.IsNullOrEmpty(path))
                return;

            AssetDatabase.CreateAsset(data, path);
        }
        else
        {
            EditorUtility.SetDirty(data);
        }

        AssetDatabase.SaveAssets();
        existingData = data;

        Undo.SetCurrentGroupName("Bake Grass Data");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < grassContainer.childCount; i++)
        {
            var child = grassContainer.GetChild(i).gameObject;
            Undo.RecordObject(child, "Deactivate Grass");
            child.SetActive(false);
        }

        Undo.CollapseUndoOperations(undoGroup);

        var typeSummary = string.Join("\n", meshTypes.Select((t, idx) =>
        {
            int count = entries.Count(e => e.meshType == idx);
            return $"  [{idx}] {t.name}: {count} instances → {t.material.name}";
        }));

        Debug.Log($"[GrassDataBaker] Baked {entries.Length} instances across {meshTypes.Length} mesh types. Skipped {skipped}.\n{typeSummary}");

        if (grassRenderer != null)
        {
            Undo.RecordObject(grassRenderer, "Configure Grass Renderer");
            grassRenderer.instanceData = data;
            grassRenderer.meshVariants = meshTypes.Select(t => new GrassRenderer.MeshVariant
            {
                mesh = t.mesh,
                material = t.material
            }).ToArray();
            EditorUtility.SetDirty(grassRenderer);
        }
    }

    Material ResolveIndirectMaterial(Material original)
    {
        if (original == null)
            return grassIndirectMaterial;

        var tex = original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : null;
        if (tex == null && original.HasProperty("_MainTex"))
            tex = original.GetTexture("_MainTex");

        return tex != null ? flowerIndirectMaterial : grassIndirectMaterial;
    }

    void Unbake()
    {
        if (grassContainer == null) return;

        Undo.SetCurrentGroupName("Unbake Grass Data");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < grassContainer.childCount; i++)
        {
            var child = grassContainer.GetChild(i).gameObject;
            Undo.RecordObject(child, "Reactivate Grass");
            child.SetActive(true);
        }

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"[GrassDataBaker] Unbaked — re-enabled {grassContainer.childCount} GameObjects.");
    }

    string GetMeshTypeName(GameObject go, MeshFilter mf)
    {
        var prefabSource = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (prefabSource != null)
            return prefabSource.name;

        return mf.sharedMesh.name;
    }
}
