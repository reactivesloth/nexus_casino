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

        private void Update()
        {
            if (_isUsing)
            {
                //тут нужно взять сначала текстуру с канваса, либо с WebView
                //https://developer.vuplex.com/webview/IWebView#GetRawTextureData
                //https://support.vuplex.com/articles/how-to-use-standard-material
                
                //разделяем логику приема передачи
                
                // на получаетеле задаем текстуру на "монитор"
                var tex = new Texture2D(1, 1);
                computerMeshRenderer.materials[materialIndex].SetTexture("_BaseMap", tex);
                // непонятно почему, но текстура не применяется на материале (даже если назначается, то материал в сцене не меняется
            }
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