using System;
using Code.Network.Player;
using Code.Player;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network.PlayerSitInteraction
{
    public class SitUI : NetworkBehaviour
    {
        public TextMeshProUGUI hintUI;
        public float detectDistance = 2f;
        public LayerMask benchLayer;

        private Transform targetSitPoint;
        private PlayerMovementController playerController;

        private void Awake()
        {
            playerController = GetComponent<PlayerMovementController>();
            if (hintUI == null)
            {
                hintUI = GameObject.Find("HintUI").GetComponent<TextMeshProUGUI>();
            }
        }
        
        private void Update()
        {
            if(!IsOwner) return;
            
            var distance = playerController.FirstPersonView ? detectDistance : detectDistance * 5;
            
            Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, distance, benchLayer))
            {
                Transform sitPoint = hit.collider.transform.Find("SitPoint");
                if (sitPoint != null)
                {
                    targetSitPoint = sitPoint;
                    hintUI.text = "Нажмите Е, чтобы взаимодействовать";
                    return;
                }
            }

            targetSitPoint = null;
            hintUI.text = string.Empty;
        }

        public Transform GetTargetSitPoint() => targetSitPoint;
    }
}