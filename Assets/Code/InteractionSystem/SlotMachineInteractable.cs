using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using UnityEngine.UI;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")]
        [SerializeField, Tooltip("Drag сюда ваш Canvas (может быть Screen-Space или World-Space)")]
        private Canvas computerCanvas;
        private bool _isUsing = false;

        [Header("View Settings")]
        [SerializeField] private MeshRenderer computerMeshRenderer;
        [SerializeField] private int materialIndex = 0;
        
        private Material _material;
        
        public override string InteractionPrompt =>
            !_isUsing ? "Use Computer" : "Exit Computer";
        
        private void Start() {
            
            if (computerCanvas != null)
                computerCanvas.gameObject.SetActive(false);
        }
        
        private void Reset()
        {
            if (computerCanvas == null)
                computerCanvas = GetComponentInChildren<Canvas>(true);
        }
        
        protected internal override void OnInteract(NetworkConnection conn)
        {
            if (_isUsing) return;

            base.OnInteract(conn);
            _isUsing = true;
            TargetToggleComputerUI(conn, true);
        }
        
        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            if (!_isUsing) return;

            base.OnEndInteract(conn);
            _isUsing = false;
            TargetToggleComputerUI(conn, false);
        }

        [TargetRpc]
        private void TargetToggleComputerUI(NetworkConnection conn, bool open)
        {
            Debug.Log($"[ComputerInteractable] TargetToggleComputerUI called -> open={open}");
            if (computerCanvas == null)
            {
                Debug.LogError("[ComputerInteractable] computerCanvas is NULL! Assign it in Inspector or as a child.");
                return;
            }

            computerCanvas.gameObject.SetActive(open);
        }
    }
}