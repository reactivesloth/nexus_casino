using System;
using Code.API;
using Code.Network;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using TMPro;
using Vuplex.WebView;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")]
        [SerializeField, Tooltip("Drag сюда ваш Canvas (может быть Screen-Space или World-Space)")]
        private Canvas computerCanvas, contentCanvas;
        
        
        [SerializeField] private TextMeshPro idNumberText;

        [SerializeField] private CanvasWebViewPrefab webViewPrefab;
        [SerializeField] private NetworkImageStream networkImageStream;
        private bool _isUsing = false;

        private Material _material;

        public override string InteractionPrompt =>
            !_isUsing ? "Use Computer" : "Exit Computer";

        public int IDNumber;
        private CanvasWebViewPrefab _webView;
        public IWebView WebView => _webView.WebView;
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            //webView ??= GetComponentInChildren<CanvasWebViewPrefab>(true);
            networkImageStream ??= GetComponentInChildren<NetworkImageStream>(true);
            
            if (idNumberText != null)
                idNumberText.text = IDNumber.ToString();
        }
#endif
        
        private void Awake()
        {
            if (idNumberText != null)
                idNumberText.text = IDNumber.ToString();
        }

        private void Start()
        {
            if (computerCanvas)
                computerCanvas.gameObject.SetActive(false);
        }

        private void Reset()
        {
            if (!computerCanvas)
                computerCanvas = GetComponentInChildren<Canvas>(true);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isUsing = false;
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
            contentCanvas.gameObject.SetActive(open);
            
            if (!open)
            {
                _webView.WebView?.Dispose();
                DestroyImmediate(_webView);
                networkImageStream.ClearTexture();
                //networkImageStream.StopStreaming();
            }
            else
            {
                _webView = Instantiate(webViewPrefab, computerCanvas.transform);
                _webView.transform.SetAsFirstSibling();
                
                if (_webView.WebView == null)
                    _webView.InitialUrl = $"https://back.nexusmetaclub.com?jwt={ClientDataStorage.AccessToken}";
                else
                    _webView.WebView?.LoadUrl($"https://back.nexusmetaclub.com?jwt={ClientDataStorage.AccessToken}");
                networkImageStream.SetTexture();
                //networkImageStream.StartStreaming();
            }
        }
    }
}