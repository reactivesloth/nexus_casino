using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public class CopyPose : MonoBehaviour
    {
        private Transform[] _sourceHierarchy;
        private Transform[] _targetHierarchy;
        public readonly List<Transform> SourceBones = new List<Transform>(128);
        public readonly List<Transform> TargetBones = new List<Transform>(128);

        private void Start()
        {
            var customizer = GetComponentInParent<CharacterCustomization>();
            if (customizer == null || customizer.MainMesh == null)
            {
                Debug.LogWarning("CopyPose: CharacterCustomization or MainMesh is missing.");
                enabled = false; return;
            }

            var sourceMesh = customizer.MainMesh;
            var targetMeshes = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (targetMeshes == null || targetMeshes.Length == 0)
            {
                Debug.LogWarning("CopyPose: No SkinnedMeshRenderer found under this object.");
                enabled = false; return;
            }

            // copy bounds from character
            var srcBounds = sourceMesh.localBounds;
            for (int i = 0; i < targetMeshes.Length; i++)
            {
                var mesh = targetMeshes[i];
                if (mesh != null) mesh.localBounds = srcBounds;
            }

            var lodGroup = GetComponent<LODGroup>();
            if (lodGroup != null)
            {
                lodGroup.RecalculateBounds();
                lodGroup.size = 0.5f;
            }

            // hierarchies
            if (sourceMesh.rootBone == null)
            {
                Debug.LogWarning("CopyPose: sourceMesh.rootBone is null");
                enabled = false; return;
            }

            _sourceHierarchy = sourceMesh.rootBone.GetComponentsInChildren<Transform>(true);

            var targetRoot = GetRootBone(targetMeshes[0].rootBone);
            if (targetRoot == null)
            {
                Debug.LogWarning("CopyPose: target root bone is null");
                enabled = false; return;
            }
            _targetHierarchy = targetRoot.GetComponentsInChildren<Transform>(true);

            // build dictionary without LINQ
            var targetByName = new Dictionary<string, Transform>(_targetHierarchy.Length);
            for (int i = 0; i < _targetHierarchy.Length; i++)
            {
                var t = _targetHierarchy[i];
                if (t == null) continue;
                if (!targetByName.ContainsKey(t.name))
                    targetByName.Add(t.name, t);
            }

            // Only copy bones that are present in both
            for (int i = 0; i < _sourceHierarchy.Length; i++)
            {
                var src = _sourceHierarchy[i];
                if (src == null) continue;
                if (targetByName.TryGetValue(src.name, out var tgt))
                {
                    SourceBones.Add(src);
                    TargetBones.Add(tgt);
                }
            }
        }

        private Transform GetRootBone(Transform bone)
        {
            if (bone == null) return null;
            var parent = bone.parent;
            if (parent == transform || parent == null) return bone;
            return GetRootBone(parent);
        }

        private void LateUpdate()
        {
            int count = SourceBones.Count;
            for (int i = 0; i < count; i++)
            {
                var s = SourceBones[i];
                var t = TargetBones[i];
                if (s == null || t == null) continue;
                t.localPosition = s.localPosition;
                t.localRotation = s.localRotation;
                t.localScale = s.localScale;
            }
        }
    }
}