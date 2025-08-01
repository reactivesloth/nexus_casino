using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Transporting;
using System;

public class VoiceChat : NetworkBehaviour
{
    public enum ChatType { Global, Proximity }
    public ChatType VoiceChatType = ChatType.Global;

    public enum DetectionType { PushToTalk, VoiceActivation }
    public DetectionType VoiceDetectionType = DetectionType.PushToTalk;

    public bool Activated = true;
    public KeyCode PushToTalkKey;

    public AudioSource source;
    public float proximityRange = 10f;
    public float voiceActivationThreshold = 0.002f;

    [Header("Compression")]
    // Сейчас всегда используем PCM16; можно расширить, добавив μ-law и переключатель
    public bool usePCM16Compression = true;

    private bool canTalk = true;
    private bool previousCanTalk = false;

    private string deviceName;
    private const int sampleRate = 48000;
    private const int bufferSize = 16384; // ~=0.34s
    private readonly float transmitInterval = bufferSize / (float)sampleRate;

    private float[] audioBuffer;
    private int position;

    private AudioClip microphoneClip;
    private float[] sampleData;
    private float[] micDataBuffer;

    private Coroutine transmitCoroutine;
    private WaitForSeconds transmitWait;

    // Кэш трансформов по clientId
    private readonly Dictionary<int, Transform> _playerTransformCache = new();

    // Входящие буферы воспроизведения
    private readonly Dictionary<int, VoicePlayback> _playbacks = new();
    private readonly object _playbackLock = new();

    // Для сжатия
    private byte[] transmitByteBuffer;

    // Лимитер/нормализация микса
    private float mixGain = 1f;
    private float mixGainTarget = 1f;
    private const float mixGainRestoreSpeed = 0.02f; // сглаживание (чем меньше — тем плавнее восстановление)

    private void Awake()
    {
        // отложенная инициализация
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
            return;

        if (source == null)
            Debug.LogError("[VOICE] AudioSource not assigned!");

        deviceName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

        if (string.IsNullOrEmpty(deviceName))
            Debug.LogError("[VOICE] No microphone device found!");

        audioBuffer = new float[bufferSize];
        sampleData = new float[bufferSize];
        micDataBuffer = new float[bufferSize];
        source.playOnAwake = false;

        transmitWait = new WaitForSeconds(transmitInterval);
        transmitByteBuffer = new byte[bufferSize * 2]; // PCM16: 2 байта на сэмпл

        EnsureOutputClip();
    }

    void Update()
    {
        if (!Activated || !IsOwner)
            return;

        string selectedDevice = MicrophoneManager.Instance.GetCurrentDeviceName();
        if (selectedDevice != deviceName)
        {
            UpdateMicrophone(selectedDevice);
        }

        switch (VoiceDetectionType)
        {
            case DetectionType.PushToTalk:
                canTalk = Input.GetKey(PushToTalkKey);
                if (canTalk && microphoneClip == null)
                {
                    StartMicrophone();
                }
                else if (!canTalk && microphoneClip != null)
                {
                    StopTalking();
                    StopMicrophone();
                }
                break;

            case DetectionType.VoiceActivation:
                if (microphoneClip == null)
                {
                    StartMicrophone();
                }
                canTalk = IsVoiceActivated();
                break;
        }

        if (!previousCanTalk && canTalk)
            StartTalking();

        if (previousCanTalk && !canTalk)
            StopTalking();

        previousCanTalk = canTalk;

        UpdatePlaybacks(Time.deltaTime);
    }

    private void UpdatePlaybacks(float deltaTime)
    {
        List<int> toRemove = null;
        Vector3 selfPos = transform.position;

        lock (_playbackLock)
        {
            foreach (var kv in _playbacks)
            {
                int senderId = kv.Key;
                VoicePlayback playback = kv.Value;

                if (Time.time - playback.LastReceivedTime > 5f)
                {
                    toRemove ??= new List<int>();
                    toRemove.Add(senderId);
                    continue;
                }

                Transform senderTransform = GetPlayerTransform(senderId);
                if (senderTransform != null)
                {
                    playback.SenderPosition = senderTransform.position;
                }

                playback.UpdateAttenuation(selfPos, playback.SenderPosition, proximityRange, VoiceChatType);
                playback.SmoothAttenuation(deltaTime);
            }

            if (toRemove != null)
            {
                foreach (int id in toRemove)
                {
                    _playbacks.Remove(id);
                }
            }
        }
    }

