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
        
        [SerializeField] private int debugPing = -1;

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
            debugPing = server is { ConnectionState: ConnectionState.Connected } ? server.Ping : -1;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                Invoke(nameof(Connect), 1f);    
                //Connect();
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
            if(InstanceFinder.ClientManager.Connection.ClientId < 0)
            {
                Invoke(nameof(Connect), 1f);
                return;
            }
            
            packetProcessor = new NetPacketProcessor();
            writer = new NetDataWriter();
            
            packetProcessor.SubscribeReusable<StreamFrameData>(OnFrameReceive);
            packetProcessor.SubscribeReusable<PlayerConnectionData>(OnClientConnected);

            client = new NetManager(this, null)
            {
                AutoRecycle = true,
                PacketPoolSize = 10_000,
                PingInterval = 1000,
                UpdateTime = 5,
                UseNativeSockets = true,
            };
            
            client.Start();
            client.Connect(StreamingLiteNetLibServer.ServerAddress, StreamingLiteNetLibServer.ServerStreamPort, "stream_peer");
            
            Invoke(nameof(Disconnect), 10f);
            if (debugLogs)
                Debug.Log(
                    $"[StreamingLiteNetLibPeer] Connecting to {StreamingLiteNetLibServer.ServerAddress}:{StreamingLiteNetLibServer.ServerStreamPort}");
        }

        /// <summary>
        /// Отключиться от сервера.
        /// </summary>
        [ContextMenu("Disconnect")]
        public void Disconnect()
        {
            client.DisconnectAll();
            client.Stop();
        }

        /// <summary>
        /// Отправить фрейм стрима на сервер.
        /// </summary>
        public void SendStreamFrame(int slotNumber, byte[] frameData)
        {
            if (server.ConnectionState != ConnectionState.Connected)
            {
                Debug.LogWarning($"[StreamingLiteNetLibPeer] Connection state is {server.ConnectionState}");
                return;
            }
            
            var data = new StreamFrameData()
            {
                StreamerId = 1,//InstanceFinder.ClientManager.Connection.ClientId,
                SlotId = slotNumber,
                Data = frameData
            };
            
            writer.Reset();
            packetProcessor.Write(writer, data);
            server.Send(writer, DeliveryMethod.ReliableOrdered);
            Debug.Log($"[StreamingLiteNetLibPeer] Try send frame {data.Data.Length} bytes");
        }

        private void OnFrameReceive(StreamFrameData frameData)
        {
            OnFrameReceived?.Invoke(frameData);
            if(debugLogs)
                Debug.Log($"[StreamingLiteNetLibPeer] Received frame {frameData.Data.Length} bytes");
        }
        
        private void OnClientConnected(PlayerConnectionData data)
        {
            if(debugLogs)
                Debug.Log($"[StreamingLiteNetLibPeer] I,m connected with id {data.PlayerId}");
        }

        #region INetEventListener

        public void OnPeerConnected(NetPeer peer)
        {
            server = peer;

            var data = new PlayerConnectionData
            {
                PlayerId = 1,
            };
            writer.Reset();
            packetProcessor.Write(writer, data);
            server.Send(writer, DeliveryMethod.ReliableOrdered);
            _isConnected = true;
            
            if(debugLogs) Debug.Log($"[StreamingLiteNetLibPeer] Peer {peer.Id} connected! Connection {data.PlayerId} sent");
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Connect();
            
            if(debugLogs) Debug.Log($"[StreamingLiteNetLibPeer] Peer disconnected. Reason: {disconnectInfo.Reason}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            if(debugLogs) Debug.LogError($"[StreamingLiteNetLibPeer] ({endPoint.Address}:{endPoint.Port}) Network error: {socketError}");
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber,
            DeliveryMethod deliveryMethod)
        {
            packetProcessor.ReadAllPackets(reader);
            /*if(debugLogs)
                Debug.Log($"[StreamingLiteNetLibPeer] Received somethings {reader.RawData.Length} bytes");*/
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