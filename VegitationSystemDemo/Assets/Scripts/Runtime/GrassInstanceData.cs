using System;
using UnityEngine;

namespace DaisyParty.Rendering
{
    [CreateAssetMenu(menuName = "DaisyParty/Grass Instance Data")]
    public class GrassInstanceData : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public Vector3 position;
            public Quaternion rotation;
            public float scale;
            public int meshType;
        }

        [Serializable]
        public struct MeshTypeInfo
        {
            public string name;
            public Mesh mesh;
            public Material material;
        }

        public Entry[] instances;
        public Bounds worldBounds;
        public MeshTypeInfo[] meshTypes;
    }
}
