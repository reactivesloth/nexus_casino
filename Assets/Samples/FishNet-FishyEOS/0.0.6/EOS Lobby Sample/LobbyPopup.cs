using System.Collections;
using System.Threading.Tasks;
using Code.UI;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EOSLobby
{
    public class LobbyPopup : MonoBehaviour
    {
        [SerializeField] private GameObject lobbyPopupUI;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;
        private Coroutine _popupCoroutine;
        public bool ShowAtStartUntilHide = false;
        public int HideAtStartAt = 0;

        private void Awake()
        {
            if (ShowAtStartUntilHide)
                Show ("loading.please_wait", "loading", 0);
            else if (HideAtStartAt > 0)
            {
                Show ("loading.please_wait", "loading", 0);
                Invoke ("Hide", HideAtStartAt);
            }
        }

        public void Show(string title, string message, int percentage = 100)
        {
            //Debug.Log($"[LobbyPopup] Showing: {title} - {message}"); 
            //LoadingScreenUI.Instance.Show(title, message, percentage);

            // Заказчик попросил убрать процентный индикатор загрузки при поиске/создании лобби. Если вдруг передумает - убрать эту строку и раскомментировать предыдущие две.
            LoadingScreenUI.Instance.Show (title, message, 0);
        }
        
        public void Hide()
        {
            Debug.Log("[LobbyPopup] Hiding");
            LoadingScreenUI.Instance.Hide();
            lobbyPopupUI.SetActive(false);
        }

        public async Task<bool> PromptAsync(string title, string message, bool showOkButton = true, bool showCancelButton = false)
        {
            okButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            titleText.text = title;
            messageText.text = message;
            okButton.gameObject.SetActive(showOkButton);
            cancelButton.gameObject.SetActive(showCancelButton);
            lobbyPopupUI.SetActive(true);
            var tcs = new TaskCompletionSource<bool>();
            okButton.onClick.AddListener(() => tcs.SetResult(true));
            cancelButton.onClick.AddListener(() => tcs.SetResult(false));
            var result = await tcs.Task;
            lobbyPopupUI.SetActive(false);
            okButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            return result;
        }

        public class PromptResult
        {
            public bool? Value { get; set; }
        }
        
        public Coroutine PromptCoroutine(out PromptResult promptResult, string title, string message, bool showOkButton = true, bool showCancelButton = false)
        {
            lobbyPopupUI.SetActive(true);
            promptResult = new PromptResult();
            if (_popupCoroutine != null) StopCoroutine(_popupCoroutine);
            return _popupCoroutine = StartCoroutine(PromptCoroutineRoutine(promptResult, title, message, showOkButton, showCancelButton));
        }
        
        private IEnumerator PromptCoroutineRoutine(PromptResult promptResult, string title, string message, bool showOkButton, bool showCancelButton)
        {
            okButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            titleText.text = title;
            messageText.text = message;
            okButton.gameObject.SetActive(showOkButton);
            cancelButton.gameObject.SetActive(showCancelButton);
            okButton.onClick.AddListener(() => promptResult.Value = true);
            cancelButton.onClick.AddListener(() => promptResult.Value = false);
            yield return new WaitUntil(() => promptResult.Value.HasValue);
            okButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
            _popupCoroutine = null;
            lobbyPopupUI.SetActive(false);
        }
    }
}