using System;
using System.Collections;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Network.Lobby;
using Code.Stories;
using Proyecto26;
using UnityEngine;
using UnityEngine.UI;
using Vuplex.WebView;

namespace Code.UI
{
    public class ScreenshotButtonHandler : MonoBehaviour
    {
        [SerializeField] private Button screenshotButton;
        [SerializeField] private CanvasWebViewPrefab webView;
        [SerializeField] private SlotMachineInteractable slotMachineInteractable;
        [SerializeField] private float timeout = 10f;

        private Coroutine _timeoutCoroutine;

        private void OnEnable()
        {
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

        private void LocalHandle(byte[] screenshotBytes)
        {
            var slotId = slotMachineInteractable ? slotMachineInteractable.IDNumber : -1;
            LocalStoriesStorage.Instance.ScreenshotMake(screenshotBytes, slotId);
        }

        private void APIHandle(byte[] screenshotBytes)
        {

            var filename = $"{Guid.NewGuid()}_{ClientDataStorage.UserData.username}_{DateTime.Now}.png".Replace(' ','_');
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
                    return null;

                var fileUri = fileLoadResponse.Text.Trim('\"');

                var loadStoryRequest = new RequestHelper
                {
                    Uri = ApiRoutes.GetLoadStoryUrl(),
                    Headers = ClientDataStorage.GetJwtHeader(),
                    Body = new PostStoryData
                    {
                        image_url = fileUri,
                        slot_id = slotMachineInteractable.IDNumber,
                        //lobby_id = LobbyVariables.Instance.currentLobby.lobbyId
                    }
                };
                return RestClient.Post(loadStoryRequest);
            });
        }
    }
}