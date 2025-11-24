using System;
using System.Collections;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network;
using FishNet;
using FishNet.Transporting;
using PlayFlow;
using Proyecto26;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class SlotFunctionsUI : MonoBehaviour
    {

        [Header("Stories")] [SerializeField] private StoriesUI updateStoryUiOnLoad;
        [SerializeField] private float timeout = 15f;
        private bool _screenShotBusy;

        [Header("Stream")] [SerializeField] private Image streamIndicator;
        [SerializeField] private float streamDownscale = 0.75f;
        [SerializeField] private int streamJpgQuality = 20;
        private bool _streaming;

        [Space] [SerializeField] private SlotMachineInteractable slotMachineInteractable;

        private Coroutine _resultShowCoroutine;
        private MainScreenController _mainScreenController;
        private float _time;

        private void Awake()
        {
            _mainScreenController = FindAnyObjectByType<MainScreenController>();
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientStarted;
        }

        private void OnClientStarted(ClientConnectionStateArgs args)
        {
            if(args.ConnectionState != LocalConnectionState.Started) return;
            if(_mainScreenController.StreamSlotId.Value == slotMachineInteractable.IDNumber)
                _mainScreenController.RequestCancel();
            StreamSlotIdOnOnChange(-1, _mainScreenController.StreamSlotId.Value, false);
        }

        private void OnEnable()
        {
            if (!slotMachineInteractable.IsOwner) return;
            if (PlayerInput.Instance.feedbackText != null) PlayerInput.Instance.feedbackText.text = string.Empty;

            _mainScreenController.StreamSlotId.OnChange += StreamSlotIdOnOnChange;
        }

        private void Update()
        {
            if (!slotMachineInteractable.IsOwner) return;

            if (PlayerInput.Instance.IsSlotsFullscreen) SwitchFullscreen();
            if (PlayerInput.Instance.IsSlotsScreenshot && !_screenShotBusy) OnScreenshotClicked();
            if (PlayerInput.Instance.IsSlotsStream && !_streaming) RequestStream();
            if (PlayerInput.Instance.IsSlotsStream && _streaming) CancelStream();

            if (_screenShotBusy)
            {
                _time -= Time.deltaTime;
                if (_time <= 0.1f)
                {
                    _screenShotBusy = false;
                    PlayerInput.Instance.slotsScreenshotButton.ResetCooldown();
                }
                else
                {
                    PlayerInput.Instance.slotsScreenshotButton.UpdateCooldown(_time, timeout);
                }
            }
        }

        private void SwitchFullscreen()
        {
            if (slotMachineInteractable != null)
                slotMachineInteractable.SwitchFullScreen();
        }

        private void OnDisable()
        {
            if (_resultShowCoroutine != null)
            {
                StopCoroutine(_resultShowCoroutine);
                _resultShowCoroutine = null;
            }

            _mainScreenController.StreamSlotId.OnChange -= StreamSlotIdOnOnChange;
        }

        private async void OnScreenshotClicked()
        {
            _screenShotBusy = true;
            _time = timeout;
            try
            {
                if (WebViewManager.Instance == null || WebViewManager.Instance.WebView == null)
                {
                    ShowResult("No WebView available.", Color.red, 2);
                    return;
                }

                var bytes = await WebViewManager.Instance.WebView.CaptureScreenshot(); // глобальный вебвью [1]
                if (bytes == null || bytes.Length == 0)
                {
                    ShowResult("Empty screenshot.", Color.red, 2);
                }
                else
                {
                    ShowResult("Loading story...", Color.gray);
                    APIHandle(bytes);
                }
            }
            catch (Exception ex)
            {
                ShowResult("Screenshot failed: " + ex.Message, Color.red, 2);
            }
        }

        private void APIHandle(byte[] screenshotBytes)
        {
            string baseName = Guid.NewGuid().ToString("N");
            string uid = ClientDataStorage.UserData.id > 0 ? ClientDataStorage.UserData.id.ToString() : "unknown";
            string filename = $"{baseName}_{uid}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var form = new WWWForm();
            form.AddBinaryData("file", screenshotBytes, filename);

            var loadFileRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetLoadFileUrl(),
                Headers = ClientDataStorage.GetJwtHeader(),
                FormData = form,
                Timeout = 8
            };

            RestClient.Post(loadFileRequest).Then(fileLoadResponse =>
            {
                if (fileLoadResponse.StatusCode != 200)
                {
                    ShowResult($"File load error. {fileLoadResponse.StatusCode}: {fileLoadResponse.Error}", Color.red, 2);
                    return null;
                }

                Debug.Log("File load successful");
                var fileUri = fileLoadResponse.Text.Trim('\"');

                var loadStoryRequest = new RequestHelper
                {
                    Uri = ApiRoutes.GetLoadStoryUrl(),
                    Headers = ClientDataStorage.GetJwtHeader(),
                    Body = new PostStoryData
                    {
                        image_url = fileUri,
                        slot_id = slotMachineInteractable != null ? slotMachineInteractable.IDNumber : 0,
                        lobby_id = PlayFlowLobbyManagerV2.Instance != null && PlayFlowLobbyManagerV2.Instance.CurrentLobby != null ? PlayFlowLobbyManagerV2.Instance.CurrentLobby.id : 0.ToString(),
                    },
                    Timeout = 7
                };
                return RestClient.Post(loadStoryRequest);
            })?.Then(loadStoryResponse =>
            {
                if (loadStoryResponse.StatusCode != 200)
                {
                    ShowResult($"Story load error. {loadStoryResponse.StatusCode}: {loadStoryResponse.Error}",
                        Color.red,2);
                    return;
                }

                var parsed = JsonUtility.FromJson<SuccessResponse<GetStoryData>>(loadStoryResponse.Text);
                if (parsed == null || !parsed.success)
                {
                    ShowResult(
                        $"Story load error. {(parsed != null ? parsed.code : "Error")}: {(parsed != null ? parsed.detail : "Invalid response")}",
                        Color.red, 2);
                    return;
                }
                
                ShowResult($"Story load success id = {parsed.data?.id}", Color.gray, 2);
                if (updateStoryUiOnLoad != null)
                    updateStoryUiOnLoad.StartNewCycle();
            }).Catch(err => {
                ShowResult($"Load story error: {err.Message}", Color.red, 2);
            });
        }
        
        private void ShowResult(string text, Color color, int timeoutInSeconds = int.MaxValue)
        {
            if(timeoutInSeconds < 0) timeoutInSeconds = 0;
            if (_resultShowCoroutine != null) StopCoroutine(_resultShowCoroutine);
            _resultShowCoroutine = StartCoroutine(ShowResultCoroutine(text ?? string.Empty, color, timeoutInSeconds));
        }

        private IEnumerator ShowResultCoroutine(string text, Color color, int timeoutInSeconds = int.MaxValue)
        {
            var resultText = PlayerInput.Instance.feedbackText;
            
            if (resultText != null)
            {
                resultText.color = color;
                resultText.text = text;
            }

            yield return new WaitForSeconds(timeoutInSeconds);
            if (resultText != null) resultText.text = string.Empty;
        }

        private void RequestStream()
        {
            var nickname = !string.IsNullOrEmpty(ClientDataStorage.UserData.username) ? ClientDataStorage.UserData.username : "unknown";
            _mainScreenController.RequestStream(slotMachineInteractable.IDNumber, nickname);
        }

        private void CancelStream()
        {
            _mainScreenController.RequestCancel();
        }

        private void StreamSlotIdOnOnChange(int prevId, int newId, bool asServer)
        {
            var thisId = slotMachineInteractable.IDNumber;
            if (newId == thisId) OnStartStreaming();
            else if (newId != thisId) OnEndStreaming();
        }

        private void OnStartStreaming()
        {
            _streaming = true;
            slotMachineInteractable.NetworkImageStream.SetQualitySettings(streamDownscale, streamJpgQuality);
        }

        private void OnEndStreaming()
        {
            _streaming = false;
            slotMachineInteractable.NetworkImageStream.ResetQualitySettings();
        }
    }
}