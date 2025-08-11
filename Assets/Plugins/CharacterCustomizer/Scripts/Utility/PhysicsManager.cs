using System.Collections;
using UnityEngine;

namespace CC
{
    [DefaultExecutionOrder(100)]
    public class PhysicsManager : MonoBehaviour
    {
        public Animator animator;
        public CapsuleCollider capsule;
        private Rigidbody[] rigidBodies;
        private Collider[] colliders;
        private ModifyBone[] modifyBones;

        public bool useGravity;
        public bool ragdolling;
        public bool customizing;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (capsule == null) capsule = GetComponent<CapsuleCollider>();
            rigidBodies = GetComponentsInChildren<Rigidbody>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            modifyBones = GetComponentsInChildren<ModifyBone>(true);

            for (int i = 0; i < rigidBodies.Length; i++)
            {
                var rb = rigidBodies[i];
                if (rb == null) continue;
                rb.useGravity = useGravity;
                rb.isKinematic = true;
            }
        }

        private void Start()
        {
            if (ragdolling) StartCoroutine(ragdoll(true));
        }

        public void customizationSetup()
        {
            var headRig = GetComponentInChildren<HeadColliders>(true);
            if (headRig != null) headRig.createColliders();

            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c != null) c.enabled = true;
            }

            if (capsule != null) capsule.enabled = false;
            customizing = true;
        }

        public IEnumerator ragdoll(bool shouldRagdoll)
        {
            ragdolling = shouldRagdoll;

            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c != null) c.enabled = ragdolling || customizing;
            }

            for (int i = 0; i < modifyBones.Length; i++)
            {
                var mb = modifyBones[i];
                if (mb != null) mb.onSimulate(ragdolling);
            }

            if (ragdolling) yield return new WaitForFixedUpdate();

            if (capsule != null) capsule.enabled = !ragdolling && !customizing;

            for (int i = 0; i < rigidBodies.Length; i++)
            {
                var rb = rigidBodies[i];
                if (rb == null) continue;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = !ragdolling;
            }

            if (animator != null) animator.enabled = !ragdolling;
        }
    }
}