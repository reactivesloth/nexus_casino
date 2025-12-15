using System;
using System.Net;
using System.Net.Sockets;
using Code.Network.Stream.Data;
using Code.Network.Stream.Utility;
using LiteNetLib;
using LiteNetLib.Utils;
using FishNet;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network.Stream
{
    /// <summary>
    /// LiteNetLib клиент для отправки и приёма стримов.
    /// Объединяет функциональность отправителя и получателя в один класс.
    /// </summary>
    public sealed class StreamingLiteNetLibPeer : MonoBehaviour, INetEventListener
    {
        [SerializeField] private int serverPort = 7777;
        [SerializeField] private float timeToReconnect = 60f;
        [SerializeField] private int unreliableFramesPerReliable = 10;

        [SerializeField] private bool debugLogs = false;

        [SerializeField] private int debugPing = -1;

        private int _unreliableFramesCounter = 0;

        private NetManager _client;
        private NetPeer _server;

        public bool IsConnected { get; private set; }

        private NetPacketProcessor _packetProcessor;
        private NetDataWriter _writer;

        public event Action<StreamFrameData> OnFrameReceived;

        public static StreamingLiteNetLibPeer Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void Update()
        {
            _client?.PollEvents();
            debugPing = _server is { ConnectionState: ConnectionState.Connected } ? _server.Ping : -1;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Invoke(nameof(Connect), 1f);
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Подключиться к серверу стриминга.
        /// </summary>
        public void Connect()
        {
            if (InstanceFinder.ClientManager.Connection.ClientId < 0)
            {
                Invoke(nameof(Connect), 1f);
                return;
            }

            _packetProcessor = new NetPacketProcessor();
            _writer = new NetDataWriter();

            _packetProcessor.SubscribeReusable<StreamFrameData>(OnFrameReceive);
            _packetProcessor.SubscribeReusable<StreamFrameChunkData>(OnFrameChunkReceived);
            _packetProcessor.SubscribeReusable<PlayerConnectionData>(OnClientConnected);

            _client = new NetManager(this, null)
            {
                AutoRecycle = true,
                PacketPoolSize = 10_000,
                PingInterval = 1000,
                UpdateTime = 5,
                UseNativeSockets = true,
            };

            _client.Start();
            _client.Connect(StreamingLiteNetLibServer.ServerAddress, StreamingLiteNetLibServer.ServerStreamPort,
                "stream_peer");

            Invoke(nameof(Reconnect), timeToReconnect);
            if (debugLogs)
                Debug.Log(
                    $"[StreamingLiteNetLibPeer] Connecting to {StreamingLiteNetLibServer.ServerAddress}:{StreamingLiteNetLibServer.ServerStreamPort}");
        }

        [ContextMenu("Reconnect")]
        public void Reconnect()
        {
            Disconnect();
            Connect();
        }

        /// <summary>
        /// Отключиться от сервера.
        /// </summary>
        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            if (_client == null)
                return;

            _client.DisconnectAll();
            _client.Stop();
        }

        /// <summary>
        /// Отправить фрейм стрима на сервер.
        /// </summary>
        public void SendStreamFrame(StreamFrameData frameData, bool forceReliable = false)
        {
            if (_server.ConnectionState != ConnectionState.Connected)
            {
                if (debugLogs)
                    Debug.LogWarning($"[StreamingLiteNetLibPeer] Connection state is {_server.ConnectionState}");
                return;
            }

            if (_unreliableFramesCounter < unreliableFramesPerReliable && !forceReliable)
            {
                _unreliableFramesCounter++;
                var chunks = FrameBuilder.GetFrameChunks(frameData,
                    _server.GetMaxSinglePacketSize(DeliveryMethod.Sequenced) - StreamFrameChunkData.HeaderSize);
                SendChunks(chunks);
            }
            else
            {
                _unreliableFramesCounter = 0;
                SendFullFrame(frameData);
            }

            if (debugLogs)
                Debug.Log($"[StreamingLiteNetLibPeer] Try send frame {frameData.Data.Length} bytes");
        }

        private void SendChunks(StreamFrameChunkData[] chunksData)
        {
            foreach (var chunk in chunksData)
            {
                if (debugLogs)
                    Debug.Log(
                        $"[StreamingLiteNetLibPeer] Try send chunk №{chunk.ChunkIndex} {chunk.Payload.Length} bytes");
                _writer.Reset();
                _packetProcessor.Write(_writer, chunk);
                _server.Send(_writer, DeliveryMethod.Sequenced);
            }
        }

        private void SendFullFrame(StreamFrameData frameData)
        {
            _writer.Reset();
            _packetProcessor.Write(_writer, frameData);
            _server.Send(_writer, DeliveryMethod.ReliableUnordered);
        }

        private void OnFrameChunkReceived(StreamFrameChunkData chunkData)
        {
            StreamFramesDataAccumulator.AddChunk(chunkData);
            var allChunksThisFrame = StreamFramesDataAccumulator.GetChunks(chunkData.SlotId, chunkData.FrameId);

            if (debugLogs)
                Debug.Log(
                    $"[StreamingLiteNetLibPeer] Received chunk from user {chunkData.StreamerId} slot {chunkData.SlotId} ({chunkData.ChunkIndex}/{chunkData.ChunkCount}) Chunks from storage {allChunksThisFrame.Count}");

            if (FrameBuilder.TryGetFullFrame(allChunksThisFrame, out var frame))
                OnFrameReceive(frame);
        }

        private void OnFrameReceive(StreamFrameData frameData)
        {
            OnFrameReceived?.Invoke(frameData);

            if (debugLogs)
            {
                Debug.Log(
                    $"[StreamingLiteNetLibPeer] Received frame {frameData.Data.Length} bytes from slot №{frameData.SlotId} user №{frameData.StreamerId}\n");
            }
        }

        private void OnClientConnected(PlayerConnectionData data)
        {
            if (debugLogs)
                Debug.Log($"[StreamingLiteNetLibPeer] I,m connected with id {data.PlayerId}");
        }

        #region INetEventListener

        public void OnPeerConnected(NetPeer peer)
        {
            _server = peer;
            Debug.Log($"[StreamingLiteNetLibPeer] Client TTL {_client.Ttl}");

            var data = new PlayerConnectionData
            {
                PlayerId = InstanceFinder.ClientManager.Connection.ClientId,
            };
            _writer.Reset();
            _packetProcessor.Write(_writer, data);
            _server.Send(_writer, DeliveryMethod.ReliableOrdered);
            IsConnected = true;

            if (debugLogs)
                Debug.Log($"[StreamingLiteNetLibPeer] Peer {peer.Id} connected! Connection {data.PlayerId} sent");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Connect();

            if (debugLogs) Debug.Log($"[StreamingLiteNetLibPeer] Peer disconnected. Reason: {disconnectInfo.Reason}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            if (debugLogs)
                Debug.LogError(
                    $"[StreamingLiteNetLibPeer] ({endPoint.Address}:{endPoint.Port}) Network error: {socketError}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
            DeliveryMethod deliveryMethod)
        {
            _packetProcessor.ReadAllPackets(reader);
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
        }

        #endregion
    }
}