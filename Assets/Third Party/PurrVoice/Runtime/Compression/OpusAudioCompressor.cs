using System.Collections.Generic;

namespace PurrNet.Voice
{
    public static class OpusAudioCompressor
    {
        private static readonly Dictionary<(int sampleRate, int frameSize), OpusCodec> _codecs = new();

        private static OpusCodec GetCodec(int sampleRate, int frameSize)
        {
            var key = (sampleRate, frameSize);
            if (!_codecs.TryGetValue(key, out var codec))
            {
                codec = new OpusCodec(sampleRate, 1, frameSize);
                _codecs[key] = codec;
            }

            return codec;
        }

        public static byte[] Encode(float[] input, int sampleRate, int frameSize)
        {
            var codec = GetCodec(sampleRate, frameSize);
            return codec.Encode(input);
        }

        public static float[] Decode(byte[] data, int sampleRate, int frameSize)
        {
            var codec = GetCodec(sampleRate, frameSize);
            return codec.Decode(data);
        }

        public static int Encode(float[] input, int sampleRate, int frameSize, byte[] outputBuffer)
        {
            var codec = GetCodec(sampleRate, frameSize);
            return codec.Encode(input, outputBuffer);
        }

        public static int Decode(byte[] data, int sampleRate, int frameSize, float[] outputBuffer)
        {
            var codec = GetCodec(sampleRate, frameSize);
            return codec.Decode(data, outputBuffer);
        }
    }
}