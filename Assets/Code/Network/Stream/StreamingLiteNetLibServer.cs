using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Code.Network.Stream.Data;
using LiteNetLib;
using FishNet;
using FishNet.Transporting.Tugboat;
using LiteNetLib.Utils;
using PlayFlow;
using UnityEngine;

namespace Code.Network.Stream
{
    /// <summary>
    /// LiteNetLib сервер для приёма стримов от стримеров и ретрансляции зрителям.
    /// Запускается на центральном сервере вместе с FishNet.
    /// </summary>
    public sealed class StreamingLiteNetLibServer : MonoBehaviour, INetEventListener
    {
        [SerializeField] private int port = 7777;
        [SerializeField] private int maxClients = 100;
        [SerializeField] private bool debugLogs = true;
        
        // Настройки буферизации
        [SerializeField] private int maxFramesPerSlot = 3; // Максимум кадров в буфере для каждого слота
        [SerializeField] private float frameDropCheckInterval = 1f; // Интервал проверки старых кадров

        private NetManager server;
        private bool _isRunning = false;

        private NetPacketProcessor packetProcessor;
        private NetDataWriter writer;

        private readonly Dictionary<NetPeer, int> clients = new Dictionary<NetPeer, int>();
        
        public static string ServerAddress
        {
            get
            {
                var tugboat = InstanceFinder.TransportManager.Transport as Tugboat;
                if (tugboat == null)
                    return string.Empty;
                var addr = tugboat.GetClientAddress();
                return addr;
            }
        }

        public static int ServerStreamPort
        {
            get
            {
                var internalPort = FindAnyObjectByType<StreamingLiteNetLibServer>().port;
                var lobby = PlayFlowLobbyManagerV2.Instance.CurrentLobby;
                if (lobby == null)
                    return internalPort;
                if (!lobby.TryGetPortMapping(internalPort, out var portMapping))
                    return internalPort;
                return portMapping.ExternalPort;
            }
        }

        private void Awake()
        {
            StartServer();
        }

        private void Update()
        {
            server?.PollEvents();
        }

        private void StartServer()
        {
            packetProcessor = new NetPacketProcessor();
            writer = new NetDataWriter();

            packetProcessor.SubscribeReusable<StreamFrameData, NetPeer>(OnFrameReceived);
            packetProcessor.SubscribeReusable<PlayerConnectionData, NetPeer>(OnClientConnected);

            server = new NetManager(this, null)
            {
                AutoRecycle = true,
                PacketPoolSize = 10_000,
                PingInterval = 1000,
                UpdateTime = 5,
                UseNativeSockets = true
            };

            server.Start(port);
        }

        private void StopServer()
        {
        }

        private void OnFrameReceived(StreamFrameData data, NetPeer peer)
        {
            AddFrameToBuffer(data);
        }

        private void OnClientConnected(PlayerConnectionData data, NetPeer peer)
        {
            clients[peer] = data.PlayerId;
            
            writer.Reset();
            packetProcessor.Write(writer, data);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Добавляет кадр в буфер слота с проверкой максимального размера буфера
        /// </summary>
        private void AddFrameToBuffer(StreamFrameData data)
        {
            RetranslateFrame(data);
        }

        private void RetranslateFrame(StreamFrameData data)
        {
            foreach (var client in clients)
            {
                /*// Не отправляем стримеру обратно 
                if (client.Value == data.StreamerId)
                    return;*/

                writer.Reset();
                packetProcessor.Write(writer, data);
                client.Key.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        #region INetEventListener

        public void OnPeerConnected(NetPeer peer)
        {
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (clients.ContainsKey(peer))
                clients.Remove(peer);
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
            DeliveryMethod deliveryMethod)
        {
            packetProcessor.ReadAllPackets(reader, peer);
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
            UnconnectedMessageType messageType)
        {
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            request.Accept();
        }

        #endregion
    }
}