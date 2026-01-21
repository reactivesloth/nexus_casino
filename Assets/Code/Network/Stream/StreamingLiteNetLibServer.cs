using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Code.Network.PlayFlow;
using Code.Network.Stream.Data;
using Code.Network.Stream.Utility;
using LiteNetLib;
using LiteNetLib.Utils;
using PlayFlow;
using PlayFlow.SDK.Servers;
using PurrNet;
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
        [SerializeField] private bool debugLogs = true;

        private NetManager server;
        private bool _isRunning = false;

        private NetPacketProcessor packetProcessor;
        private NetDataWriter writer;

        private readonly Dictionary<int, List<NetPeer>> _slotsPeers = new();
        private readonly Dictionary<NetPeer, int> _connectedPeersSlots = new();
        private readonly Dictionary<int, StreamFrameData> _slotsLastFrame = new();

        public static string ServerAddress { get; set; }
        public static int ServerStreamPort { get; set; }

        private void Start()
        {
            FindServer();
        }
        
        private async void FindServer()
        {
            try
            {
                ServerList response = await PlayFlowLobby._apiClient.ListServersAsync(includeLaunching: true);
                Debug.Log($"Found {response.total_servers} total servers.");

                foreach (var server in response.servers)
                {
                    Debug.Log($"- Server: {server.name}, Status: {server.status}");
                    if (server.status == "running" && server.version_tag == Application.version)
                    {
                        if (server.network_ports[0].host == PlayerPrefs.GetString("PlayFlow_IP"))
                        {
                            ServerAddress = server.network_ports[1].host;
                            ServerStreamPort = server.network_ports[1].external_port;
                        }
                    }
                }
            }
            catch (PlayFlowApiException e)
            {
                Debug.LogError($"Failed to list servers: {e.Message}");
            }
        }

        public static StreamingLiteNetLibServer Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

#if UNITY_SERVER
            StartServer();
#endif
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
            packetProcessor.SubscribeReusable<StreamFrameChunkData, NetPeer>(OnFrameChunkReceived);
            packetProcessor.SubscribeReusable<SlotConnectionData, NetPeer>(OnSlotConnected);

            server = new NetManager(this, null)
            {
                AutoRecycle = true,
                PacketPoolSize = 50_000,
                UseNativeSockets = true
            };

            server.Start(port);
        }

        private void OnFrameReceived(StreamFrameData frameData, NetPeer peer)
        {
            _slotsLastFrame[frameData.SlotId] = frameData;
            RetranslateFrame(frameData, peer);
        }
        
        private void OnFrameChunkReceived(StreamFrameChunkData chunkData, NetPeer peer)
        {
            RetranslateChunk(chunkData, peer);
        }

        private void OnSlotConnected(SlotConnectionData data, NetPeer peer)
        {
            if(!_slotsPeers.TryGetValue(data.SlotId, out var peers))
                _slotsPeers[data.SlotId] = new List<NetPeer>();
            _slotsPeers[data.SlotId].Add(peer);

            _connectedPeersSlots[peer] = data.SlotId;
            
            writer.Reset();
            packetProcessor.Write(writer, data);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            
            if(_slotsLastFrame.TryGetValue(data.SlotId, out var lastFrame))
                RetranslateFrame(lastFrame, peer);
        }
        
        private void RetranslateFrame(StreamFrameData frameData, NetPeer senderPeer)
        {
            if(!_slotsPeers.TryGetValue(frameData.SlotId, out var peers))
                return;
            
            foreach (var peer in peers)
            {
                if (Equals(peer, senderPeer))
                    continue;
                
                writer.Reset();
                packetProcessor.Write(writer, frameData);
                peer.Send(writer, DeliveryMethod.ReliableUnordered);
            }
        }   

        private void RetranslateFrame(List<StreamFrameChunkData> frameData, int streamerId, int slotId)
        {
            // TODO: If need
        }

        private void RetranslateChunk(StreamFrameChunkData chunkData, NetPeer senderPeer)
        {
            if(!_slotsPeers.TryGetValue(chunkData.SlotId, out var peers))
                return;
            
            foreach (var peer in peers)
            {
                if (Equals(peer, senderPeer))
                    continue;
                
                writer.Reset();
                packetProcessor.Write(writer, chunkData);
                peer.Send(writer, DeliveryMethod.ReliableUnordered);
            }
        }
        
        #region INetEventListener

        public void OnPeerConnected(NetPeer peer)
        {
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (_connectedPeersSlots.TryGetValue(peer, out var slotId))
            {
                _slotsPeers[slotId]?.Remove(peer);
            }
            
            _connectedPeersSlots.Remove(peer);
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