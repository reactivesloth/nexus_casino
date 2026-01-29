using System.Collections.Generic;
using UnityEngine;
using PurrNet;

namespace MetaVoiceChat.NetProviders.PurrNet
{
    [RequireComponent(typeof(MetaVc))]
    public class PurrNetNetProvider : NetworkBehaviour, INetProvider
    {
        public static PurrNetNetProvider LocalPlayerInstance { get; private set; }
        private static readonly List<PurrNetNetProvider> s_Instances = new();
        public static IReadOnlyList<PurrNetNetProvider> Instances => s_Instances;

        public MetaVc MetaVc { get; private set; }

        private const int DefaultMaxDataBytesPerPacket = 1200;

        protected override void OnSpawned()
        {
            if (isOwner) LocalPlayerInstance = this;
            s_Instances.Add(this);

            MetaVc = GetComponent<MetaVc>();
            MetaVc.StartClient(this, isOwner, DefaultMaxDataBytesPerPacket);
        }

        protected override void OnDespawned()
        {
            if (isOwner) LocalPlayerInstance = null;
            s_Instances.Remove(this);
            MetaVc.StopClient();
        }

        public bool IsLocalPlayerDeafened
        {
            get
            {
                if (LocalPlayerInstance == null) return false;
                return LocalPlayerInstance.MetaVc.isDeafened;
            }
        }

        public void RelayFrame(int index, double timestamp, System.ReadOnlySpan<byte> data)
        {
            byte[] dataToSend = data.ToArray(); 

            float additionalLatency = Time.deltaTime;

            var frame = new PurrNetFrame(index, timestamp, additionalLatency, dataToSend);

            if (isServer)
            {
                ReceiveFrameObserversRpc(frame);
            }
            else
            {
                RelayFrameServerRpc(frame);
            }
        }

        [ServerRpc]
        private void RelayFrameServerRpc(PurrNetFrame frame, RPCInfo info = default)
        {
            frame.additionalLatency += Time.deltaTime;
            ReceiveFrameObserversRpc(frame);
        }

        [ObserversRpc (excludeOwner:true)]
        private void ReceiveFrameObserversRpc(PurrNetFrame frame, RPCInfo info = default)
        {
            float latency = frame.additionalLatency;

            if (isServer)
            {
                latency -= Time.deltaTime;
            }

            MetaVc.ReceiveFrame(frame.index, frame.timestamp, latency, frame.data);
        }
    }
}