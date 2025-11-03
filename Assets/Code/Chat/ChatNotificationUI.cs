using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

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
        
        private Coroutine _currentAnimationCoroutine;
        private ChatMessage _pendingMessage;
        private bool _hasPendingMessage = false;
        private bool _isShowingNotification = false;
        private bool _notificationsEnabled = true;

        private void OnValidate()
        {
            messageCanvasGroup ??= messageView.GetComponent<CanvasGroup>();
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

            // Сохраняем последнее пришедшее сообщение
            _pendingMessage = message;
            _hasPendingMessage = true;

            // Если не показываем - начинаем показывать
            if (!_isShowingNotification)
            {
                ShowNextNotification();
            }
            // Если показываем - ждём завершения текущего и покажем последнее пришедшее
        }

        private void ShowNextNotification()
        {
            if (!_hasPendingMessage)
            {
                _isShowingNotification = false;
                return;
            }

            _isShowingNotification = true;
            _hasPendingMessage = false;
            ChatMessage message = _pendingMessage;

            _currentAnimationCoroutine = StartCoroutine(ShowNotificationCoroutine(message));
        }

        private IEnumerator ShowNotificationCoroutine(ChatMessage message)
        {
            // Инициализируем сообщение
            messageView.Init(message);
            messageView.gameObject.SetActive(true);

            // Fade In
            yield return FadeCanvasGroup(messageCanvasGroup, messageCanvasGroup.alpha, 1f, fadeInDuration);

            // Отображение
            yield return new WaitForSeconds(displayDuration);

            // Fade Out
            yield return FadeCanvasGroup(messageCanvasGroup, 1f, 0f, fadeOutDuration);

            messageView.gameObject.SetActive(false);

            // Показываем следующее если оно есть
            ShowNextNotification();
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
        {
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / duration;
                cg.alpha = Mathf.Lerp(startAlpha, endAlpha, normalizedTime);
                yield return null;
            }
            
            cg.alpha = endAlpha;
        }

        public void OnNotificationChange(bool isOn)
        {
            _notificationsEnabled = isOn;
            
            // Если отключаем уведомления - скрываем текущее
            if (!isOn)
            {
                if (_currentAnimationCoroutine != null)
                {
                    StopCoroutine(_currentAnimationCoroutine);
                    _currentAnimationCoroutine = null;
                }
                
                messageView.gameObject.SetActive(false);
                messageCanvasGroup.alpha = 0f;
                _isShowingNotification = false;
                _hasPendingMessage = false;
            }
        }

        private void OnDestroy()
        {
            if (_currentAnimationCoroutine != null)
            {
                StopCoroutine(_currentAnimationCoroutine);
            }
        }
    }
}