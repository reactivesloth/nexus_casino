using System;
using Code.API.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Chat
{
    public class MessageComponent : MonoBehaviour
    {
        [SerializeField] private Sprite defaultAvatar;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Button openProfileButton;
        
        [SerializeField] private TextMeshProUGUI nicknameText;
        [SerializeField] private TextMeshProUGUI messageText;
        
        [SerializeField] private GameObject socialPanel;
        [SerializeField] private TextMeshProUGUI viewsCountText;
        [SerializeField] private TextMeshProUGUI likesCountText;
        [Header("Like Button")]
        [SerializeField] private Button likeButton;
        [SerializeField] private GameObject likeByMeIndicator;

        private MessageData _chatMessageData;
        private ChatController _chatController;
        
        private void Awake()
        {
            _chatController ??= FindAnyObjectByType<ChatController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (likeButton != null)
                likeButton.onClick.AddListener(OnLikeClicked);
            if (openProfileButton != null)
                openProfileButton.onClick.AddListener(OnProfileClicked);
        }

        private void OnDisable()
        {
            if(likeButton != null)
                likeButton.onClick.RemoveListener(OnLikeClicked);
            if (openProfileButton != null)
                openProfileButton.onClick.RemoveListener(OnProfileClicked);
        }

        public void Init(ChatMessage message)
        {
            ResetMessage();
            
            _chatMessageData = message.chatMessageData;
            
            nicknameText.text = message.style.FormatUsername(message.displayUsername);
            messageText.text = message.style.FormatMessageText(message.displayMessage);

            InitSocial();
        }

        public void UpdateLikesStatus(int likesCount, bool isLikedByMe)
        {
            if(likesCountText == null)
                return;
            
            _chatMessageData.likes_count = likesCount;
            _chatMessageData.is_liked_by_me = isLikedByMe;
            
            likesCountText.text = likesCount.ToString();
            _chatMessageData.is_liked_by_me = isLikedByMe;
            likeByMeIndicator.SetActive(_chatMessageData.is_liked_by_me);
        }

        public void UpdateViewsStatus(int viewsCount, bool isViewedByMe)
        {
            if(viewsCountText == null)
                return;
            
            _chatMessageData.views_count = viewsCount;
            _chatMessageData.is_viewed_by_me = isViewedByMe;
            
            viewsCountText.text = viewsCount.ToString();
            _chatMessageData.is_viewed_by_me = isViewedByMe;
        }

        private void InitSocial()
        {
            if(_chatMessageData == null || socialPanel == null)
                return;
            
            socialPanel.SetActive(true);
            
            UpdateViewsStatus(_chatMessageData.views_count, _chatMessageData.is_viewed_by_me);
            UpdateLikesStatus(_chatMessageData.likes_count, _chatMessageData.is_liked_by_me);
            
            if(!_chatMessageData.is_viewed_by_me)
                _chatController.OnViewMessage(_chatMessageData.id);
            
            LoadAvatar();
        }

        private void LoadAvatar()
        {
            if(avatarImage == null)
                return;
            
            // TODO: avatar load
        }

        private void ResetMessage()
        {
            nicknameText.text = string.Empty;
            messageText.text = string.Empty;
            
            if(socialPanel != null)
                socialPanel.SetActive(false);
            
            if(avatarImage != null)
                avatarImage.sprite = defaultAvatar;
            if(viewsCountText != null)
                viewsCountText.text = string.Empty;
            if(likesCountText != null)
                likesCountText.text = string.Empty;
            if(likeByMeIndicator != null)
                likeByMeIndicator.SetActive(false);
            
            _chatMessageData = null;
        }

        private void OnLikeClicked()
        {
            if(_chatController == null || _chatMessageData == null)
                return;
            
            _chatController.OnLikeMessage(_chatMessageData.id, !_chatMessageData.is_liked_by_me);
        }

        private void OnProfileClicked()
        {
            // TODO: Init and open profile when done
        }
    }
}
