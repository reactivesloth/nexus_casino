using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.Network;
using Code.Network.InteractionSystem;
using Code.Utility;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Code.UI
{
    public class InteractionUIHint : MonoBehaviour
    {
        public static InteractionUIHint Instance { get; private set; }

        [Header("Interaction")] [SerializeField]
        private GameObject _promptUI;

        [SerializeField] private TextMeshProUGUI _promptText;
        [Header("Social")] [SerializeField] private GameObject _socialUI;
        [SerializeField] private TextMeshProUGUI _likesCountText, _viewsCountText;
        [SerializeField] private Button _likeButton;
        [SerializeField] private GameObject _likeIndicatorObject;

        private Interactable _interactable;
        private bool _initedHint;
        private bool _hasLikedStatus;

        public bool IsSocialOpened => _socialUI.activeSelf;

        private void EnsureInit()
        {
            if (_initedHint) return;
            _initedHint = true;
            if (_promptUI != null && !_promptUI.activeSelf)
                _promptUI.SetActive(false);
            if (_promptText != null && string.IsNullOrEmpty(_promptText.text))
                _promptText.text = string.Empty;
            _socialUI.gameObject.SetActive(false);
            _likesCountText.text = string.Empty;
            _viewsCountText.text = string.Empty;
            _likeButton.interactable = false;
            _likeIndicatorObject.SetActive(false);
        }

        private void Awake()
        {
            Instance = this;
            EnsureInit();
            HidePrompt();
            HideSocial();
        }

        private void OnEnable()
        {
            _likeButton.onClick.AddListener(OnLikeClicked);
        }

        private void OnDisable()
        {
            _likeButton.onClick.RemoveListener(OnLikeClicked);
        }

        public void ShowPrompt(string message)
        {
            EnsureInit();
            if (_promptUI != null) _promptUI.SetActive(true);
            if (_promptText != null)
                LocalizationHelper.SetLocalizedTextAsync(_promptText, message);
            if (string.IsNullOrEmpty(message))
                HidePrompt();
        }

        public void HidePrompt()
        {
            EnsureInit();
            if (_promptUI != null) _promptUI.SetActive(false);
        }

        public void ShowSocial(Interactable interactable)
        {
            if (IsSocialOpened)
                return;
            
            EnsureInit();
            _interactable = interactable;
            _socialUI.SetActive(true);

            var interactableRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetInteractableSocialUrl(interactable.Key),
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Get(interactableRequest).Then(interactableResponse =>
            {
                Debug.Log($"[InteractionUIHint] Social data: {interactableResponse.Text}");
                if (interactableResponse.StatusCode != 200)
                    return;
                var responseData =
                    JsonUtility.FromJson<SuccessResponse<InteractableSocialData>>(interactableResponse.Text);
                if (!responseData.success)
                    return;
                InitSocial(responseData.data);
                _likeButton.interactable = true;
            });
        }

        public void HideSocial()
        {
            if (!IsSocialOpened)
                return;

            EnsureInit();
            _interactable = null;
            _socialUI.gameObject.SetActive(false);
            _likeButton.interactable = false;
            SetViewStatus(0);
            SetLikeButtonStatus(false, 0);
        }

        private void InitSocial(InteractableSocialData data)
        {
            SetViewStatus(data.views_count);
            if (!data.is_viewed_by_me)
                RestClient.Post(new RequestHelper
                {
                    Uri = ApiRoutes.PostMarkViewedUrl(),
                    Headers = ClientDataStorage.GetJwtHeader(),
                    Body = new InteractableSocialRequest
                    {
                        object_id = _interactable.Key
                    }
                }).Then(markViewedResponse =>
                {
                    if (markViewedResponse.StatusCode != 200)
                        return;
                    var responseData =
                        JsonUtility.FromJson<SuccessResponse<InteractableSocialData>>(markViewedResponse.Text);
                    if (!responseData.success)
                        return;
                    SetViewStatus(responseData.data.views_count);
                });

            SetLikeButtonStatus(data.is_liked_by_me, data.likes_count);
        }

        private void SetViewStatus(int viewsCount)
        {
            _viewsCountText.text = viewsCount.ToString();
        }

        private void SetLikeButtonStatus(bool hasLiked, int likeCount)
        {
            _hasLikedStatus = hasLiked;
            _likeIndicatorObject.SetActive(_hasLikedStatus);
            _likesCountText.text = likeCount.ToString();
        }

        private void OnLikeClicked()
        {
            if (_interactable == null)
                return;

            var toggleLikeRequest = new RequestHelper
            {
                Uri = ApiRoutes.PostToggleLikeUrl(),
                Headers = ClientDataStorage.GetJwtHeader(),
                Body = new ToggleLikeRequest
                {
                    object_id = _interactable.Key,
                    like = !_hasLikedStatus
                }
            };

            RestClient.Post(toggleLikeRequest).Then(toggleLikeResponse =>
            {
                if (toggleLikeResponse.StatusCode != 200)
                    return;
                var responseData =
                    JsonUtility.FromJson<SuccessResponse<InteractableSocialData>>(toggleLikeResponse.Text);
                if (!responseData.success)
                    return;
                SetLikeButtonStatus(responseData.data.is_liked_by_me, responseData.data.likes_count);
            });
        }
    }
}