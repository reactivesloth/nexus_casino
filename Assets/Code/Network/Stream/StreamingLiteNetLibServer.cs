using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Code.InteractionSystem;
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
        private record PlayerData
        {
            public PlayerConnectionData Data { get; set; }
            public NetPeer Peer { get; set; }
        }

        [SerializeField] private int port = 7777;
        [SerializeField] private int maxClients = 100;
        [SerializeField] private bool debugLogs = true;

        private NetManager server;
        private bool _isRunning = false;

        private NetPacketProcessor packetProcessor;
        private NetDataWriter writer;

        private readonly Dictionary<int, PlayerData> _clients = new();
        private readonly Dictionary<int, SlotMachineInteractable> _slots = new();
        private readonly Dictionary<int, StreamFrameData> _slotsLastFrame = new();

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
                var internalPort = Instance.port;
                var lobby = PlayFlowLobbyManagerV2.Instance.CurrentLobby;
                if (lobby == null)
                    return internalPort;
                return !lobby.TryGetPortMapping(internalPort, out var portMapping)
                    ? internalPort
                    : portMapping.ExternalPort;
            }
        }

        public static StreamingLiteNetLibServer Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

#if UNITY_SERVER
            StartServer();
#endif
            foreach (var slot in FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                _slots.Add(slot.IDNumber, slot);
        }

        private void Update()
        {
            server?.PollEvents();
        }
        
        public void OnPlayerObserverSlot(int playerId, int slotId)
        {
            var frameData = _slotsLastFrame[slotId];
            var playerData = _clients[playerId];
            
            if(frameData == null || playerData == null)
                return;
            
            writer.Reset();
            packetProcessor.Write(writer, frameData);
            playerData.Peer?.Send(writer, DeliveryMethod.ReliableUnordered);
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

        private void OnFrameReceived(StreamFrameData data, NetPeer peer)
        {
            _slotsLastFrame[data.SlotId] = data;
            
            RetranslateFrame(data);
        }

        private void OnClientConnected(PlayerConnectionData data, NetPeer peer)
        {
            _clients[data.PlayerId] = new PlayerData{ Data = data, Peer = peer };

            writer.Reset();
            packetProcessor.Write(writer, data);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
        
        private void RetranslateFrame(StreamFrameData frameData)
        {
            foreach (var (id, playerData) in _clients)
            {
                if (playerData.Data.PlayerId == frameData.StreamerId)
                    continue;
                
                var slotObserversIds = _slots[frameData.SlotId].Observers.Select(o => o.ClientId)
                    .ToArray();
                if(slotObserversIds.Length == 0 || !slotObserversIds.Contains(playerData.Data.PlayerId))
                    continue;

                writer.Reset();
                packetProcessor.Write(writer, frameData);
                playerData.Peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        #region INetEventListener

        public void OnPeerConnected(NetPeer peer)
        {
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var recordForRemove = _clients.FirstOrDefault(x => Equals(x.Value.Peer, peer));
            _clients.Remove(recordForRemove.Key);
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
            request.AcceptIfKey("stream_peer");
        }

        #endregion
    }
}