using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Code.Network.Stream.Data;
using LiteNetLib;
using LiteNetLib.Utils;
using FishNet;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using PlayFlow;
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
        [SerializeField] private bool debugLogs = false;

        private NetManager client;
        private NetPeer server;
        private bool _isConnected = false;

        public bool IsConnected =>
            _isConnected /*&& _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected*/;

        private NetPacketProcessor packetProcessor;
        private NetDataWriter writer;

        public event Action<StreamFrameData> OnFrameReceived;
        
        private void Awake()
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void Update()
        {
            client?.PollEvents();
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Connect();
            }
            else
            {
                Disconnect();
            }
        }

        /// <summary>
        /// Подключиться к серверу стриминга.
        /// </summary>
        public void Connect()
        {
            packetProcessor = new NetPacketProcessor();
            writer = new NetDataWriter();
            
            packetProcessor.SubscribeReusable<StreamFrameData>(OnFrameReceive);

            client = new NetManager(this, null)
            {
                AutoRecycle = true
            };
            client.Start();
            client.Connect(StreamingLiteNetLibServer.ServerAddress, StreamingLiteNetLibServer.ServerStreamPort, "stream_peer");
            
            if (debugLogs)
                Debug.Log(
                    $"[StreamingLiteNetLibPeer] Connecting to {StreamingLiteNetLibServer.ServerAddress}:{StreamingLiteNetLibServer.ServerStreamPort}");
        }

        /// <summary>
        /// Отключиться от сервера.
        /// </summary>
        public void Disconnect()
        {
        }

        /// <summary>
        /// Отправить фрейм стрима на сервер.
        /// </summary>
        public void SendStreamFrame(int slotNumber, byte[] frameData)
        {
            if(server.ConnectionState != ConnectionState.Connected)
                return;
            
            var data = new StreamFrameData()
            {
                StreamerId = InstanceFinder.ClientManager.Connection.ClientId,
                SlotId = slotNumber,
                Data = frameData
            };
            
            writer.Reset();
            packetProcessor.Write(writer, data);
            server.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Получить текущий фрейм стрима от конкретного клиента.
        /// </summary>
        public bool TryGetStreamFrame(int streamClientId, byte slotNumber, out byte[] frameData)
        {
            frameData = null;
            return false;
        }

        private void OnFrameReceive(StreamFrameData frameData)
        {
            OnFrameReceived?.Invoke(frameData);
        }

        #region INetEventListener

        public void OnPeerConnected(NetPeer peer)
        {
            server = peer;

            var data = new PlayerConnectionData
            {
                PlayerId = InstanceFinder.ClientManager.Connection.ClientId,
            };
            writer.Reset();
            packetProcessor.Write(writer, data);
            server.Send(writer, DeliveryMethod.ReliableOrdered);
            _isConnected = true;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Connect();
            
            if(debugLogs) Debug.LogWarning($"[StreamingLiteNetLibPeer] Peer disconnected. Reason: {disconnectInfo.Reason}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
            DeliveryMethod deliveryMethod)
        {
            packetProcessor.ReadAllPackets(reader);
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