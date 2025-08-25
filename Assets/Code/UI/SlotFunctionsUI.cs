using System;
using System.Collections;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network;
using Code.Network.Lobby;
using FishNet;
using JetBrains.Annotations;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class SlotFunctionsUI : MonoBehaviour
    {
        [Header("Stories")]
        [SerializeField, CanBeNull] private StoriesUI updateStoryUiOnLoad;
        [SerializeField] private float timeout = 10f;
        private bool _screenShotBusy;

        [Header("Stream")] [SerializeField] private Image streamIndicator;
        [SerializeField] private float streamDownscale = 0.75f;
        [SerializeField] private int streamJpgQuality = 20;
        private bool _streaming;

        [Space] [SerializeField] private SlotMachineInteractable slotMachineInteractable;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private float resultShowTime = 5f;
        
        private Coroutine _resultShowCoroutine;
        private Coroutine _timeoutCoroutine;

        private MainScreenController _mainScreenController;

        private void Awake()
        {
            _mainScreenController = FindAnyObjectByType<MainScreenController>();
        }

        private void OnEnable()
        {
            if (!slotMachineInteractable.IsOwner) return;
            
            if (resultText != null) resultText.text = string.Empty;
            _mainScreenController.StreamSlotId.OnChange += StreamSlotIdOnOnChange;
            StreamSlotIdOnOnChange(-1, _mainScreenController.StreamSlotId.Value, false);
        }

        private void Update()
        {
            if (!slotMachineInteractable.IsOwner) return;
            
            if (!PlayerInput.Instance.ShowSlotsUI) PlayerInput.Instance.ShowSlotsUI = true;
            
            if (PlayerInput.Instance.IsSlotsFullscreen) SwitchFullscreen();
            if (PlayerInput.Instance.IsSlotsScreenshot && !_screenShotBusy) OnScreenshotClicked();
            if (PlayerInput.Instance.IsSlotsStream && !_streaming) RequestStream();
            if (PlayerInput.Instance.IsSlotsStream && _streaming) CancelStream();
        }

        private void SwitchFullscreen()
        {
            if (slotMachineInteractable != null)
                slotMachineInteractable.SwitchFS();
        }

        private void OnDisable()
        {
            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
            }

            if (_resultShowCoroutine != null)
            {
                StopCoroutine(_resultShowCoroutine);
                _resultShowCoroutine = null;
            }
            
            _mainScreenController.StreamSlotId.OnChange -= StreamSlotIdOnOnChange;
            PlayerInput.Instance.ShowSlotsUI = false;
        }

        #region Stories
        
        private async void OnScreenshotClicked()
        {
            _screenShotBusy = true;
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
            _screenShotBusy = false;
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
            var connectionId = InstanceFinder.ClientManager.Connection.ClientId;
            var nickname = ClientDataStorage.UserData != null ? ClientDataStorage.UserData.username : "unknown";
            _mainScreenController.RequestStream(slotMachineInteractable.IDNumber, connectionId, nickname);
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
            else if(newId != thisId)
                OnEndStreaming();
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

        #endregion
    }
}