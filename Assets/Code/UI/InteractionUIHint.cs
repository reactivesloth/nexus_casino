using System.Collections.Generic;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
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
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
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
                Uri = ApiRoutes.GetInteractableSocialUrl(),
                Headers = ClientDataStorage.GetJwtHeader(),
                Params = new Dictionary<string, string> { { "object_id", interactable.Key } }
            };

            RestClient.Get(interactableRequest).Then(interactableResponse =>
            {
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
            EnsureInit();
            _interactable = null;
            _socialUI.gameObject.SetActive(false);
            _likeButton.interactable = false;
        }

        private void InitSocial(InteractableSocialData data)
        {
            _viewsCountText.text = data.views_count.ToString();
            // TODO: mark view where its done
            if (!data.is_viewed_by_me)
                RestClient.Post(new RequestHelper
                {
                    Uri = "", // TODO
                    Headers = ClientDataStorage.GetJwtHeader(),
                    Params = new SerializedDictionary<string, string> { { "object_id", _interactable.Key } }
                });

            _likesCountText.text = data.likes_count.ToString();
            SetLikeButtonStatus(data.is_liked_by_me);
        }

        private void SetLikeButtonStatus(bool hasLiked)
        {
            _hasLikedStatus = hasLiked;
            _likeIndicatorObject.SetActive(_hasLikedStatus);
        }

        private void OnLikeClicked()
        {
            if (_interactable == null)
                return;

            var toggleLikeRequest = new RequestHelper
            {
                Uri = "", // TODO
                Headers = ClientDataStorage.GetJwtHeader(),
                Params = new Dictionary<string, string> { { "object_id", _interactable.Key } }
            };

            RestClient.Post(toggleLikeRequest).Then(toggleLikeResponse =>
            {
                if (toggleLikeResponse.StatusCode != 200)
                    return;
                var responseData = JsonUtility.FromJson<SuccessResponse<Empty>>(toggleLikeResponse.Text);
                if (!responseData.success)
                    return;
                SetLikeButtonStatus(!_hasLikedStatus);
            });
        }
    }
}