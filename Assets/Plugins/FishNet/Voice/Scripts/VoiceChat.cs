using System.Collections;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Transporting;

/// <summary>
/// Compact, low‑latency voice chat component for Fish‑Net.
/// ▸ 20‑мс пакеты (960 семплов) передаются по каналу UnreliableFragmented.
/// ▸ Воспроизведение через AudioSource.PlayOneShot не прерывает предыдущие пакеты.
/// </summary>
public class VoiceChat : NetworkBehaviour
{
    public enum ChatType { Global, Proximity }
    public ChatType VoiceChatType = ChatType.Global;

    public enum DetectionType { PushToTalk, VoiceActivation }
    public DetectionType VoiceDetectionType = DetectionType.PushToTalk;

    [Header("Runtime")]
    public bool Activated = true;
    public KeyCode PushToTalkKey = KeyCode.V;

    [Header("Audio settings")]
    public AudioSource source;
    public float proximityRange = 10f;
    [Range(0f, 0.01f)]
    public float voiceActivationThreshold = 0.002f;

    // ───────────────────────────────── CONSTANTS ─────────────────────────────────
    private const int sampleRate = 48_000;   // Гц
    private const int packetMs   = 20;       // длительность одного пакета
    private const int bufferSize = sampleRate * packetMs / 1000; // 960 семплов

    // ───────────────────────────────── STATE ─────────────────────────────────────
    private bool canTalk;
    private bool previousCanTalk;

    private string deviceName;
    private int position;                    // позиция чтения в циклическом буфере микрофона

    private AudioClip micClip;               // клип микрофона (ring‑buffer 1с)
    private Coroutine talkRoutine;

    // буферы
    private float[] audioBuffer;             // исходящий пакет
    private float[] sampleData;              // временный буфер для VAD
    private float[] micDataBuffer;           // для визуализации уровня

