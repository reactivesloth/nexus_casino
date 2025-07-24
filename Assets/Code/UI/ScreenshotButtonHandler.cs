using System;
using Code.Stories;
using UnityEngine;
using UnityEngine.UI;
using Vuplex.WebView;

namespace Code.UI
{
    public class ScreenshotButtonHandler: MonoBehaviour
    {
        [SerializeField] private Button screenshotButton;
        [SerializeField] private CanvasWebViewPrefab webView;

        private void OnEnable()
        {
            screenshotButton.onClick.AddListener(OnScreenshotClicked);
        }

        private void OnDisable()
        {
            screenshotButton.onClick.RemoveListener(OnScreenshotClicked);
        }

        private async void OnScreenshotClicked()
        {
            byte[] screenshotBytes = await webView.WebView.CaptureScreenshot();
            LocalHandle(screenshotBytes);
        }

        private void LocalHandle(byte[] screenshotBytes)
        {
            LocalStoriesStorage.Instance.ScreenshotMake(screenshotBytes);
        }

        private void APIHandle(byte[] screenshotBytes)
        {
            
        }
    }
}