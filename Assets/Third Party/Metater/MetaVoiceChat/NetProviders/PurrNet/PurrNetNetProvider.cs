using System;
using System.Collections.Generic;
using MetaVoiceChat.Utils;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;


namespace MetaVoiceChat.NetProviders.PurrNet
{
    [RequireComponent(typeof(MetaVc))]
    public class PurrNetNetProvider : NetworkBehaviour, INetProvider
    {
        #region Singleton
        public static PurrNetNetProvider LocalPlayerInstance { get; private set; }
        private readonly static List<PurrNetNetProvider> instances = new();
        public static IReadOnlyList<PurrNetNetProvider> Instances => instances;
        #endregion

        bool INetProvider.IsLocalPlayerDeafened => LocalPlayerInstance.MetaVc.isDeafened;

        public MetaVc MetaVc { get; private set; }

        protected override void OnSpawned() {
            base.OnSpawned();
            
            #region Singleton
            if (isOwner)
            {
                LocalPlayerInstance = this;
            }

            instances.Add(this);
            #endregion

            static int GetMaxDataBytesPerPacket(NetworkManager networkManager)
            {
                if (networkManager.clientToServerConn != null) return 1011;

                int bytes = networkManager.transport.transport.GetMTU(networkManager.clientToServerConn.Value, Channel.Unreliable, false) - 13;
                bytes -= sizeof(int); // Index
                bytes -= sizeof(double); // Timestamp
                bytes -= sizeof(byte); // Additional latency
                bytes -= sizeof(ushort); // Array length
                return bytes;
            }

            MetaVc = GetComponent<MetaVc>();
            MetaVc.StartClient(this, isOwner, GetMaxDataBytesPerPacket(NetworkManager.main));
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();
            
            #region Singleton
            if (isOwner)
            {
                LocalPlayerInstance = null;
            }

            instances.Remove(this);
            #endregion

            MetaVc.StopClient();
        }

        void INetProvider.RelayFrame(int index, double timestamp, ReadOnlySpan<byte> data)
        {
            var array = FixedLengthArrayPool<byte>.Rent(data.Length);
            data.CopyTo(array);

            float additionalLatency = Time.deltaTime;
            PurrNetFrame frame = new(index, timestamp, additionalLatency, array);

            if (networkManager.isServer)
            {
                ObsReceiveFrame(frame);
            }
            else
            {
                ServerRelayFrame(frame);
            }

            FixedLengthArrayPool<byte>.Return(array);
        }

        [ServerRpc (channel: Channel.Unreliable)]
        private void ServerRelayFrame(PurrNetFrame frame)
        {
            float additionalLatency = frame.additionalLatency + Time.deltaTime;
            frame = new(frame.index, frame.timestamp, additionalLatency, frame.data);
            ObsReceiveFrame(frame);
        }

        // A possible optimization is to use target RPCs and only send filled arrays to clients that are within audible range, and empty arrays to others.
        // Audible range would be determined by the distance between the reciever's position and the sender's audio source position.
        [ObserversRpc(excludeOwner: true, channel: Channel.Unreliable)]
        private void ObsReceiveFrame(PurrNetFrame frame)
        {
            if (networkManager.isServer)
            {
                // Don't apply server Time.deltaTime to additionalLatency -- this frame did not go over the network again.
                float additionalLatency = frame.additionalLatency - Time.deltaTime;
                MetaVc.ReceiveFrame(frame.index, frame.timestamp, additionalLatency, frame.data);
            }
            else
            {
                MetaVc.ReceiveFrame(frame.index, frame.timestamp, frame.additionalLatency, frame.data);
            }
        }
    }
}