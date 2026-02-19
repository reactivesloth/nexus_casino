using Concentus.Enums;
using System;
using System.Buffers;
using Concentus;

namespace PurrNet.Voice
{
    public class OpusCodec
    {
        public const int MaxEncodedBytes = 4000;

        private readonly IOpusEncoder _encoder;
        private readonly IOpusDecoder _decoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;

        private const float ShortToFloatFactor = 1f / short.MaxValue;

        public int SampleRate => _sampleRate;

        public OpusCodec(int sampleRate, int channels, int frameSize)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _frameSize = frameSize;
            _encoder = OpusCodecFactory.CreateEncoder(sampleRate, channels, OpusApplication.OPUS_APPLICATION_VOIP);
            _decoder = OpusCodecFactory.CreateDecoder(sampleRate, channels);
        }

        /// <summary>
        /// Encode into a caller-provided buffer. Zero allocations.
        /// </summary>
        /// <param name="input">Input float samples.</param>
        /// <param name="outputBuffer">Output buffer, must be at least MaxEncodedBytes.</param>
        /// <returns>Number of bytes written to outputBuffer.</returns>
        public int Encode(float[] input, int offset, int count, byte[] outputBuffer)
        {
            if (outputBuffer.Length < MaxEncodedBytes)
                throw new ArgumentException($"Output buffer must be at least {MaxEncodedBytes} bytes");

            var shortInput = ArrayPool<short>.Shared.Rent(count);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    float sample = input[offset + i];
                    sample = Math.Max(-1f, Math.Min(1f, sample));
                    shortInput[i] = (short)(sample * short.MaxValue);
                }

                return _encoder.Encode(shortInput.AsSpan(0, count), _frameSize, outputBuffer.AsSpan(),
                    outputBuffer.Length);
            }
            finally
            {
                ArrayPool<short>.Shared.Return(shortInput);
            }
        }

        /// <summary>
        /// Decode into a caller-provided buffer. Zero allocations.
        /// </summary>
        /// <param name="data">Encoded bytes.</param>
        /// <param name="outputBuffer">Output buffer for float samples, must be at least frameSize * channels.</param>
        /// <returns>Number of samples written to outputBuffer.</returns>
        public int Decode(byte[] data, int offset, int count, float[] outputBuffer)
        {
            if (data == null || count <= 0) return 0;

            int requiredSize = _frameSize * _channels;
            if (outputBuffer.Length < requiredSize)
                throw new ArgumentException($"Output buffer must be at least {requiredSize} samples");

            var shortOutput = ArrayPool<short>.Shared.Rent(requiredSize);
            try
            {
                int len = _decoder.Decode(data.AsSpan(offset, count), shortOutput.AsSpan(), _frameSize, false);

                for (int i = 0; i < len; i++)
                    outputBuffer[i] = shortOutput[i] * ShortToFloatFactor;

                return len;
            }
            finally
            {
                ArrayPool<short>.Shared.Return(shortOutput);
            }
        }

        /// <summary>
        /// Allocating Encode overload for callers that need a new byte[]. Prefer Encode(input, outputBuffer) for GC-free path.
        /// </summary>
        public byte[] Encode(float[] input)
        {
            var outputBuffer = ArrayPool<byte>.Shared.Rent(MaxEncodedBytes);
            try
            {
                int len = Encode(input, outputBuffer);
                var result = new byte[len];
                outputBuffer.AsSpan(0, len).CopyTo(result);
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(outputBuffer);
            }
        }

        /// <summary>
        /// Allocating Decode overload for callers that need a new float[]. Prefer Decode(data, outputBuffer) for GC-free path.
        /// </summary>
        public float[] Decode(byte[] data)
        {
            var tmp = ArrayPool<float>.Shared.Rent(_frameSize * _channels);
            try
            {
                int len = Decode(data, tmp);
                var result = new float[len];
                tmp.AsSpan(0, len).CopyTo(result);
                return result;
            }
            finally
            {
                ArrayPool<float>.Shared.Return(tmp);
            }
        }

        public int Encode(float[] input, byte[] outputBuffer)
            => Encode(input, 0, input.Length, outputBuffer);

        public int Decode(byte[] data, float[] outputBuffer)
            => Decode(data, 0, data?.Length ?? 0, outputBuffer);
    }
}