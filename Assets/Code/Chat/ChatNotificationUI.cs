using System.Collections;
using UnityEngine;

namespace Code.Chat
{
    public class ChatNotificationUI : MonoBehaviour
    {
        [SerializeField] private MessageComponent messageView;
        [SerializeField] private CanvasGroup messageCanvasGroup;
        
        [Header("Animation Settings")]
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeOutDuration = 0.3f;
        
        private Coroutine _animationCoroutine;
        private ChatMessage _pendingMessage;
        private bool _notificationsEnabled = true;

        private void OnValidate()
        {
            if (messageView != null && messageCanvasGroup == null)
            {
                messageCanvasGroup = messageView.GetComponent<CanvasGroup>();
            }
        }

        private void Start()
        {
            messageView.gameObject.SetActive(false);
            messageCanvasGroup.alpha = 0f;
        }

        public void OnMessage(ChatMessage message)
        {
            if (!_notificationsEnabled)
                return;

            _pendingMessage = message;
            
            // Если нет активной анимации - показываем сразу
            if (_animationCoroutine == null)
            {
                ShowNextNotification();
            }
        }

        private void ShowNextNotification()
        {
            if (_pendingMessage == null)
                return;

            var message = _pendingMessage;
            _pendingMessage = null;

            _animationCoroutine = StartCoroutine(ShowNotificationCoroutine(message));
        }

        private IEnumerator ShowNotificationCoroutine(ChatMessage message)
        {
            messageView.Init(message);
            messageView.gameObject.SetActive(true);

            // Fade In
            yield return FadeCanvasGroup(1f, fadeInDuration);

            // Display
            yield return new WaitForSeconds(displayDuration);

            // Fade Out
            yield return FadeCanvasGroup(0f, fadeOutDuration);

            messageView.gameObject.SetActive(false);
            _animationCoroutine = null;

            // Показываем следующее если оно есть
            ShowNextNotification();
        }

        private IEnumerator FadeCanvasGroup(float targetAlpha, float duration)
        {
            float startAlpha = messageCanvasGroup.alpha;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                messageCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }
            
            messageCanvasGroup.alpha = targetAlpha;
        }

        public void OnNotificationChange(bool isOn)
        {
            _notificationsEnabled = isOn;
            
            if (!isOn && _animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
                messageView.gameObject.SetActive(false);
                messageCanvasGroup.alpha = 0f;
                _pendingMessage = null;
            }
        }

        private void OnDestroy()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
        }
    }
}