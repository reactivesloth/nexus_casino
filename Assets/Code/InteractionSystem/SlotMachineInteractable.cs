using System.Linq;
using Code.Network;
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

        private bool _initSlot;

        private void Awake()
        {
            if (_initSlot) return;
            _initSlot = true;

            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);
            if (contentCanvas) contentCanvas.gameObject.SetActive(false);

            if (networkImageStream == null)
                networkImageStream = GetComponentInChildren<NetworkImageStream>(true);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            contentCanvas.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (networkImageStream == null) networkImageStream = GetComponentInChildren<NetworkImageStream>(true);
        }
#endif

        public override string InteractionPrompt => !IsOccupied ? "Use Computer" : "Exit Computer";
        
        protected override void OnInteractCallback_Client(bool success, bool force = false)
        {
            base.OnInteractCallback_Client(success, force);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            ToggleComputerUI(true);
        }

        protected override void OnInteractEndCallback_Client(bool success)
        {
            base.OnInteractEndCallback_Client(success);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            ToggleComputerUI(false);
        }

        protected override void OnInteractCallback_Observers(bool success, bool force = false)
        {
            base.OnInteractCallback_Observers(success, force);
            if(!success)
                return;
            if (contentCanvas) contentCanvas.gameObject.SetActive(true);
        }

        protected override void OnInteractEndCallback_Observers(bool success)
        {
            base.OnInteractEndCallback_Observers(success);
            if(!success)
                return;
            if (contentCanvas) contentCanvas.gameObject.SetActive(false);
        }

        private void ApplyComputerStateImmediate(bool open)
        {
            bool useFs = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 1;

            Debug.Log($"Open {open}");

            if (!open)
            {
                if (contentCanvas) contentCanvas.gameObject.SetActive(false);
                if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);

                if (networkImageStream != null) networkImageStream.ClearTexture();

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
            else
            {
                if (contentCanvas) contentCanvas.gameObject.SetActive(true);

                if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;

                if (useFs)
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

        public void SwitchFullScreen()
        {
            PlayerPrefs.SetInt("PlayerSlotMachineIsFullscreen", PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 0 ? 1 : 0);
            PlayerPrefs.Save();

            ApplyComputerStateImmediate(true);
        }

        private void ToggleComputerUI(bool open)
        {
            ApplyComputerStateImmediate(open);
        }

        public static SlotMachineInteractable FindById(int id)
        {
            var all = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(s => s.IDNumber == id);
        }
    }
}
