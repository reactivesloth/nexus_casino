using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using PlayFlow.SDK.Servers;
using Random = UnityEngine.Random;

namespace Code.Network.PlayFlow
{
    public class PlayFlowLobby : MonoBehaviour
    {
        public string playflowApiKey = "YOUR_API_KEY_HERE";
        public static PlayflowServerApiClient _apiClient;
        public bool CanConnect { get; private set; } = false;

        [Header("Для ручного ввода")]
        [SerializeField] private string ip;
        [SerializeField] private string port;

        void Start()
        {
            _apiClient = new PlayflowServerApiClient(playflowApiKey);

            FindServer();
        }

        [ContextMenu("SetAddress")]
        private void SetAddress()
        {
            PlayerPrefs.SetString("PlayFlow_IP", ip);
            PlayerPrefs.SetString("PlayFlow_Port", port);
            CanConnect = true;
        }

        private async void StartNewServer()
        {
            var serverRequest = new ServerCreateRequest
            {
                name = $"Server {Random.Range(0, 10_000)}",
                region = "eu-west",
                compute_size = "small",
                version_tag = Application.version
            };

            try
            {
                var response = await _apiClient.StartServerAsync(serverRequest);

                WaitForServer(response);
                
                Debug.Log($"Server is starting! Instance ID: {response.instance_id}");
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to start server: {e.Message}");
            }
        }

        private async void WaitForServer(ServerStartResponse serverStats)
        {
            CanConnect = false;
            
            while (!CanConnect)
            {
                await Task.Delay(1000);
                try
                {
                    var serverData = await _apiClient.GetServerDetailsAsync(serverStats.instance_id);
                    CanConnect = serverData.status == "running";
                    if (CanConnect)
                    {
                        Debug.Log($"Server is running! Address: {serverData.network_ports[0].host}:{serverData.network_ports[0].external_port}");
                        PlayerPrefs.SetString("PlayFlow_IP", serverData.network_ports[0].host);
                        PlayerPrefs.SetString("PlayFlow_Port", serverData.network_ports[0].external_port.ToString());
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to get server details: {e.Message}");
                    throw;
                }
            }
        }

        public async void FindServer()
        {
            CanConnect = false;

            try
            {
                ServerList response = await _apiClient.ListServersAsync(includeLaunching: true);
                Debug.Log($"Found {response.total_servers} total servers.");

                // Server Filter
                var availableServers =
                    response.servers.Where(s => s.status == "running" && s.version_tag == Application.version).ToList();

                if (availableServers.Count == 0)
                {
                    StartNewServer();
                    return;
                }

                foreach (var server in availableServers)
                {
                    Debug.Log($"- Server: {server.name}, Status: {server.status}");
                    if (server.status == "running" && server.version_tag == Application.version)
                    {
                        PlayerPrefs.SetString("PlayFlow_IP", server.network_ports[0].host);
                        PlayerPrefs.SetString("PlayFlow_Port", server.network_ports[0].external_port.ToString());
                        CanConnect = true;
                    }
                }
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to list servers: {e.Message}");
            }
        }
    }
}