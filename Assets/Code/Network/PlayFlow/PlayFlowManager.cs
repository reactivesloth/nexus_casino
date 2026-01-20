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
        private void Awake()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<UDPTransport>();

#if UNITY_SERVER
            transport.address = "";
            transport.serverPort = 7770;
            transport.StartServer();
#else
            LoadingScreenUI.Instance.Show("loading.start_scene", "loading.please_wait");
            transport.address = PlayerPrefs.GetString("PlayFlow_IP", "127.0.0.1");
            transport.serverPort = ushort.Parse(PlayerPrefs.GetString("PlayFlow_Port", "7770"));
            transport.StartClient();
            
            StartCoroutine(SpawnPlayer());
#endif            
        }

        private IEnumerator SpawnPlayer()
        {
            yield return new WaitUntil(() => InstanceHandler.NetworkManager.clientState == ConnectionState.Connected);
            Debug.Log("Success!");
            FindAnyObjectByType<PlayerSpawner>().SpawnPlayer();
            LoadingScreenUI.Instance.Hide();
        }

        private void Update ()
        {
            //TODO: Check players and disconnect after 10 mins if no players
#if UNITY_SERVER

#endif
        }
    }
}
