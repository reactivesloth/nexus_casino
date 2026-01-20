using PurrNet;
using UnityEngine;

namespace Code.Network.PlayFlow
{
    public class PlayFlowManager : MonoBehaviour
    {
        private void Awake()
        {
            var transport = InstanceHandler.NetworkManager.GetComponent<PurrNet.Transports.UDPTransport>();

#if UNITY_SERVER
            transport.adress = "";
            transport.serverPort = 7770;
            transport.StartServer();
#else

            transport.address = PlayerPrefs.GetString("PlayFlow_IP", "127.0.0.1");
            transport.serverPort = ushort.Parse(PlayerPrefs.GetString("PlayFlow_Port", "7770"));
            transport.StartClient();
#endif            
        }

        private void Update ()
        {
            //TODO: Check players and disconnect after 10 mins if no players
#if UNITY_SERVER

#endif
        }
    }
}
