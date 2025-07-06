using UnityEngine;
using System.Collections;
using System.Linq;
using FishNet.Object;
using FishNet.Connection;
using Code.Player; // Namespace containing PlayerMovementController

namespace Code.InteractionSystem
{
    /// <summary>
    /// Abstract base class for all networked interactable objects with simple occupancy.
    /// </summary>
    
    public class SitInteractable : Interactable
    {
        [Header("Sit Settings")]
        [SerializeField, Tooltip("Speed of movement into sit position.")] private float moveSpeed = 2f;
        [SerializeField, Tooltip("Delay before standing up.")] private float standUpDelay = 0.5f;

        [Header("Interaction Settings")]
        [SerializeField, Tooltip("Transform specifying where the player should sit.")] private Transform sitPoint;

        // Client side state
        private bool _isSitting = false;
        private Vector3 _origPos;
        private Quaternion _origRot;
        private Coroutine _routine;

        //public override string InteractionPrompt => !_isSitting && !IsOccupied ? "Sit" : _isSitting ? "Stand Up" : "Occupied";

        private void Awake()
        {
            if (sitPoint == null)
                sitPoint = transform.Find("SitPoint");
        }

        protected override void OnInteract(NetworkConnection conn)
        {
            TargetToggleSit(conn);
        }

        protected override void OnEndInteract(NetworkConnection conn)
        {
            TargetToggleSit(conn);
        }
        
        [TargetRpc]
        private void TargetToggleSit(NetworkConnection connection)
        {
            // Retrieve the local player's movement controller by ownership
            var movement = FindObjectsOfType<PlayerMovementController>()
                .First(m => m.Owner.IsLocalClient);
            var controller = movement.GetComponent<CharacterController>();
            var animator = movement.GetComponent<Animator>();
            var playerTf = movement.transform;

            if (_routine != null)
                StopCoroutine(_routine);

            if (_isSitting)
                _routine = StartCoroutine(StandUp(controller, movement, animator, playerTf));
            else
                _routine = StartCoroutine(SitDown(controller, movement, animator, playerTf));
        }

        private IEnumerator SitDown(CharacterController controller, PlayerMovementController movement,
            Animator animator, Transform playerTf)
        {
            _isSitting = true;
            _origPos = playerTf.position;
            _origRot = playerTf.rotation;
            controller.enabled = false;
            movement.CanMove = false;

            float t = 0f;
            Vector3 start = _origPos;
            Quaternion rotStart = _origRot;
            Vector3 end = sitPoint.position;
            Quaternion rotEnd = sitPoint.rotation;

            while (t < 1f)
            {
                t += Time.deltaTime * moveSpeed;
                playerTf.position = Vector3.Lerp(start, end, t);
                playerTf.rotation = Quaternion.Slerp(rotStart, rotEnd, t);
                yield return null;
            }

            animator.SetBool("IsSitting", true);
        }

        private IEnumerator StandUp(CharacterController controller, PlayerMovementController movement,
            Animator animator, Transform playerTf)
        {
            animator.SetBool("IsSitting", false);
            yield return new WaitForSeconds(standUpDelay);

            playerTf.position = _origPos;
            playerTf.rotation = _origRot;
            controller.enabled = true;
            movement.CanMove = true;
            _isSitting = false;
            _routine = null;

            // Release occupancy on server
            ReleaseInteractable();
        }
    }
}