    private void EnsureOutputClip()
    {
        if (source == null)
            return;

        if (source.clip == null)
        {
            AudioClip dummy = AudioClip.Create("VoiceOutputDummy", sampleRate, 1, sampleRate, false);
            source.clip = dummy;
            source.loop = true;
            source.Play();
        }
    }

    private void StartMicrophone()
    {
        if (string.IsNullOrEmpty(deviceName))
            return;

        position = 0;
        microphoneClip = Microphone.Start(deviceName, true, 10, sampleRate);
    }

    private void StopMicrophone()
    {
        if (string.IsNullOrEmpty(deviceName))
            return;

        Microphone.End(deviceName);
        microphoneClip = null;
    }

    private void UpdateMicrophone(string newDeviceName)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            StopTalking();
            StopMicrophone();
        }

        deviceName = newDeviceName;

        if (canTalk)
        {
            StartMicrophone();
            StartTalking();
        }
    }

    private void StartTalking()
    {
        if (string.IsNullOrEmpty(deviceName) || transmitCoroutine != null)
            return;

        transmitCoroutine = StartCoroutine(TransmitVoice());
    }

    private void StopTalking()
    {
        if (transmitCoroutine != null)
        {
            StopCoroutine(transmitCoroutine);
            transmitCoroutine = null;
        }
    }

    private IEnumerator TransmitVoice()
    {
        while (canTalk)
        {
            if (microphoneClip == null)
                yield break;

            int micPosition = Microphone.GetPosition(deviceName);

            if (micPosition < position)
                position = micPosition;

            if (position + bufferSize > micPosition)
            {
                yield return null;
                continue;
            }

            microphoneClip.GetData(audioBuffer, position);
            position = (position + bufferSize) % microphoneClip.samples;

            if (usePCM16Compression)
            {
                CompressFloatToPCM16(audioBuffer, transmitByteBuffer);
                TransmitAudioServerRpc(transmitByteBuffer);
            }
            else
            {
                // fallback на float (старое) — можно убрать, если не нужен
                float[] copy = new float[audioBuffer.Length];
                Array.Copy(audioBuffer, copy, audioBuffer.Length);
                TransmitAudioServerRpc_Float(copy);
            }

            yield return transmitWait;
        }
    }

    private void CompressFloatToPCM16(float[] inSamples, byte[] outBytes)
    {
        int len = inSamples.Length;
        for (int i = 0; i < len; i++)
        {
            float f = Mathf.Clamp(inSamples[i], -1f, 1f);
            short s = (short)(f * 32767f);
            int byteIndex = i * 2;
            outBytes[byteIndex] = (byte)(s & 0xFF);
            outBytes[byteIndex + 1] = (byte)((s >> 8) & 0xFF);
        }
    }

    private bool IsVoiceActivated()
    {
        if (microphoneClip == null)
            return false;

        int micPosition = Microphone.GetPosition(deviceName);
        int sampleStartPosition = micPosition - bufferSize;
        if (sampleStartPosition < 0)
            return false;

        microphoneClip.GetData(sampleData, sampleStartPosition);

        float sum = 0;
        for (int i = 0; i < sampleData.Length; i++)
            sum += Mathf.Abs(sampleData[i]);

        float average = sum / sampleData.Length;
        return average > voiceActivationThreshold;
    }

    // Старый RPC для float — сохраняется совместимость, можно удалить при полной миграции
    [ServerRpc(RequireOwnership = false)]
    private void TransmitAudioServerRpc_Float(float[] audioData, Channel channel = Channel.Unreliable, NetworkConnection sender = null)
    {
        TransmitAudioObserversRpc_Float(audioData, sender.ClientId);
    }

    [ObserversRpc]
    private void TransmitAudioObserversRpc_Float(float[] audioData, int senderClientId, Channel channel = Channel.Unreliable)
    {
        int localClientId = NetworkManager.ClientManager.Connection.ClientId;
        if (senderClientId == localClientId)
            return;

        RegisterReceivedAudio_Float(senderClientId, audioData);
    }

    private void RegisterReceivedAudio_Float(int senderClientId, float[] audioData)
    {
        lock (_playbackLock)
        {
            if (!_playbacks.TryGetValue(senderClientId, out var playback))
            {
                playback = new VoicePlayback(sampleRate * 2);
                _playbacks[senderClientId] = playback;
            }

            Transform senderTransform = GetPlayerTransform(senderClientId);
            if (senderTransform != null)
                playback.SenderPosition = senderTransform.position;

            playback.AddSamples(audioData); // старый путь
        }
    }

    // Новый сжатый путь
    [ServerRpc(RequireOwnership = false)]
    private void TransmitAudioServerRpc(byte[] compressedPCM16, Channel channel = Channel.Unreliable, NetworkConnection sender = null)
    {
        TransmitAudioObserversRpc(compressedPCM16, sender.ClientId);
    }

    [ObserversRpc]
    private void TransmitAudioObserversRpc(byte[] compressedPCM16, int senderClientId, Channel channel = Channel.Unreliable)
    {
        int localClientId = NetworkManager.ClientManager.Connection.ClientId;
        if (senderClientId == localClientId)
            return;

        RegisterReceivedAudio(senderClientId, compressedPCM16);
    }

    private void RegisterReceivedAudio(int senderClientId, byte[] compressedPCM16)
    {
        lock (_playbackLock)
        {
            if (!_playbacks.TryGetValue(senderClientId, out var playback))
            {
                playback = new VoicePlayback(sampleRate * 2);
                _playbacks[senderClientId] = playback;
            }

            Transform senderTransform = GetPlayerTransform(senderClientId);
            if (senderTransform != null)
                playback.SenderPosition = senderTransform.position;

            playback.AddCompressedSamples(compressedPCM16);
        }
    }

    private Transform GetPlayerTransform(int clientId)
    {
        if (_playerTransformCache.TryGetValue(clientId, out var cached) && cached != null)
            return cached;

        foreach (var obj in FindObjectsOfType<NetworkObject>())
        {
            if (obj.Owner != null && obj.Owner.ClientId == clientId)
            {
                _playerTransformCache[clientId] = obj.transform;
                return obj.transform;
            }
        }

        _playerTransformCache.Remove(clientId);
        return null;
    }

    private float GetMicInputVolume()
    {
        if (microphoneClip == null || string.IsNullOrEmpty(deviceName))
            return 0f;

        int micPosition = Microphone.GetPosition(deviceName);
        int sampleStartPosition = micPosition - bufferSize;
        if (sampleStartPosition < 0)
            return 0f;

        microphoneClip.GetData(micDataBuffer, sampleStartPosition);

        float sum = 0;
        for (int i = 0; i < micDataBuffer.Length; i++)
            sum += micDataBuffer[i] * micDataBuffer[i];

        float rmsValue = Mathf.Sqrt(sum / micDataBuffer.Length);
        return Mathf.Clamp(rmsValue * 50f, 0f, 1f);
    }

    private void OnDestroy()
    {
        if (!IsOwner)
            return;

        StopTalking();
        StopMicrophone();

        if (source != null && source.clip != null)
        {
            Destroy(source.clip);
            source.clip = null;
        }

        lock (_playbackLock)
        {
            _playbacks.Clear();
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        int frameCount = data.Length / channels;

        // Убедиться, что временный буфер достаточно большой
        float[] temp = VoicePlayback.TempBuffer;
        if (temp.Length < frameCount)
            VoicePlayback.ResizeTempBuffer(frameCount);
        int actualFrameCount = frameCount;

        // Обнуляем выходной буфер (на случай, если есть остатки)
        for (int i = 0; i < data.Length; i++)
            data[i] = 0f;

        lock (_playbackLock)
        {
            foreach (var playback in _playbacks.Values)
            {
                int read = playback.Read(temp, actualFrameCount);
                if (read == 0)
                    continue;

                for (int i = 0; i < read; i++)
                {
                    float sample = temp[i];
                    for (int c = 0; c < channels; c++)
                    {
                        int idx = i * channels + c;
                        data[idx] += sample;
                    }
                }
            }
        }

        // Адаптивный лимитер: если пик >1, ослабляем плавно
        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float abs = Mathf.Abs(data[i]);
            if (abs > peak) peak = abs;
        }

        mixGainTarget = peak > 1f ? (1f / peak) : 1f;
        mixGain = Mathf.Lerp(mixGain, mixGainTarget, mixGainRestoreSpeed);

        if (mixGain < 1f)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= mixGain;
        }

        // Обязательное ограничение
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 1f) data[i] = 1f;
            else if (data[i] < -1f) data[i] = -1f;
        }
    }

    // Внутренний класс для хранения входящего голоса
    private class VoicePlayback
    {
        private readonly object _lock = new();
        private float[] _circularBuffer;
        private int _writeIndex;
        private int _readIndex;
        private int _availableSamples;

        public Vector3 SenderPosition;
        public float TargetAttenuation = 1f;
        public float CurrentAttenuation = 1f;
        public float AttenuationSmoothSpeed = 8f; // сколько единиц в секунду сглаживается

        public float LastReceivedTime { get; private set; } = Time.time;

        public static float[] TempBuffer = new float[4096];

        public VoicePlayback(int capacity)
        {
            _circularBuffer = new float[capacity];
            TargetAttenuation = CurrentAttenuation = 1f;
        }

        public void AddSamples(float[] samples)
        {
            lock (_lock)
            {
                foreach (var s in samples)
                {
                    if (_availableSamples >= _circularBuffer.Length)
                    {
                        _readIndex = (_readIndex + 1) % _circularBuffer.Length;
                        _availableSamples--;
                    }

                    _circularBuffer[_writeIndex] = s;
                    _writeIndex = (_writeIndex + 1) % _circularBuffer.Length;
                    _availableSamples++;
                }

                LastReceivedTime = Time.time;
            }
        }

        public void AddCompressedSamples(byte[] compressedPCM16)
        {
            lock (_lock)
            {
                int sampleCount = compressedPCM16.Length / 2;
                for (int i = 0; i < sampleCount; i++)
                {
                    int byteIdx = i * 2;
                    short s = (short)(compressedPCM16[byteIdx] | (compressedPCM16[byteIdx + 1] << 8));
                    float sample = s / 32767f;

                    if (_availableSamples >= _circularBuffer.Length)
                    {
                        _readIndex = (_readIndex + 1) % _circularBuffer.Length;
                        _availableSamples--;
                    }

                    _circularBuffer[_writeIndex] = sample;
                    _writeIndex = (_writeIndex + 1) % _circularBuffer.Length;
                    _availableSamples++;
                }

                LastReceivedTime = Time.time;
            }
        }

        public void UpdateAttenuation(Vector3 listenerPosition, Vector3 senderPosition, float proximityRange, ChatType chatType)
        {
            if (chatType == ChatType.Proximity)
            {
                float distance = Vector3.Distance(listenerPosition, senderPosition);
                if (distance > proximityRange)
                    TargetAttenuation = 0f;
                else
                    TargetAttenuation = 1f - Mathf.Clamp01(distance / proximityRange);
            }
            else
            {
                TargetAttenuation = 1f;
            }
        }

        public void SmoothAttenuation(float deltaTime)
        {
            CurrentAttenuation = Mathf.MoveTowards(CurrentAttenuation, TargetAttenuation, deltaTime * AttenuationSmoothSpeed);
        }

        public int Read(float[] outBuffer, int count)
        {
            lock (_lock)
            {
                int toRead = Mathf.Min(count, _availableSamples);
                if (toRead == 0 || CurrentAttenuation <= 0f)
                    return 0;

                for (int i = 0; i < toRead; i++)
                {
                    outBuffer[i] = _circularBuffer[_readIndex] * CurrentAttenuation;
                    _readIndex = (_readIndex + 1) % _circularBuffer.Length;
                }

                _availableSamples -= toRead;
                return toRead;
            }
        }

        public static void ResizeTempBuffer(int needed)
        {
            if (TempBuffer.Length >= needed)
                return;
            TempBuffer = new float[needed];
        }
    }
}
