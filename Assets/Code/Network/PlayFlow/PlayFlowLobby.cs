using System.Collections.Generic;
using UnityEngine;
using PlayFlow.SDK.Servers;

namespace Code.Network.PlayFlow
{
    public class PlayFlowLobby : MonoBehaviour
    {
        public string playflowApiKey = "YOUR_API_KEY_HERE"; 

        private PlayflowServerApiClient _apiClient;
        
        void Start()
        {
            PlayerPrefs.SetString("PlayFlow_IP", "137.66.29.230");
            PlayerPrefs.SetString("PlayFlow_Port", "7426");
            _apiClient = new PlayflowServerApiClient(playflowApiKey);
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
        
        private async void ListAllServers()
        {
            try
            {
                ServerList response = await _apiClient.ListServersAsync(includeLaunching: true);
                Debug.Log($"Found {response.total_servers} total servers.");

                foreach (var server in response.servers)
                {
                    Debug.Log($"- Server: {server.name}, Status: {server.status}");
                }
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to list servers: {e.Message}");
            }
        }
    }
}