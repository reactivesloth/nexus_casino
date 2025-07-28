using System;
using System.Collections.Generic;
using Code.Network.Lobby;
using Code.Utility;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Stories
{
    public class LocalStoriesStorage : NetworkBehaviour
    {
        public static LocalStoriesStorage Instance { get; private set; }
        
        private readonly SyncList<Story> _stories = new(new SyncTypeSettings()
        {
            WritePermission = WritePermission.ClientUnsynchronized,
            ReadPermission = ReadPermission.Observers
        });

        public event Action<List<Story>> StoriesUpdated;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            _stories.OnChange += StoriesOnOnChange;
        }

        private void OnDisable()
        {
            _stories.OnChange -= StoriesOnOnChange;
        }

        public List<Story> GetStories(int count = 30, int automatId = -1)
        {
            var result = new List<Story>();

            for (int i = _stories.Count - 1; i >= 0 && result.Count < count; i--)
            {
                var story = _stories[i];
                if (automatId < 0 || story.automatId == automatId)
                {
                    result.Add(story);
                }
            }

            return result;
        }

        public void ScreenshotMake(byte[] screenshotBytes, int automatId = 0)
        {
            var story = new Story
            {
                playerName = LobbyVariables.Instance.displayName,
                automatId = automatId,
                lobbyId = LobbyVariables.Instance.currentLobby.lobbyId,
                screenshotBytes = screenshotBytes
            };
            
            _stories.Add(story);
        }
        
        private void StoriesOnOnChange(SyncListOperation op, int index, Story oldItem, Story newItem, bool asServer)
        {
            Debug.Log($"NEW STORY! Info: {newItem}");
            StoriesUpdated?.Invoke(_stories.Collection);
        }
    }

    [Serializable]
    public struct Story: IBroadcast
    {
        public string playerName;
        public int automatId;
        public string lobbyId;
        public byte[] screenshotBytes;

        public Sprite ScreenshotSprite => ImageByteConverter.CreateSpriteFromBytes(screenshotBytes);

        public override string ToString()
        {
            return $"PlayerName is {playerName}, automatId is {automatId}";
        }
    }
}