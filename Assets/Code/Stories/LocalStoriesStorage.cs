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
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        public event Action<List<Story>> StoriesUpdated;

        public List<Story> Stories => _stories?.Collection;

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

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.RegisterBroadcast<Story>(HandlerNewStory);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.UnregisterBroadcast<Story>(HandlerNewStory);
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
            
            ClientManager.Broadcast(story);
        }
        
        private void StoriesOnOnChange(SyncListOperation op, int index, Story oldItem, Story newItem, bool asServer)
        {
            StoriesUpdated?.Invoke(_stories.Collection);
        }
        
        private void HandlerNewStory(NetworkConnection con, Story storyData, Channel channel)
        {
            _stories.Add(storyData);
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
    }
}