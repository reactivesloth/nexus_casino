using System;

namespace MetaVoiceChat.NetProviders.PurrNet
{
    public readonly struct PurrNetFrame
    {
        public readonly int index;
        public readonly double timestamp;
        public readonly float additionalLatency;
        public readonly ArraySegment<byte> data;

        public PurrNetFrame(int index, double timestamp, float additionalLatency, ArraySegment<byte> data)
        {
            this.index = index;
            this.timestamp = timestamp;
            this.additionalLatency = additionalLatency;
            this.data = data;
        }
    }
}