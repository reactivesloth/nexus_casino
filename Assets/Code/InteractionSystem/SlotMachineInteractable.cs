using UnityEngine;
using FishNet.Object;
using FishNet.Connection;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")]
        [SerializeField, Tooltip("Drag сюда ваш Canvas (может быть Screen-Space или World-Space)")]
        private Canvas computerCanvas;

        private bool _isUsing = false;

        public override string InteractionPrompt =>
            !_isUsing ? "Use Computer" : "Exit Computer";

        private void Reset()
        {
            // если случайно не назначили в инспекторе — пытаемся найти
            if (computerCanvas == null)
                computerCanvas = GetComponentInChildren<Canvas>(true);
        }

        private void Start()
        {
            // при старте Canvas всегда должен быть скрыт
            if (computerCanvas != null)
                computerCanvas.gameObject.SetActive(false);
        }

        protected override void OnInteract(NetworkConnection conn)
        {
            if (_isUsing) return;

            base.OnInteract(conn);
            _isUsing = true;
            TargetToggleComputerUI(conn, true);
        }

        protected override void OnEndInteract(NetworkConnection conn)
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