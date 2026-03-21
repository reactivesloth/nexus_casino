using System.Collections;
using Code.UI;
using PurrNet;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;
using PlayerSpawner = Code.Network.Player.PlayerSpawner;

namespace Code.Network.Server
{
    public class ServerManager : MonoBehaviour
    {
        [Header("Local Test"), SerializeField] private bool localTestMode = false;
        [SerializeField] private bool asServer = false;
        
        private float _emptyTime;
        private float emptyServerLifeTime = 600;

        private void Start()
        {
            InstanceHandler.NetworkManager.Subscribe<ChangeServerInfo>(HandleServerCustomData);
            ConnectToServer();
        }

        private void HandleServerCustomData(PlayerID player, ChangeServerInfo data, bool b)
        {
            
        }

        private void ConnectToServer ()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();
            
            if (!localTestMode)
                StartProd(transport);
            else
                StartLocalTestMode(transport);
        }

        private void StartProd(UDPTransport transport)
        {
#if UNITY_SERVER
            transport.address = "";
            transport.serverPort = 7770;
            transport.StartServer();
#else
            StartCoroutine(ConnectAndSpawnPlayer(PlayerPrefs.GetString("Server_IP", "127.0.0.1"),
                ushort.Parse(PlayerPrefs.GetString("Server_Port", "7770"))));
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
    }

    public struct ChangeServerInfo : IPackedAuto
    {
        public string NewServerDataString;
    }
}