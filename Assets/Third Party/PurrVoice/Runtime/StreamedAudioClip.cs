using System;
using System.Buffers;
using UnityEngine;

namespace PurrNet.Voice
{
    public class StreamedAudioClip : IVoiceOutput
    {
        public AudioSource source;

        public event Action<ArraySegment<float>> onStartPlayingSample;
        public event Action<ArraySegment<float>> onEndPlayingSample;

        private ProcessSamplesDelegate _processSamples;
        private FilterLevel[] _levels;

        public float playbackOffsetInSeconds = 0.1f;
        public IAudioInputSource inputSource;
        public int frequency => inputSource?.frequency ?? -1;

        private AudioClip _streamClip;
        private bool _audioSetup;
        private bool _isReady;

        private int _clipLen;
        private int _writeHead;
        private int _desiredLag;

        private bool _shouldPlay;

        private float[] _writeBuffer;

        public void Init(IAudioInputSource inputSource,
                         ProcessSamplesDelegate processSamples = null,
                         params FilterLevel[] levels)
        {
            this.inputSource = inputSource;
            _processSamples = processSamples;
            _levels = levels;
            NetworkManager.main.onTick += OnTick;
        }

        public void SetAudioSource(AudioSource source)
        {
            this.source = source;
        }

        public void SetInput(IAudioInputSource mic)
        {
            if (mic == null)
                return;

            if (inputSource != null)
            {
                inputSource.Stop();
                inputSource.onSampleReady -= OnSampleReady;
            }
            
            inputSource = mic;
            inputSource.onSampleReady += OnSampleReady;
            inputSource.Start();
        }

        public void Start()
        {
            if (inputSource == null || inputSource.isRecording)
                return;

            if (inputSource.Start() != StartDeviceResult.Success)
                return;

            SetupAudio();
        }

        public void SetupAudio()
        {
            if (inputSource != null)
                inputSource.onSampleReady += OnSampleReady;

            if (source != null)
            {
                source.loop = true;
                source.playOnAwake = false;
            }

            _isReady = false;
            _audioSetup = false;
            _clipLen = 0;
            _writeHead = 0;
        }

        private void EnsureAudioClipCreated()
        {
            if (_audioSetup || frequency <= 0 || source == null)
                return;

            int sr = AudioSettings.outputSampleRate;
            _clipLen = sr;

            _streamClip = AudioClip.Create("StreamedVoice", _clipLen, 1, sr, false);
            source.clip = _streamClip;

            AudioSettings.GetDSPBufferSize(out int dsp, out int num);
            _desiredLag = Mathf.CeilToInt(playbackOffsetInSeconds * sr) + (dsp * num);

            _writeHead = 0;
            _audioSetup = true;

            if (_writeBuffer == null || _writeBuffer.Length < _clipLen)
                _writeBuffer = new float[_clipLen];
        }

        public void Stop()
        {
            if (inputSource == null || !inputSource.isRecording)
                return;

            inputSource.Stop();
            StopAudio();
        }

        public void StopAudio()
        {
            if (inputSource != null)
                inputSource.onSampleReady -= OnSampleReady;

            if (source && source.isPlaying)
                source.Stop();

            _audioSetup = false;
            _isReady = false;
        }

        public void HandleAudioFilterRead(float[] data, int channels)
        {
        }

