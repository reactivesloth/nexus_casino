using System;
using System.Collections;
using System.IO;
using Code.UI;
using Newtonsoft.Json;
using PlayFlow.SDK.Servers;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;
using PlayerSpawner = Code.Network.Player.PlayerSpawner;

namespace Code.Network.PlayFlow
{
    public class PlayFlowManager : MonoBehaviour
    {
        [SerializeField] private string playflowApiKey = "YOUR_API_KEY_HERE";
        [SerializeField] private float emptyServerLifeTime = 600f;
        
        public static PlayflowServerApiClient ApiClient;
        
        private float _emptyTime;
        
        private void Start()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();

            InstanceHandler.NetworkManager.onPlayerJoined += OnPlayerJoined;
            InstanceHandler.NetworkManager.onPlayerLeftScene += OnPlayerLeft;
            InstanceHandler.NetworkManager.Subscribe<ServerLog>(HandleServerCustomData);
            
            ApiClient = new PlayflowServerApiClient(playflowApiKey);
            
#if UNITY_SERVER
            transport.address = "";
            transport.serverPort = 7770;
            transport.StartServer();
#else
            StartCoroutine(SpawnPlayer());
#endif            
        }

        private IEnumerator SpawnPlayer()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();
            transport.address = PlayerPrefs.GetString("PlayFlow_IP", "127.0.0.1");
            transport.serverPort = ushort.Parse(PlayerPrefs.GetString("PlayFlow_Port", "7770"));
            
            LoadingScreenUI.Instance.Show("loading.start_scene", "loading.please_wait");
            
            yield return new WaitForSeconds(5f);
            
            transport.StartClient();
            
            yield return new WaitUntil(() => InstanceHandler.NetworkManager.clientState == ConnectionState.Connected);
            Debug.Log("Success!");
            FindAnyObjectByType<PlayerSpawner>().SpawnPlayer();
            
            yield return new WaitForSeconds(2f);
            
            LoadingScreenUI.Instance.Hide();
        }

        private void Update ()
        {
#if UNITY_SERVER
            UpdateTimer();
#endif
        }

        private void UpdateTimer()
        {
            var playerCount = InstanceHandler.NetworkManager.playerCount;

            if (playerCount > 0)
            {
                _emptyTime = 0f;
                return;
            }

            _emptyTime += Time.deltaTime;

            if (_emptyTime >= emptyServerLifeTime)
            {
                InstanceHandler.NetworkManager.StopServer();
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }
        
                
        
        private void OnPlayerJoined(PlayerID player, bool isReconnect, bool asServer)
        {
            if(!asServer)
                return;
            UpdateServerPlayerCount();
        }
        
        private void OnPlayerLeft(PlayerID player, SceneID scene, bool asServer)
        {
            if(!asServer)
                return;
            UpdateServerPlayerCount();
        }
        
        [ServerOnly]
        private async void UpdateServerPlayerCount()
        {
            var playFlowJsonFile = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "playflow.json");
            InstanceHandler.NetworkManager.SendToAll(new ServerLog{Message = playFlowJsonFile});
            if(!File.Exists(playFlowJsonFile))
            {
                InstanceHandler.NetworkManager.SendToAll(new ServerLog{Message = "File Not Exits"});
                return;
            }
            var playFlowJson = await File.ReadAllTextAsync(playFlowJsonFile);
            var serverData = JsonConvert.DeserializeObject<InstanceData>(playFlowJson);

            var instanceId = serverData.instance_id;
            var customData = serverData.custom_data;
            customData["players_count"] = InstanceHandler.NetworkManager.playerCount;

            try
            {
                await ApiClient.UpdateServerAsync(instanceId, customData);
                InstanceHandler.NetworkManager.SendToAll(new ServerLog{Message = "Lobby Updated"});
            }
            catch (PlayFlowApiException e)
            {
                InstanceHandler.NetworkManager.SendToAll(new ServerLog{Message = $"UpdateError: {e.Message}"});
            }
            
        }

        private void HandleServerCustomData(PlayerID sender, ServerLog msg, bool asServer)
        {
            Debug.Log(msg.Message);
        }
    }

    public struct ServerLog : IPackedAuto
    {
        public string Message;
    }
}
