using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Code.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        [Header("Local Test"), SerializeField] private bool localTestMode = false;
        [SerializeField] private bool asServer = false;

        public static PlayflowServerApiClient ApiClient;
        public static InstanceData CurrentServerData;

        private float _emptyTime;

        private void Start()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();
            InstanceHandler.NetworkManager.Subscribe<ChangeServerInfo>(HandleServerCustomData);

            if (!localTestMode)
                StartProd(transport);
            else
                StartLocalTestMode(transport);
        }

        private void StartProd(UDPTransport transport)
        {
            ApiClient = new PlayflowServerApiClient(playflowApiKey);

#if UNITY_SERVER
            transport.address = "";
            transport.serverPort = 7770;
            transport.StartServer();
#else
            StartCoroutine(ConnectAndSpawnPlayer(PlayerPrefs.GetString("PlayFlow_IP", "127.0.0.1"),
                ushort.Parse(PlayerPrefs.GetString("PlayFlow_Port", "7770"))));
#endif
        }

        private void StartLocalTestMode(UDPTransport transport)
        {
            if (asServer)
            {
                transport.address = "";
                transport.serverPort = 7770;
                transport.StartServer();
            }
            
            StartCoroutine(ConnectAndSpawnPlayer());
        }

        private IEnumerator ConnectAndSpawnPlayer(string ip = "127.0.0.1", ushort port = 7770)
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();
            transport.address = ip;
            transport.serverPort = port;

            if (LoadingScreenUI.Instance != null)
            {
                LoadingScreenUI.Instance.Show("loading.start_scene", "loading.please_wait");

                yield return new WaitForSeconds(5f);

                transport.StartClient();

                yield return new WaitUntil(() =>
                    InstanceHandler.NetworkManager.clientState == ConnectionState.Connected);
                Debug.Log("Success!");
                FindAnyObjectByType<PlayerSpawner>().SpawnPlayer();

                yield return new WaitForSeconds(2f);

                LoadingScreenUI.Instance.Hide();
            }
        }

        private void Update()
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

        private void HandleServerCustomData(PlayerID sender, ChangeServerInfo info, bool asServer)
        {
            if (asServer)
                return;

            CurrentServerData = JsonConvert.DeserializeObject<InstanceData>(info.NewServerDataString);

            if (CurrentServerData.custom_data.TryGetValue("players", out var players))
            {
                // players, скорее всего, JArray
                if (players is JArray jarr)
                {
                    var playerList = jarr.ToObject<List<string>>();
                    Debug.Log(playerList.Count);
                }
                else
                {
                    Debug.LogError($"Unexpected type: {players?.GetType()}");
                }
            }
        }

        private static readonly object _lock = new();
        private static Task _lastTask = Task.CompletedTask;

        [ServerOnly]
        public static void UpdateSeverData(params (string, object)[] data)
        {
            lock (_lock)
            {
                _lastTask = _lastTask.ContinueWith(
                    _ => UpdateSeverData_Internal(data),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default
                ).Unwrap();
            }
        }

        [ServerOnly]
        private static async Task UpdateSeverData_Internal(params (string, object)[] data)
        {
            if (CurrentServerData == null)
                AssignServerDataToServer();
            if (CurrentServerData == null)
            {
                Debug.LogError("Server data is null");
                return;
            }

            if (ApiClient == null)
                return;

            var instanceId = CurrentServerData.instance_id;
            var customData = CurrentServerData.custom_data;

            for (var i = 0; i < data.Length; i++)
            {
                customData[data[i].Item1] = data[i].Item2;
            }

            try
            {
                var newData = await ApiClient.UpdateServerAsync(
                    instanceId,
                    new CustomDataPostWrapper { custom_data = customData }
                ).ConfigureAwait(false);

                InstanceHandler.NetworkManager.SendToAll(
                    new ChangeServerInfo { NewServerDataString = JsonConvert.SerializeObject(newData) }
                );
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError(e.Message);
            }
        }

        [ServerOnly]
        private static void AssignServerDataToServer()
        {
            var playFlowJsonFile =
                Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, "playflow.json");
            if (!File.Exists(playFlowJsonFile))
            {
                return;
            }

            var playFlowJson = File.ReadAllText(playFlowJsonFile);
            CurrentServerData = JsonConvert.DeserializeObject<InstanceData>(playFlowJson);
        }
    }

    public struct ChangeServerInfo : IPackedAuto
    {
        public string NewServerDataString;
    }
}