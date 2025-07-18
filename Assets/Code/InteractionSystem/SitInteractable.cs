using UnityEngine;
using System.Collections;
using System.Linq;
using FishNet.Object;
using FishNet.Connection;
using Code.Player;
using FishNet.Component.Animating;

namespace Code.InteractionSystem
{
    public class SitInteractable : Interactable
    {
        [Header("Sit Settings")] [SerializeField]
        private Transform sitPoint;

        [SerializeField] private bool allowRotateCamera = true;
        [SerializeField] private bool useRightMouseButtonToRotate = false;

        const string SIT_TRIGGER = "TriggerSit";
        const string STAND_TRIGGER = "TriggerStand";
        const string SIT_STATE = "Sitting";
        const string STAND_STATE = "Movement";

        private bool _isSitting;
        private Coroutine _sitRoutine;
        private Vector3 _savedPos;
        private Quaternion _savedRot;

        private void Awake()
        {
            if (sitPoint == null)
                sitPoint = transform.Find("SitPoint");
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            
            _isSitting = false;
            _sitRoutine = null;
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            base.OnInteract(conn);
            TargetToggleSit(conn);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            base.OnEndInteract(conn);
            TargetToggleSit(conn);
        }

        [TargetRpc]
        private void TargetToggleSit(NetworkConnection conn)
        {
            var movement = FindObjectsOfType<PlayerMovementController>()
                .First(m => m.Owner.IsLocalClient);
            var cc = movement.GetComponent<CharacterController>();
            var anim = movement.GetComponent<Animator>();
            var networkAnimator = movement.GetComponent<NetworkAnimator>();
            var tf = movement.transform;

            if (_sitRoutine != null)
                StopCoroutine(_sitRoutine);

            _sitRoutine = StartCoroutine(
                _isSitting
                    ? StandUpFlow(movement, anim, networkAnimator, cc, tf)
                    : SitDownFlow(movement, anim, networkAnimator, cc, tf)
            );
        }

        private IEnumerator SitDownFlow(PlayerMovementController move, Animator anim, NetworkAnimator networkAnim,
            CharacterController cc, Transform tf)
        {
            IsBusy = true;
            move.CanMove = false;
            cc.enabled = false;
            anim.applyRootMotion = true;
            networkAnim.SetTrigger(SIT_TRIGGER);

            _savedPos = tf.position;
            _savedRot = tf.rotation;
            tf.position = sitPoint.position;
            tf.rotation = sitPoint.rotation;

            yield return new WaitUntil(() =>
                anim.GetCurrentAnimatorStateInfo(0).IsName(SIT_STATE)
            );

            // lock into chair
            //tf.SetParent(sitPoint, false);
            anim.applyRootMotion = false;
            _sitRoutine = null;

            _isSitting = true;
            if (allowRotateCamera)
            {
                move.LookCameraLimitRotation = true;

                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = true;
                    move.LockCursor = false;
                }
            }

            move.sitBaseYaw = move.cinemachineTargetYaw;
            move.sitBasePitch = move.cinemachineTargetPitch;
            IsBusy = false;
        }

        private IEnumerator StandUpFlow(PlayerMovementController move, Animator anim, NetworkAnimator networkAnim,
            CharacterController cc, Transform tf)
        {
            IsBusy = true;
            _isSitting = false;
            anim.applyRootMotion = true;
            networkAnim.SetTrigger(STAND_TRIGGER);

            yield return new WaitUntil(() =>
                anim.GetCurrentAnimatorStateInfo(0).IsName(STAND_STATE)
            );

            tf.position = _savedPos;
            tf.rotation = _savedRot;
            anim.applyRootMotion = false;
            cc.enabled = true;
            move.CanMove = true;

            if (allowRotateCamera)
            {
                move.LookCameraLimitRotation = false;

                if (useRightMouseButtonToRotate)
                {
                    move.LookCameraLimitRotationRKM = false;
                    move.LockCursor = true;
                }
            }

            // server-side release happens via base.OnEndInteract called earlier
            _sitRoutine = null;
            IsBusy = false;
        }
    }
}