    // ──────────────────────────────── LIFECYCLE ──────────────────────────────────
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) return;

        if (source == null)
            Debug.LogError("[VOICE] AudioSource not assigned!");

        deviceName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (string.IsNullOrEmpty(deviceName))
            Debug.LogError("[VOICE] No microphone device found!");

        // allocate buffers
        audioBuffer    = new float[bufferSize];
        sampleData     = new float[bufferSize];
        micDataBuffer  = new float[bufferSize];

        source.playOnAwake = false;
    }

    private void Update()
    {
        if (!Activated || !IsOwner) return;

        // переключение устройств во время работы
        string selectedDevice = MicrophoneManager.Instance.GetCurrentDeviceName();
        if (selectedDevice != deviceName)
            UpdateMicrophone(selectedDevice);

        // определяем, разрешено ли говорить
        switch (VoiceDetectionType)
        {
            case DetectionType.PushToTalk:
                canTalk = Input.GetKey(PushToTalkKey);
                break;

            case DetectionType.VoiceActivation:
                if (micClip == null) StartMicrophone();
                canTalk = IsVoiceActivated();
                break;
        }

        // управление жизненным циклом микрофона (PTT)
        if (VoiceDetectionType == DetectionType.PushToTalk)
        {
            if (canTalk && micClip == null)
            {
                StartMicrophone();
                StartTalking();
            }
            else if (!canTalk && micClip != null)
            {
                StopTalking();
                StopMicrophone();
            }
        }

        // старт/стоп для voice activation
        if (VoiceDetectionType == DetectionType.VoiceActivation)
        {
            if (!previousCanTalk && canTalk) StartTalking();
            if (previousCanTalk  && !canTalk) StopTalking();
        }

        previousCanTalk = canTalk;
    }

    // ─────────────────────────── MICROPHONE CONTROL ─────────────────────────────
    private void StartMicrophone()
    {
        if (string.IsNullOrEmpty(deviceName)) return;

        position = 0;
        // ring buffer на 1 секунду достаточно, чтобы избежать переполнения
        micClip = Microphone.Start(deviceName, true, 1, sampleRate);
    }

    private void StopMicrophone()
    {
        if (micClip == null) return;
        Microphone.End(deviceName);
        micClip = null;
    }

    private void UpdateMicrophone(string newDeviceName)
    {
        StopTalking();
        StopMicrophone();

        deviceName = newDeviceName;
        if (canTalk)
        {
            StartMicrophone();
            StartTalking();
        }
    }

    // ───────────────────────────── TRANSMIT AUDIO ───────────────────────────────
    private void StartTalking()
    {
        if (talkRoutine == null && micClip != null)
            talkRoutine = StartCoroutine(TransmitVoice());
    }

    private void StopTalking()
    {
        if (talkRoutine != null)
        {
            StopCoroutine(talkRoutine);
            talkRoutine = null;
        }
    }

    private IEnumerator TransmitVoice()
    {
        var wait = new WaitForSeconds(packetMs / 1000f);
        while (canTalk && micClip != null)
        {
            int micPos = Microphone.GetPosition(deviceName);
            if (micPos < position) position = micPos; // перешли границу кольцевого буфера

            if (position + bufferSize > micPos) { yield return null; continue; }

            micClip.GetData(audioBuffer, position);
            position = (position + bufferSize) % micClip.samples;

            TransmitAudioServerRpc(audioBuffer);
            yield return wait;
        }
    }

    // ───────────────────────────── VOICE ACTIVATION ─────────────────────────────
    private bool IsVoiceActivated()
    {
        if (micClip == null) return false;

        int micPos = Microphone.GetPosition(deviceName);
        int start  = micPos - bufferSize;
        if (start < 0) return false; // мало данных

        micClip.GetData(sampleData, start);

        float sum = 0f;
        for (int i = 0; i < sampleData.Length; i++)
            sum += Mathf.Abs(sampleData[i]);

        return (sum / sampleData.Length) > voiceActivationThreshold;
    }

    // ──────────────────────────────── NETWORKING ────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    private void TransmitAudioServerRpc(float[] audioData, NetworkConnection sender = null)
    {
        TransmitAudioObserversRpc(audioData, sender.ClientId);
    }

    [ObserversRpc]
    private void TransmitAudioObserversRpc(float[] audioData, int senderClientId)
    {
        // не воспроизводим собственный голос
        if (senderClientId == NetworkManager.ClientManager.Connection.ClientId)
            return;
        Debug.Log($"[VOICE] TransmitAudioObserversRpc. {audioData.Length} bytes");

        PlayReceivedAudio(audioData, senderClientId);
    }

    // ─────────────────────────────── PLAYBACK ───────────────────────────────────
    private void PlayReceivedAudio(float[] audioData, int senderClientId)
    {
        if (source == null) return;

        if (VoiceChatType == ChatType.Proximity)
        {
            source.spatialBlend = 1f;
            source.maxDistance = proximityRange;

            Transform senderTf = GetPlayerTransform(senderClientId);
            if (senderTf != null && Vector3.Distance(transform.position, senderTf.position) > proximityRange)
                return; // далеко, не воспроизводим
        }
        else
        {
            source.spatialBlend = 0f; // 2D
        }

        // создаём крошечный клип и играем без прерывания текущего
        AudioClip clip = AudioClip.Create("pkt", audioData.Length, 1, sampleRate, false);
        clip.SetData(audioData, 0);
        Debug.Log($"[VOICE] PlayReceivedAudio. {clip.length} bytes");
        source.PlayOneShot(clip);
    }

    private Transform GetPlayerTransform(int clientId)
    {
        foreach (var obj in FindObjectsOfType<NetworkObject>())
            if (obj.Owner.ClientId == clientId)
                return obj.transform;
        return null;
    }

    // ──────────────────────────── DEBUG / VISUAL ───────────────────────────────
    private float GetMicInputVolume()
    {
        if (micClip == null) return 0f;

        int micPos = Microphone.GetPosition(deviceName);
        int start  = micPos - bufferSize;
        if (start < 0) return 0f;

        micClip.GetData(micDataBuffer, start);

        float sum = 0f;
        for (int i = 0; i < micDataBuffer.Length; i++)
            sum += micDataBuffer[i] * micDataBuffer[i];

        return Mathf.Clamp(Mathf.Sqrt(sum / micDataBuffer.Length) * 50f, 0f, 1f);
    }
}
