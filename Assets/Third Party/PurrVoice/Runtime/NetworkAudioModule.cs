using System;
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Voice
{
    public class NetworkAudioModule : NetworkModule, IAudioInputSource
    {
        private float[] _chunkBuffer;
        private float[] _decodeBuffer;
        private byte[] _encodeBuffer;
        private int _chunkSize;
        private int _bufferPos;
        private ProcessSamplesDelegate _processSamples;

        private SyncVar<int> _frequency = new SyncVar<int>(-1, ownerAuth: true);

        private OpusCodec _clientCodec;
        private OpusCodec _serverCodec;

        public event Action<int> OnFrequencyChanged
        {
            add => _frequency.onChanged += value;
            remove => _frequency.onChanged -= value;
        }

        public int frequency => _frequency;
        public bool isRecording { get; private set; }

        public event Action<ArraySegment<float>> onSampleReady;

        private const int MAX_CHUNK_SIZE_BYTES = 900;

        private static readonly int[] s_frequencyOptions = { 8000, 12000, 16000, 24000, 48000 };

        public NetworkAudioModule(ProcessSamplesDelegate processSamples = null)
        {
            _frequency.onChanged += OnFrequencySet;
            _processSamples = processSamples;
        }

        private void OnFrequencySet(int newFreq)
        {
            int targetRate = GetClosestFrequency(newFreq);

            _chunkSize = targetRate / 50;
            _chunkBuffer = new float[_chunkSize];
            _decodeBuffer = new float[_chunkSize];
            _encodeBuffer = new byte[OpusCodec.MaxEncodedBytes];
            _bufferPos = 0;

            if (isClient)
                _clientCodec = new OpusCodec(targetRate, 1, _chunkSize);

            if (isServer)
                _serverCodec = new OpusCodec(targetRate, 1, _chunkSize);
        }

        private static int GetClosestFrequency(int frequency)
        {
            int closest = s_frequencyOptions[0];
            int minDiff = Math.Abs(frequency - closest);

            for (int i = 1; i < s_frequencyOptions.Length; i++)
            {
                int option = s_frequencyOptions[i];
                int diff = Math.Abs(frequency - option);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closest = option;
                }
            }

            return closest;
        }

        public void SetFrequency(int frequency)
        {
            if (!isController)
            {
                Debug.LogError(
                    $"Only the controller can set the frequency. Current controller: {owner}, current player: {localPlayer}");
                return;
            }
            
            _frequency.value = frequency;
        }

        public void SendAudioChunk(ArraySegment<float> segment)
        {
            if (!isOwner || _frequency.value < 0 || _chunkBuffer == null || _chunkBuffer.Length == 0 || _clientCodec == null)
                return;

            if (segment.Array == null || segment.Count <= 0)
                return;

            int offset = 0;
            var srcArray = segment.Array;
            int srcBaseOffset = segment.Offset;

            while (offset < segment.Count)
            {
                int remaining = _chunkSize - _bufferPos;
                int copy = Math.Min(remaining, segment.Count - offset);
                if (copy <= 0)
                    break;

                Array.Copy(srcArray, srcBaseOffset + offset, _chunkBuffer, _bufferPos, copy);
                _bufferPos += copy;
                offset += copy;

                if (_bufferPos == _chunkSize)
                {
                    (parent as PurrVoicePlayer)?.DebugNetworkSentData(_chunkBuffer);
                    int encodedLen = _clientCodec.Encode(_chunkBuffer, _encodeBuffer);
                    int encodedOffset = 0;
                    while (encodedOffset < encodedLen)
                    {
                        int chunkLen = Math.Min(MAX_CHUNK_SIZE_BYTES, encodedLen - encodedOffset);

                        var encodedSlice = new ByteData(_encodeBuffer, encodedOffset, chunkLen);
                        RpcSendAudio(encodedSlice);

                        encodedOffset += chunkLen;
                    }

                    _bufferPos = 0;
                }
            }
        }

        [ServerRpc(channel: Channel.Unreliable, compressionLevel:CompressionLevel.Best)]
        private void RpcSendAudio(ByteData encoded)
        {
            if (_serverCodec == null || _decodeBuffer == null || encoded.data == null || encoded.length <= 0)
                return;

            int sampleCount = _serverCodec.Decode(encoded.data, encoded.offset, encoded.length, _decodeBuffer);
            if (sampleCount <= 0)
                return;

            SendAudio_Internal(_decodeBuffer, sampleCount);
        }

        private void SendAudio_Internal(float[] buffer, int sampleCount)
        {
            var segment = new ArraySegment<float>(buffer, 0, sampleCount);
            if (_processSamples != null)
                segment = _processSamples(segment, _frequency, FilterLevel.Server);

            float[] samples = segment.Array;
            int count = segment.Count;
            int offset = segment.Offset;

            if (samples == null || count <= 0 || _serverCodec == null)
                return;

            int encodedLen = _serverCodec.Encode(samples, offset, count, _encodeBuffer);
            (parent as PurrVoicePlayer)?.DebugServerProcessed(segment);

            var encodedData = new ByteData(_encodeBuffer, 0, encodedLen);

            var observers = parent.observers;
            for (int i = 0; i < observers.Count; i++)
            {
                var player = observers[i];

                if (owner == player)
                    continue;
                if (player == localPlayer)
                {
                    ReceiveAudio_Internal(samples, offset, count);
                    continue;
                }

                TargetReceiveAudio(player, encodedData);
            }
        }

        [TargetRpc(channel: Channel.Unreliable, compressionLevel:CompressionLevel.Best)]
        private void TargetReceiveAudio(PlayerID player, ByteData encoded)
        {
            if (_clientCodec == null || _decodeBuffer == null || encoded.data == null || encoded.length <= 0)
                return;

            int sampleCount = _clientCodec.Decode(encoded.data, encoded.offset, encoded.length, _decodeBuffer);
            if (sampleCount <= 0)
                return;

            ReceiveAudio_Internal(_decodeBuffer, 0, sampleCount);
        }

        private void ReceiveAudio_Internal(float[] buffer, int offset, int count)
        {
            var segment = new ArraySegment<float>(buffer, offset, count);
            (parent as PurrVoicePlayer)?.DebugReceived(segment);
            onSampleReady?.Invoke(segment);
        }
        
        public StartDeviceResult Start()
        {
            isRecording = true;
            return StartDeviceResult.Success;
        }

        public void Stop()
        {
            isRecording = false;
        }
    }
}
