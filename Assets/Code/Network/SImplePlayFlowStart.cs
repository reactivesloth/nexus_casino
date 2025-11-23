using FishNet;
using UnityEngine;

public class SimplePlayFlowStart : MonoBehaviour
{
    public string serverIp = "127.0.0.1";    // IP сервера (локальный или PlayFlow IP)
    public ushort serverPort = 7777;         // Порт сервера

    void Start() 
    {
#if UNITY_SERVER
        // Запуск в режиме сервера (локально или на хосте)
        InstanceFinder.NetworkManager.ServerManager.StartConnection();
        Debug.Log("Сервер запущен");
#endif
       
#if !UNITY_SERVER
        // Клиент подключается к серверу напрямую без лобби
        InstanceFinder.NetworkManager.ClientManager.StartConnection(serverIp, serverPort);
        Debug.Log($"Клиент подключается к {serverIp}:{serverPort}");
#endif
    }
}