        private void OnSampleReady(ArraySegment<float> data)
        {
            if (data.Array == null || data.Count <= 0)
                return;

            EnsureAudioClipCreated();
            if (!_audioSetup || _streamClip == null || source == null)
                return;

            var processed = data;
            if (_processSamples != null)
                processed = _processSamples(data, frequency, _levels);

            VoicePlaybackMonitor.ReportPlayback(processed);
            onStartPlayingSample?.Invoke(processed);

            int inRate = frequency;
            int outRate = AudioSettings.outputSampleRate;

            if (inRate == outRate)
            {
                WriteSamplesDirect(processed);
            }
            else
            {
                int outCount = Mathf.CeilToInt(processed.Count * (outRate / (float)inRate));
                float[] tmp = ArrayPool<float>.Shared.Rent(outCount);

                try
                {
                    float ratio = inRate / (float)outRate;
                    var srcArray = processed.Array;
                    int srcOffset = processed.Offset;
                    int srcCount = processed.Count;

                    for (int i = 0; i < outCount; i++)
                    {
                        float t = i * ratio;
                        int t0 = (int)t;
                        int t1 = Mathf.Min(t0 + 1, srcCount - 1);

                        float s0 = srcArray[srcOffset + t0];
                        float s1 = srcArray[srcOffset + t1];

                        tmp[i] = Mathf.Lerp(s0, s1, t - t0);
                    }

                    WriteFromBuffer(tmp, 0, outCount);
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(tmp);
                }
            }

            onEndPlayingSample?.Invoke(processed);

            if (!_isReady)
            {
                int ahead = (_writeHead - source.timeSamples + _clipLen) % _clipLen;
                if (ahead >= _desiredLag)
                {
                    int startPos = (_writeHead - _desiredLag + _clipLen) % _clipLen;
                    source.timeSamples = startPos;
                    source.Play();
                    _isReady = true;
                }
            }
        }

        public void OnTick(bool asServer)
        {
            if (!_audioSetup || _streamClip == null || source == null)
                return;

            int ahead = (_writeHead - source.timeSamples + _clipLen) % _clipLen;

            if (ahead < _desiredLag)
                WriteZeros(_desiredLag - ahead);

            if (!_isReady && ahead >= _desiredLag)
            {
                int startPos = (_writeHead - _desiredLag + _clipLen) % _clipLen;
                source.timeSamples = startPos;
                source.Play();
                _isReady = true;
            }

            if (_shouldPlay)
            {
                _shouldPlay = false;
                if (!source.isPlaying)
                    source.Play();
            }
        }

        private void WriteZeros(int count)
        {
            if (_writeBuffer == null || _writeBuffer.Length < _clipLen)
                _writeBuffer = new float[_clipLen];

            Array.Clear(_writeBuffer, 0, _writeBuffer.Length);

            int remaining = count;
            while (remaining > 0)
            {
                int chunk = Mathf.Min(remaining, _clipLen - _writeHead);
                _streamClip.SetData(_writeBuffer, _writeHead);
                _writeHead = (_writeHead + chunk) % _clipLen;
                remaining -= chunk;
            }
        }

        /// <summary>Writes samples directly when sample rates match. Zero allocations.</summary>
        private void WriteSamplesDirect(ArraySegment<float> data)
        {
            if (_writeBuffer == null || _writeBuffer.Length < _clipLen)
                _writeBuffer = new float[_clipLen];

            var srcArray = data.Array;
            int srcOffset = data.Offset;
            int remaining = data.Count;
            int srcPos = srcOffset;

            while (remaining > 0)
            {
                int chunk = Mathf.Min(remaining, _clipLen - _writeHead);

                Array.Copy(srcArray, srcPos, _writeBuffer, 0, chunk);
                _streamClip.SetData(_writeBuffer, _writeHead);

                _writeHead = (_writeHead + chunk) % _clipLen;
                remaining -= chunk;
                srcPos += chunk;
            }
        }

        /// <summary>Writes from a buffer to the stream using единственный временный буфер.</summary>
        private void WriteFromBuffer(float[] buffer, int srcOffset, int count)
        {
            if (_writeBuffer == null || _writeBuffer.Length < _clipLen)
                _writeBuffer = new float[_clipLen];

            int remaining = count;
            int srcPos = srcOffset;

            while (remaining > 0)
            {
                int chunk = Mathf.Min(remaining, _clipLen - _writeHead);

                Array.Copy(buffer, srcPos, _writeBuffer, 0, chunk);
                _streamClip.SetData(_writeBuffer, _writeHead);

                _writeHead = (_writeHead + chunk) % _clipLen;
                remaining -= chunk;
                srcPos += chunk;
            }
        }
    }
}
