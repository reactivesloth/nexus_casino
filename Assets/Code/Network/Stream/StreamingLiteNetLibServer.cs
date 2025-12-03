using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Code.InteractionSystem;
using Code.Network.Stream.Data;
using LiteNetLib;
using FishNet;
using FishNet.Transporting;
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
            //InstanceFinder.ServerManager.OnServerConnectionState += OnServerConnectionState;
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
                DisconnectTimeout = 5_000_000,
            };

            server.Start(port);
        }

        private void StopServer()
        {
        }

        private void OnFrameReceived(StreamFrameData data, NetPeer peer)
        {
            RetranslateFrame(data);
        }

        private void OnClientConnected(PlayerConnectionData data, NetPeer peer)
        {
            clients[peer] = data.PlayerId;
        }

        private void RetranslateFrame(StreamFrameData data)
        {
            // ПОлучаем id наблюдателей за автоматом №data.SlotId
            var slotObserverController =
                SlotMachineInteractable.FindById(data.SlotId).Observers.Select(nc => nc.ClientId);

            // Выбираем пиров по id для отправки
            var clientsForSent = clients.Where(c => 
                slotObserverController.Contains(c.Value));

            foreach (var client in clientsForSent)
            {
                // Не отправляем стримеру обратно 
                if (client.Value == data.StreamerId)
                    return;

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