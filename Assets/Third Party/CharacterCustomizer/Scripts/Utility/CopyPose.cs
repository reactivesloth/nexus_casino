using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace CC
{
    public class CopyPose : MonoBehaviour
    {
        private Transform[] SourceHierarchy;
        private Transform[] TargetHierarchy;
        public List<Transform> SourceBones = new List<Transform>();
        public List<Transform> TargetBones = new List<Transform>();

        private void Start()
        {
            //Get meshes
            var mainScript = GetComponentInParent<CharacterCustomization>();
            var sourceMesh = mainScript.MainMesh;
            var targetMeshes = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            //Copy bounds from character
            var LODGroup = GetComponentInChildren<LODGroup>();
            var mainLODGroup = mainScript.GetComponentInChildren<LODGroup>();
            if (LODGroup != null) { LODGroup.RecalculateBounds(); LODGroup.size = (mainLODGroup == null) ? 1.5f : mainLODGroup.size; }

            foreach (var mesh in targetMeshes)
            {
                mesh.localBounds = sourceMesh.localBounds;
            }

            //Get bone hierarchies
            SourceHierarchy = sourceMesh.rootBone.GetComponentsInChildren<Transform>();
            TargetHierarchy = GetRootBone(targetMeshes[0].rootBone).GetComponentsInChildren<Transform>();

            var targetBonesDict = TargetHierarchy.ToDictionary(t => t.name, t => t);

            //Only copy bones that are found in both hierarchies, also ensures order is the same
            foreach (Transform child in SourceHierarchy)
            {
                //Check if a bone with the same name exists in the target hierarchy using the dictionary
                if (targetBonesDict.TryGetValue(child.name, out var targetBone))
                {
                    SourceBones.Add(child);
                    TargetBones.Add(targetBone);
                }
            }
        }

        private Transform GetRootBone(Transform bone)
        {
            if (bone.parent == transform) return bone;
            return GetRootBone(bone.parent);
        }

        private void LateUpdate()
        {
            //Copy bone transform
            for (int i = 0; i < SourceBones.Count; i++)
            {
                TargetBones[i].localPosition = SourceBones[i].localPosition;
                TargetBones[i].localRotation = SourceBones[i].localRotation;
                TargetBones[i].localScale = SourceBones[i].localScale;
            }
        }
    }
}