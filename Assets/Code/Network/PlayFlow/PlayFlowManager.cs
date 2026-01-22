using System.Collections;
using Code.UI;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using PlayerSpawner = Code.Network.Player.PlayerSpawner;

namespace Code.Network.PlayFlow
{
    public class PlayFlowManager : MonoBehaviour
    {
        [SerializeField] private float emptyServerLifeTime = 600f;

        private float _emptyTime;
        
        private void Start()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();

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
    }
}
