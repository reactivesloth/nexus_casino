using UnityEngine;
using System.Collections;
using Code.Network.Player;
using FishNet.Object;

namespace Code.Network.PlayerSitInteraction
{
    public class SitController : NetworkBehaviour
    {
        public float moveSpeed = 2f;
        public float rotateSpeed = 5f;
        public KeyCode sitKey = KeyCode.E;

        private SitUI ui;
        private Transform currentSitPoint;
        private CharacterController controller;
        private PlayerMovementController movementController;
        private Animator animator;
        private bool isSitting = false;

        private Vector3 savedPos;
        
        void Start()
        {
            controller = GetComponent<CharacterController>();
            movementController = GetComponent<PlayerMovementController>();
            animator = GetComponent<Animator>();
            ui = FindObjectOfType<SitUI>();
            
        }

        void Update()
        {
            if(!IsOwner) return;
            
            if (isSitting)
            {
                if (Input.GetKeyDown(sitKey) || Input.GetKey(KeyCode.W))
                    StartCoroutine(SmoothStandUp());
                return;
            }

            Transform sitPoint = ui.GetTargetSitPoint();

            if (sitPoint != null && Input.GetKeyDown(sitKey))
                StartCoroutine(SmoothSitDown(sitPoint));
        }

        IEnumerator SmoothSitDown(Transform point)
        {
            controller.enabled = false;
            movementController.CanMove = false;
            isSitting = true;

            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            Vector3 targetPos = point.position;
            Quaternion targetRot = point.rotation;

            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime * moveSpeed;
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            currentSitPoint = point;
            savedPos = startPos;

            if (animator != null)
                animator.SetBool("IsSitting", true);
        }

        IEnumerator SmoothStandUp()
        {
            isSitting = false;

            if (animator != null)
                animator.SetBool("IsSitting", false);
            
            yield return new WaitForSeconds(3);

            // Vector3 startPos = transform.position;
            // float t = 0;
            // while (t < 1)
            // {
            //     t += Time.deltaTime * moveSpeed;
            //     transform.position = Vector3.Lerp(startPos, savedPos, t);
            //     yield return null;
            // }
            
            transform.position += transform.forward * 0.5f;
            controller.enabled = true;
            movementController.CanMove = true;
        }

        public bool IsSitting() => isSitting;
    }
}