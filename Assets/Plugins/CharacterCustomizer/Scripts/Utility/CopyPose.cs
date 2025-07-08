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
            var sourceMesh = GetComponentInParent<CharacterCustomization>().MainMesh;
            var targetMeshes = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            //Copy bounds from character
            foreach (var mesh in targetMeshes)
            {
                mesh.localBounds = sourceMesh.localBounds;
            }

            //Recalculate bounds for LOD transition
            var lodGroup = GetComponent<LODGroup>();
            if (lodGroup != null) { lodGroup.RecalculateBounds(); lodGroup.size = 0.5f; }

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