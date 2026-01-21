using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using PlayFlow.SDK.Servers;

namespace Code.Network.PlayFlow
{
    public class PlayFlowLobby : MonoBehaviour
    {
        public string playflowApiKey = "YOUR_API_KEY_HERE"; 

        private PlayflowServerApiClient _apiClient;
        public bool CanConnect { get; private set; }
        
        void Start()
        {
            _apiClient = new PlayflowServerApiClient(playflowApiKey);
            
            FindServer();
        }
        
        private async void StartNewServer()
        {
            var serverRequest = new ServerCreateRequest
            {
                name = "MyCustomServer",
                region = "eu-west",
                custom_data = new Dictionary<string, object>
                {
                    { "map_name", "castle_siege" }
                }
            };

            try
            {
                ServerStartResponse response = await _apiClient.StartServerAsync(serverRequest);
                Debug.Log($"Server is starting! Instance ID: {response.instance_id}");
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to start server: {e.Message}");
            }
        }
        
        private async void StopServer(string instanceId)
        {
            try
            {
                ServerStopResponse response = await _apiClient.StopServerAsync(instanceId);
                Debug.Log($"Server stop initiated. Status: {response.status}");
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to stop server: {e.Message}");
            }
        }

        public async void FindServer()
        {
            CanConnect = false;
            
            try
            {
                ServerList response = await _apiClient.ListServersAsync(includeLaunching: true);
                Debug.Log($"Found {response.total_servers} total servers.");

                foreach (var server in response.servers)
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