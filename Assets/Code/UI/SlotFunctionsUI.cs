using System;
using System.Collections;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network;
using Code.Network.Lobby;
using JetBrains.Annotations;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class SlotFunctionsUI : MonoBehaviour
    {
        [Header("Stories")] [SerializeField] private Button screenshotButton;
        [SerializeField, CanBeNull] private StoriesUI updateStoryUiOnLoad;
        [SerializeField] private float timeout = 10f;

        [Header("Stream")] [SerializeField] private Image streamIndicator;
        [SerializeField] private Button requestStreamButton;
        [SerializeField] private Button requestCancelStreamButton;
        [SerializeField] private float streamDownscale = 0.75f;
        [SerializeField] private int streamJpgQuality = 20;

        [Space] [SerializeField] private SlotMachineInteractable slotMachineInteractable;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private float resultShowTime = 5f;

        [Header("Fullscreen")] [SerializeField] private Button fullscreenButton;
        
        private Coroutine _resultShowCoroutine;
        private Coroutine _timeoutCoroutine;

        private MainScreenController _mainScreenController;

        private void Awake()
        {
            _mainScreenController = FindAnyObjectByType<MainScreenController>();
        }

        private void OnEnable()
        {
            if (resultText != null) resultText.text = string.Empty;
            screenshotButton.onClick.AddListener(OnScreenshotClicked);
            
            requestStreamButton.onClick.AddListener(RequestStream);
            requestCancelStreamButton.onClick.AddListener(CancelStream);
            
            _mainScreenController.StreamSlotId.OnChange += StreamSlotIdOnOnChange;
            
            fullscreenButton.onClick.AddListener(SwitchFullscreen);
        }

        private void SwitchFullscreen()
        {
            if (slotMachineInteractable != null)
                slotMachineInteractable.SwitchFS();
        }

        private void OnDisable()
        {
            if (screenshotButton != null) screenshotButton.onClick.RemoveListener(OnScreenshotClicked);

            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }

            if (screenshotButton != null)
                screenshotButton.interactable = true;

            if (_resultShowCoroutine != null)
            {
                StopCoroutine(_resultShowCoroutine);
                _resultShowCoroutine = null;
            }

            requestStreamButton.onClick.RemoveListener(RequestStream);
            requestCancelStreamButton.onClick.RemoveListener(CancelStream);
            
            _mainScreenController.StreamSlotId.OnChange -= StreamSlotIdOnOnChange;
            
            fullscreenButton.onClick.RemoveListener(SwitchFullscreen);
        }

        #region Stories
        
        private async void OnScreenshotClicked()
        {
            if (screenshotButton != null) screenshotButton.interactable = false;

            try
            {
                if (slotMachineInteractable == null || slotMachineInteractable.WebView == null)
                {
                    ShowResult("No WebView available.", Color.red);
                    return;
                }

                var bytes = await slotMachineInteractable.WebView.CaptureScreenshot();
                if (bytes == null || bytes.Length == 0)
                {
                    ShowResult("Empty screenshot.", Color.red);
                }
                else
                {
                    APIHandle(bytes);
                }
            }
            catch (Exception ex)
            {
                ShowResult("Screenshot failed: " + ex.Message, Color.red);
            }
            finally
            {
                if (_timeoutCoroutine != null) StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = StartCoroutine(TimeoutRoutine());
            }
        }

        private IEnumerator TimeoutRoutine()
        {
            yield return new WaitForSeconds(timeout);
            if (screenshotButton != null) screenshotButton.interactable = true;
            _timeoutCoroutine = null;
        }

        private void APIHandle(byte[] screenshotBytes)
        {
            string baseName = Guid.NewGuid().ToString("N");
            string uid = ClientDataStorage.UserData != null ? ClientDataStorage.UserData.id.ToString() : "unknown";
            string filename = $"{baseName}_{uid}_{DateTime.Now:yyyyMMdd_HHmmss}.png";

            var form = new WWWForm();
            form.AddBinaryData("file", screenshotBytes, filename);

            var loadFileRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetLoadFileUrl(),
                Headers = ClientDataStorage.GetJwtHeader(),
                FormData = form
            };

            RestClient.Post(loadFileRequest).Then(fileLoadResponse =>
            {
                if (fileLoadResponse.StatusCode != 200)
                {
                    ShowResult($"File load error. {fileLoadResponse.StatusCode}: {fileLoadResponse.Error}", Color.red);
                    return null;
                }

                var fileUri = fileLoadResponse.Text.Trim('\"');

                var loadStoryRequest = new RequestHelper
                {
                    Uri = ApiRoutes.GetLoadStoryUrl(),
                    Headers = ClientDataStorage.GetJwtHeader(),
                    Body = new PostStoryData
                    {
                        image_url = fileUri,
                        slot_id = slotMachineInteractable != null ? slotMachineInteractable.IDNumber : 0,
                        lobby_id = LobbyVariables.Instance != null && LobbyVariables.Instance.currentLobby != null
                            ? LobbyVariables.Instance.currentLobby.lobbyId
                            : 0.ToString()
                    }
                };
                return RestClient.Post(loadStoryRequest);
            })?.Then(loadStoryResponse =>
            {
                if (loadStoryResponse.StatusCode != 200)
                {
                    ShowResult($"Story load error. {loadStoryResponse.StatusCode}: {loadStoryResponse.Error}",
                        Color.red);
                    return;
                }

                var parsed = JsonUtility.FromJson<SuccessResponse<GetStoryData>>(loadStoryResponse.Text);
                if (parsed == null || !parsed.success)
                {
                    ShowResult(
                        $"Story load error. {(parsed != null ? parsed.code : "Error")}: {(parsed != null ? parsed.detail : "Invalid response")}",
                        Color.red);
                    return;
                }

                ShowResult($"Story load success id = {parsed.data?.id}", Color.black);
                if (updateStoryUiOnLoad != null)
                    updateStoryUiOnLoad.StartNewCycle();
            });
        }

        private void ShowResult(string text, Color color)
        {
            if (_resultShowCoroutine != null) StopCoroutine(_resultShowCoroutine);
            _resultShowCoroutine = StartCoroutine(ShowResultCoroutine(text ?? string.Empty, color));
        }

        private IEnumerator ShowResultCoroutine(string text, Color color)
        {
            if (resultText != null)
            {
                resultText.color = color;
                resultText.text = text;
            }

            yield return new WaitForSeconds(resultShowTime);

            if (resultText != null)
                resultText.text = string.Empty;
        }

        #endregion

        #region Stream On main Screen

        private void RequestStream()
        {
            _mainScreenController.RequestStream(slotMachineInteractable.IDNumber);
        }

        private void CancelStream()
        {
            _mainScreenController.RequestCancel();
        }
        
        private void StreamSlotIdOnOnChange(int prevId, int newId, bool asServer)
        {
            if(prevId == newId)
                return;
            
            var thisId = slotMachineInteractable.IDNumber;
            
            if (newId == thisId)
                OnStartStreaming();
            else if(newId != thisId && prevId == thisId)
                OnEndStreaming();
        }

        private void OnStartStreaming()
        {
            requestStreamButton.gameObject.SetActive(false);
            requestCancelStreamButton.gameObject.SetActive(true);
            slotMachineInteractable.NetworkImageStream.SetQualitySettings(streamDownscale, streamJpgQuality);
        }

        private void OnEndStreaming()
        {
            requestStreamButton.gameObject.SetActive(true);
            requestCancelStreamButton.gameObject.SetActive(false);
            slotMachineInteractable.NetworkImageStream.ResetQualitySettings();
        }

        #endregion
    }
}