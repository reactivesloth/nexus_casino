using System;
using System.Collections;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network.Lobby;
using JetBrains.Annotations;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class ScreenshotButtonHandler : MonoBehaviour
    {
        [SerializeField] private Button screenshotButton;
        [SerializeField] private TMP_Text resultText;
        [SerializeField, CanBeNull] private SlotMachineInteractable slotMachineInteractable;
        [SerializeField, CanBeNull] private StoriesUI updateStoryUiOnLoad;
        [SerializeField] private float timeout = 10f;
        [SerializeField] private float resultShowTime = 5f;

        private Coroutine _resultShowCoroutine;
        private Coroutine _timeoutCoroutine;

        private void OnEnable()
        {
            resultText.text = string.Empty;
            screenshotButton.onClick.AddListener(OnScreenshotClicked);
        }

        private void OnDisable()
        {
            screenshotButton.onClick.RemoveListener(OnScreenshotClicked);

            // Сброс таймера и разблокировка кнопки
            if (_timeoutCoroutine != null)
            {
                StopCoroutine(_timeoutCoroutine);
                _timeoutCoroutine = null;
                screenshotButton.interactable = true;
            }
        }

        private async void OnScreenshotClicked()
        {
            // Деактивировать кнопку
            screenshotButton.interactable = false;

            if (slotMachineInteractable == null || slotMachineInteractable.WebView == null)
                return;

            byte[] screenshotBytes = await slotMachineInteractable.WebView.CaptureScreenshot();
            APIHandle(screenshotBytes);

            // Запустить таймер разблокировки
            _timeoutCoroutine = StartCoroutine(TimeoutRoutine());
        }

        private IEnumerator TimeoutRoutine()
        {
            yield return new WaitForSeconds(timeout);
            screenshotButton.interactable = true;
            _timeoutCoroutine = null;
        }

        private void APIHandle(byte[] screenshotBytes)
        {

            var filename = $"{Guid.NewGuid()}_{ClientDataStorage.UserData.id}_{DateTime.Now}.png".Replace(' ','_');
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
                        slot_id = slotMachineInteractable.IDNumber,
                        lobby_id = LobbyVariables.Instance.currentLobby.lobbyId
                    }
                };
                return RestClient.Post(loadStoryRequest);
            })?.Then(loadStoryResponse =>
            {
                if (loadStoryResponse.StatusCode != 200)
                {
                    ShowResult($"Story load error. {loadStoryResponse.StatusCode}: {loadStoryResponse.Error}", Color.red);
                    return;
                }

                var loadStoryResponseParsed =
                    JsonUtility.FromJson<SuccessResponse<GetStoryData>>(loadStoryResponse.Text);

                if (!loadStoryResponseParsed.success)
                {
                    ShowResult($"Story load error. {loadStoryResponseParsed.code}: {loadStoryResponseParsed.detail}", Color.red);
                    return;
                }
                
                ShowResult($"Story load success id = {loadStoryResponseParsed.data?.id}", Color.black);
                if(updateStoryUiOnLoad != null)
                    updateStoryUiOnLoad.StartNewCycle();
            });
        }

        private void ShowResult(string text, Color color)
        {
            if(_resultShowCoroutine != null)
                StopCoroutine(_resultShowCoroutine);
            
            _resultShowCoroutine = StartCoroutine(ShowResultCoroutine(text, color));
        }
        
        private IEnumerator ShowResultCoroutine(string text, Color color)
        {
            resultText.color = color;
            resultText.text = text;

            yield return new WaitForSeconds(resultShowTime);
            
            resultText.text = string.Empty;
        }
    }
}