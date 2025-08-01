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

    private bool canTalk = true;
    private bool previousCanTalk = false;

    private string deviceName;
    private const int sampleRate = 48000;
    private const int bufferSize = 16384; // ~0.34 секунд
    private readonly float transmitInterval = bufferSize / (float)sampleRate;

    private float[] audioBuffer;
    private int position;

    private AudioClip microphoneClip;
    private float[] sampleData;
    private float[] micDataBuffer;

    private Coroutine transmitCoroutine;
    private WaitForSeconds transmitWait;

    // Кэш трансформов игроков для поиска по clientId
    private readonly Dictionary<int, Transform> _playerTransformCache = new();

    // Буферы воспроизведения входящего голоса по senderClientId
    private readonly Dictionary<int, VoicePlayback> _playbacks = new();
    private readonly object _playbackLock = new();

    private void Awake()
    {
        // Ничего здесь не аллоцируем; делаем lazy в OnStartClient
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

        EnsureOutputClip(); // чтобы OnAudioFilterRead вызывался
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

        // Обновляем attenuation и чистим "мертвые" источники
        UpdatePlaybacks();
    }

    private void UpdatePlaybacks()
    {
        List<int> toRemove = null;
        Vector3 selfPos = transform.position;

        lock (_playbackLock)
        {
            foreach (var kv in _playbacks)
            {
                int senderId = kv.Key;
                VoicePlayback playback = kv.Value;

                // Если давно не было данных — удаляем
                if (Time.time - playback.LastReceivedTime > 5f)
                {
                    toRemove ??= new List<int>();
                    toRemove.Add(senderId);
                    continue;
                }

                // Обновляем позицию отправителя и attenuation
                Transform senderTransform = GetPlayerTransform(senderId);
                if (senderTransform != null)
                {
                    playback.SenderPosition = senderTransform.position;
                }
                playback.UpdateAttenuation(selfPos, playback.SenderPosition, proximityRange, VoiceChatType);
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
            // Создаём короткий зацикленный пустой клип, чтобы AudioSource работал и вызывал OnAudioFilterRead
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

        if (transmitWait == null)
            transmitWait = new WaitForSeconds(transmitInterval);

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

            TransmitAudioServerRpc(audioBuffer);

            yield return transmitWait;
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

    [ServerRpc(RequireOwnership = false)]
    private void TransmitAudioServerRpc(float[] audioData, Channel channel = Channel.Unreliable, NetworkConnection sender = null)
    {
        TransmitAudioObserversRpc(audioData, sender.ClientId);
    }

    [ObserversRpc]
    private void TransmitAudioObserversRpc(float[] audioData, int senderClientId, Channel channel = Channel.Unreliable)
    {
        int localClientId = NetworkManager.ClientManager.Connection.ClientId;
        if (senderClientId == localClientId)
            return;

        RegisterReceivedAudio(senderClientId, audioData);
    }

    private void RegisterReceivedAudio(int senderClientId, float[] audioData)
    {
        lock (_playbackLock)
        {
            if (!_playbacks.TryGetValue(senderClientId, out var playback))
            {
                playback = new VoicePlayback(sampleRate * 2); // буфер на ~2 секунды
                _playbacks[senderClientId] = playback;
            }

            // Кэшируем позицию отправителя сразу
            Transform senderTransform = GetPlayerTransform(senderClientId);
            if (senderTransform != null)
                playback.SenderPosition = senderTransform.position;

            // Добавляем данные (копируем, чтобы не было непредвиденных мутей)
            float[] copy = new float[audioData.Length];
            Array.Copy(audioData, copy, audioData.Length);
            playback.AddSamples(copy);
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

        // Не найден - убираем на будущее
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
        // Микшуем все активные воспроизведения
        int frameCount = data.Length / channels;
        // Локальный временный буфер для моно-семплов
        float[] temp = VoicePlayback.TempBuffer;
        if (temp.Length < frameCount)
            VoicePlayback.ResizeTempBuffer(frameCount);
        int actualFrameCount = frameCount;

        lock (_playbackLock)
        {
            foreach (var playback in _playbacks.Values)
            {
                // Читаем из буфера
                int read = playback.Read(temp, actualFrameCount);
                if (read == 0)
                    continue;

                // Добавляем в выходной буфер (стерео дублирование)
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

        // Простой лимит (можно расширить), чтобы не вылезать за [-1,1]
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 1f) data[i] = 1f;
            else if (data[i] < -1f) data[i] = -1f;
        }
    }

    // Внутренний класс, управляющий буфером входящего голоса
    private class VoicePlayback
    {
        private readonly object _lock = new();
        private float[] _circularBuffer;
        private int _writeIndex;
        private int _readIndex;
        private int _availableSamples;

        public Vector3 SenderPosition; // последняя известная позиция отправителя
        public float Attenuation = 1f; // текущая attenuation
        public float LastReceivedTime { get; private set; } = Time.time;

        // Общий временный буфер для чтения в OnAudioFilterRead (чтобы не аллоцировать для каждого)
        public static float[] TempBuffer = new float[4096];

        public VoicePlayback(int capacity)
        {
            _circularBuffer = new float[capacity];
        }

        public void AddSamples(float[] samples)
        {
            lock (_lock)
            {
                foreach (var s in samples)
                {
                    if (_availableSamples >= _circularBuffer.Length)
                    {
                        // Удаляем самый старый, чтобы сделать место
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

        public void UpdateAttenuation(Vector3 listenerPosition, Vector3 senderPosition, float proximityRange, ChatType chatType)
        {
            if (chatType == ChatType.Proximity)
            {
                float distance = Vector3.Distance(listenerPosition, senderPosition);
                if (distance > proximityRange)
                    Attenuation = 0f;
                else
                    Attenuation = 1f - Mathf.Clamp01(distance / proximityRange);
            }
            else
            {
                Attenuation = 1f;
            }
        }

        // Читает до count моно-семплов, кладёт в outBuffer, применяя attenuation
        // Возвращает фактически прочитанное
        public int Read(float[] outBuffer, int count)
        {
            lock (_lock)
            {
                int toRead = Mathf.Min(count, _availableSamples);
                if (toRead == 0 || Attenuation <= 0f)
                    return 0;

                for (int i = 0; i < toRead; i++)
                {
                    outBuffer[i] = _circularBuffer[_readIndex] * Attenuation;
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
