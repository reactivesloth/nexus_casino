using System.Linq;
using Code.Network;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")]
        [SerializeField] private Canvas computer3dCanvas;
        [SerializeField] private Canvas contentCanvas;
        [SerializeField] private TextMeshPro idNumberText;

        [Header("Streaming")]
        [SerializeField] private NetworkImageStream networkImageStream;
        public NetworkImageStream NetworkImageStream => networkImageStream;

        public int IDNumber;

        private readonly SyncVar<bool> _isUsingNet = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission  = ReadPermission.Observers
        });

        private bool _isUsingLocal;
        private bool _initSlot;
        private bool _wasStarted;

        private void EnsureInit()
        {
            if (_initSlot) return;
            _initSlot = true;

            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);
            if (contentCanvas) contentCanvas.gameObject.SetActive(false);

            if (networkImageStream == null)
                networkImageStream = GetComponentInChildren<NetworkImageStream>(true);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (networkImageStream == null) networkImageStream = GetComponentInChildren<NetworkImageStream>(true);
        }
#endif

        private void Awake() => EnsureInit();

        private void OnEnable()
        {
            EnsureInit();
            _isUsingNet.OnChange += OnIsUsingChanged;
        }

        private void OnDisable()
        {
            _isUsingNet.OnChange -= OnIsUsingChanged;
        }

        private void Start() => _wasStarted = true;

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureInit();
            ApplyComputerStateImmediate(_isUsingNet.Value);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isUsingLocal = false;
        }

        public override string InteractionPrompt => !_isUsingLocal ? "Use Computer" : "Exit Computer";

        protected internal override void OnInteract_Server(NetworkConnection conn, bool force)
        {
            base.OnInteract_Server(conn, force);

            if (IsOwner)
            {
                if (_isUsingNet.Value)
                    return;

                _isUsingNet.Value = true;
                TargetToggleComputerUI(conn, true);
                ObserverActivation(true);
            }
        }

        protected internal override void OnEndInteract_Server(NetworkConnection conn)
        {
            if (_isUsingNet.Value && IsOwner)
            {
                _isUsingNet.Value = false;
                TargetToggleComputerUI(conn, false);
                ObserverActivation(false);
            }

            base.OnEndInteract_Server(conn);
        }

        private void OnIsUsingChanged(bool prev, bool next, bool asServer)
        {
            EnsureInit();
            ApplyComputerStateImmediate(next);
        }

        private void ApplyComputerStateImmediate(bool open)
        {
            _isUsingLocal = open;
            if (!_wasStarted) return;

            bool useFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 1;

            if (!open)
            {
                if (contentCanvas) contentCanvas.gameObject.SetActive(false);
                if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);

                if (networkImageStream != null) networkImageStream.ClearTexture();

                if (IsOwner)
                {
                    if (WebViewManager.Instance != null)
                    {
                        WebViewManager.Instance.HideWorldView(IDNumber);
                        WebViewManager.Instance.Hide();
                    }

                    if (PlayerInput.Instance != null)
                    {
                        PlayerInput.Instance.HideMobileFallback = false;
                        PlayerInput.Instance.IsBusy = false;
                    }
                }

                return;
            }

            if (contentCanvas) contentCanvas.gameObject.SetActive(true);

            if (IsOwner)
            {
                if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;

                if (useFS)
                {
                    WebViewManager.Instance.OpenFullscreen();
                    if (networkImageStream != null && WebViewManager.Instance.WebViewRawImage != null)
                        networkImageStream.SetTexture(WebViewManager.Instance.WebViewRawImage);
                    if (PlayerInput.Instance != null) PlayerInput.Instance.IsBusy = true;
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
        }

        public void SwitchFS()
        {
            var newFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 0;
            PlayerPrefs.SetInt("PlayerSlotMachineIsFullscreen", newFS ? 1 : 0);
            PlayerPrefs.Save();

            ApplyComputerStateImmediate(_isUsingNet.Value);
        }

        [TargetRpc]
        private void TargetToggleComputerUI(NetworkConnection conn, bool open)
        {
            if (!_wasStarted) return;
            ApplyComputerStateImmediate(open);
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
