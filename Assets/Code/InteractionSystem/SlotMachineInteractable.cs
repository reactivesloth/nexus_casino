using System;
using System.Linq;
using Code.API;
using Code.Network;
using Code.Utility;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.InteractionSystem
{
    [Serializable]
    public enum Providers
    {
        all,
        cq9,
        superomatic,
        champion,
        onlyplay
    }
    
    public class SlotMachineInteractable : Interactable
    {
        [Header("Slot Screen Promo Material")]
        [SerializeField] private MeshRenderer _slotScreenPromoMeshRenderer;
        [SerializeField] private int slotsScreenPromoMaterialIndex;
        private Material _slotsScreenPromoMaterial;
        [SerializeField] private Texture2D [] slotsScreenPromoSpriteSheet;
        
        [Header("UI Settings")]
        [SerializeField] private Canvas computer3dCanvas;
        [SerializeField] private Canvas contentCanvas;
        [SerializeField] private TextMeshPro idNumberText;

        [Header("Streaming")]
        [SerializeField] private NetworkImageStream networkImageStream;
        public NetworkImageStream NetworkImageStream => networkImageStream;
        
        [SerializeField] private Providers provider = Providers.all;
        
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
        
        private void Start()
        {
            SetupScreensForPromo();
        }

        private void SetupScreensForPromo()
        {
            _slotsScreenPromoMaterial = _slotScreenPromoMeshRenderer.sharedMaterials[slotsScreenPromoMaterialIndex];

            switch (provider)
            {
                case Providers.all:
                    _slotsScreenPromoMaterial.mainTexture = slotsScreenPromoSpriteSheet[Random.Range(0, slotsScreenPromoSpriteSheet.Length)];
                    break;
                case Providers.cq9:
                    _slotsScreenPromoMaterial.mainTexture = slotsScreenPromoSpriteSheet[0];
                    break;
                case Providers.superomatic:
                    _slotsScreenPromoMaterial.mainTexture = slotsScreenPromoSpriteSheet[1];
                    break;
                case Providers.champion:
                    _slotsScreenPromoMaterial.mainTexture = slotsScreenPromoSpriteSheet[2];
                    break;
                case Providers.onlyplay:
                    _slotsScreenPromoMaterial.mainTexture = slotsScreenPromoSpriteSheet[3];
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
            if (_slotScreenPromoMeshRenderer != null && slotsScreenPromoSpriteSheet.Length > 0) SetupScreensForPromo();
        }
#endif
        
        protected override void OnInteractCallback_Client(bool success, bool force = false)
        {
            base.OnInteractCallback_Client(success, force);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            ToggleComputerUI(true, force);
        }

        protected override void OnInteractEndCallback_Client(bool success)
        {
            base.OnInteractEndCallback_Client(success);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            ToggleComputerUI(false, false);
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

        private string GetProvider()
        {
            return provider switch
            {
                Providers.all => "",
                Providers.cq9 => "cq9",
                Providers.superomatic => "superomatic",
                Providers.champion => "champion",
                Providers.onlyplay => "onlyplay",
                _ => ""
            };
        }

        private void ApplyComputerStateImmediate(bool open, bool silentURL = false)
        {
            bool useFs = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 1;

            Debug.Log($"Open {open}");

            if (!open)
            {
                CursorManager.Instance.HideCursor();
                
                if (contentCanvas) contentCanvas.gameObject.SetActive(false);
                if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);

                if (networkImageStream != null) networkImageStream.ClearTexture();

                if (WebViewManager.Instance != null)
                {
                    WebViewManager.Instance.HideWorldView(IDNumber, GetProvider());
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
                CursorManager.Instance.ShowCursor();
                
                if (contentCanvas) contentCanvas.gameObject.SetActive(true);

                if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;

                if (!silentURL)
                {
                    WebViewManager.Instance.LoadURL(
                        string.IsNullOrEmpty(ClientDataStorage.AccessToken) ? "" : ClientDataStorage.AccessToken,
                        GetProvider()
                    );
                }

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

            ApplyComputerStateImmediate(true, true);
        }

        private void ToggleComputerUI(bool open, bool force = false)
        {
            ApplyComputerStateImmediate(open, force);
        }

        public static SlotMachineInteractable FindById(int id)
        {
            var all = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(s => s.IDNumber == id);
        }
    }
}
