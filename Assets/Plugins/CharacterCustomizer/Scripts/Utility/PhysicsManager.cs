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
        public bool customizing; //If customizing, colliders should be activated for hover detection

        private void Awake()
        {
            if (animator == null) animator = gameObject.GetComponent<Animator>();
            if (capsule == null) capsule = gameObject.GetComponent<CapsuleCollider>();
            rigidBodies = gameObject.GetComponentsInChildren<Rigidbody>();
            colliders = gameObject.GetComponentsInChildren<Collider>();
            modifyBones = gameObject.GetComponentsInChildren<ModifyBone>();

            foreach (var item in rigidBodies)
            {
                item.useGravity = useGravity;
                item.isKinematic = true;
            }
        }

        private void Start()
        {
            if (ragdolling) StartCoroutine(ragdoll(true));
        }

        public void customizationSetup()
        {
            //Create head rig
            var headRig = GetComponentInChildren<HeadColliders>();
            if (headRig != null)
            {
                headRig.createColliders();
            }

            //Enable colliders
            foreach (var item in colliders)
            {
                item.enabled = true;
            }

            if (capsule != null) capsule.enabled = false;

            customizing = true;
        }

        public IEnumerator ragdoll(bool shouldRagdoll)
        {
            ragdolling = shouldRagdoll;

            //Enable colliders when ragdolling or customizing
            foreach (var item in colliders)
            {
                item.enabled = ragdolling || customizing;
            }

            //Notify modifyBone scripts
            foreach (var item in modifyBones)
            {
                item.onSimulate(ragdolling);
            }

            if (ragdolling) yield return new WaitForFixedUpdate();

            //Disable capsule when ragdolling or customizing
            if (capsule != null) capsule.enabled = !ragdolling && !customizing;

            //Enable physics
            foreach (var item in rigidBodies)
            {
                item.angularVelocity = Vector3.zero;
                item.isKinematic = !ragdolling;
            }

            //Disable animator when ragdolling
            if (animator != null) animator.enabled = !ragdolling;

            yield break;
        }
    }
}