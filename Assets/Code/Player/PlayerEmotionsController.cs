using System;
using FishNet.Object;
using UnityEngine;

namespace Code.Player
{
    public class PlayerEmotionsController : NetworkBehaviour {
        
        private PlayerMovementController playerMovementController;

        private void Awake()
        {
            playerMovementController = gameObject.GetComponent<PlayerMovementController>();
        }

        private void Update()
        {
            if (!IsOwner)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha0)) playerMovementController.PlayEmotionAnimation(0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) playerMovementController.PlayEmotionAnimation(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) playerMovementController.PlayEmotionAnimation(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) playerMovementController.PlayEmotionAnimation(3);
            if (Input.GetKeyDown(KeyCode.Alpha4)) playerMovementController.PlayEmotionAnimation(4);
            if (Input.GetKeyDown(KeyCode.Alpha5)) playerMovementController.PlayEmotionAnimation(5);
            if (Input.GetKeyDown(KeyCode.Alpha6)) playerMovementController.PlayEmotionAnimation(6);
            if (Input.GetKeyDown(KeyCode.Alpha7)) playerMovementController.PlayEmotionAnimation(7);
            if (Input.GetKeyDown(KeyCode.Alpha8)) playerMovementController.PlayEmotionAnimation(8);
            if (Input.GetKeyDown(KeyCode.Alpha9)) playerMovementController.PlayEmotionAnimation(9);
            if (Input.GetKeyDown(KeyCode.LeftBracket)) playerMovementController.PlayEmotionAnimation(10);
            if (Input.GetKeyDown(KeyCode.RightBracket)) playerMovementController.PlayEmotionAnimation(11);
            if (Input.GetKeyDown(KeyCode.Backslash)) playerMovementController.PlayEmotionAnimation(12);
            if (Input.GetKeyDown(KeyCode.Colon)) playerMovementController.PlayEmotionAnimation(13);
            if (Input.GetKeyDown(KeyCode.Quote)) playerMovementController.PlayEmotionAnimation(14);
        }
    }
}
