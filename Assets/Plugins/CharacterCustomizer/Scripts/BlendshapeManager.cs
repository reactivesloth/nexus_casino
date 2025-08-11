using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public class BlendshapeManager : MonoBehaviour
    {
        public readonly Dictionary<string, int> NameToIndex = new Dictionary<string, int>(128);
        private SkinnedMeshRenderer mesh;

        public void parseBlendshapes()
        {
            if (mesh == null) mesh = GetComponent<SkinnedMeshRenderer>();
            if (mesh == null || mesh.sharedMesh == null)
            {
                Debug.LogWarning("BlendshapeManager: SkinnedMeshRenderer or sharedMesh is missing.");
                return;
            }

            var sm = mesh.sharedMesh;
            int count = sm.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string name = sm.GetBlendShapeName(i);
                if (string.IsNullOrEmpty(name)) continue;
                int lastDot = name.LastIndexOf('.') + 1;
                if (lastDot < 0 || lastDot >= name.Length) lastDot = 0;
                string key = name.Substring(lastDot);
                if (!NameToIndex.ContainsKey(key))
                    NameToIndex.Add(key, i);
            }
        }

        public void setBlendshape(string name, float value)
        {
            if (mesh == null) mesh = GetComponent<SkinnedMeshRenderer>();
            if (mesh == null) return;
            if (NameToIndex.Count == 0) parseBlendshapes();

            if (NameToIndex.TryGetValue(name, out int idx))
                mesh.SetBlendShapeWeight(idx, value * 100f);
        }
    }
}