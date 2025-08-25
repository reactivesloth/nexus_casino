using System.Linq;
using Code.Network;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")] [SerializeField] private Canvas computer3dCanvas;

        [SerializeField] private Canvas contentCanvas;
        [SerializeField] private TextMeshPro idNumberText;

        [Header("Streaming")] [SerializeField] private NetworkImageStream networkImageStream;

        public int IDNumber;
        public bool IsUsing => _isUsing;

        private bool _isUsing;
        private bool _wasStarted;

        public NetworkImageStream NetworkImageStream => networkImageStream;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (networkImageStream == null) networkImageStream = GetComponentInChildren<NetworkImageStream>(true);
        }
#endif

        private void Awake()
        {
            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);
            if (contentCanvas) contentCanvas.gameObject.SetActive(false);
        }

        private void Start()
        {
            _wasStarted = true;
        }

        public override string InteractionPrompt => !_isUsing ? "Use Computer" : "Exit Computer";

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isUsing = false;
        }

        protected internal override void OnInteract(NetworkConnection conn, bool force)
        {
            if (_isUsing) return;
            base.OnInteract(conn, force);
            _isUsing = true;
            TargetToggleComputerUI(conn, true);
            ObserverActivation(true);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            if (!_isUsing) return;
            base.OnEndInteract(conn);
            _isUsing = false;
            TargetToggleComputerUI(conn, false);
            ObserverActivation(false);
        }

        public void SwitchFS()
        {
            var newFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 0;
            PlayerPrefs.SetInt("PlayerSlotMachineIsFullscreen", newFS ? 1 : 0);
            PlayerPrefs.Save();

            if (newFS)
            {
                WebViewManager.Instance.OpenFullscreen();
                if (PlayerInput.Instance != null) PlayerInput.Instance.IsBusy = true;

                if (networkImageStream != null && WebViewManager.Instance.WebViewRawImage != null)
                    networkImageStream.SetTexture(WebViewManager.Instance.WebViewRawImage);
            }
            else
            {
                if (computer3dCanvas && !computer3dCanvas.gameObject.activeSelf)
                    computer3dCanvas.gameObject.SetActive(true);

                var raw = WebViewManager.Instance.ShowWorldView(IDNumber, computer3dCanvas);
                if (networkImageStream != null)
                    networkImageStream.SetTexture(raw);

                if (PlayerInput.Instance != null) PlayerInput.Instance.IsBusy = false;
            }
        }

        [TargetRpc]
        private void TargetToggleComputerUI(NetworkConnection conn, bool open)
        {
            if (!_wasStarted) return;

            bool useFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 1;

            if (contentCanvas) contentCanvas.gameObject.SetActive(open);

            if (!open)
            {
                if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);
                if (networkImageStream != null) networkImageStream.ClearTexture();

                WebViewManager.Instance.HideWorldView(IDNumber);
                WebViewManager.Instance.Hide();

                if (PlayerInput.Instance != null)
                {
                    PlayerInput.Instance.HideMobileFallback = false;
                    PlayerInput.Instance.IsBusy = false;
                }

                return;
            }

            if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;

            if (useFS)
            {
                WebViewManager.Instance.OpenFullscreen();

                if (networkImageStream != null && WebViewManager.Instance.WebViewRawImage != null)
                    networkImageStream.SetTexture(WebViewManager.Instance.WebViewRawImage);
            }
            else
            {
                if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(true);

                var raw = WebViewManager.Instance.ShowWorldView(IDNumber, computer3dCanvas);
                if (networkImageStream != null)
                    networkImageStream.SetTexture(raw);
            }
        }

        [ObserversRpc(BufferLast = true)]
        private void ObserverActivation(bool open)
        {
            if (contentCanvas != null) contentCanvas.gameObject.SetActive(open);
        }

        public static SlotMachineInteractable FindById(int id)
        {
            var all = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(s => s.IDNumber == id);
        }
    }
}