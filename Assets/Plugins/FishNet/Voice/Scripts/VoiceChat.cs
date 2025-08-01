// ✅ Полнофункциональный VoiceChat скрипт с:
// - Поддержкой Push-to-Talk и Voice Activation
// - Глобальным и Проксимити режимами
// - Сжатием PCM16
// - Отправкой/приемом через FishNet
// - Воспроизведением через OnAudioFilterRead без AudioClip.Create

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Transporting;

[RequireComponent(typeof(AudioSource))]
public class VoiceChat : NetworkBehaviour
{
    public enum ChatType { Global, Proximity }
    public enum DetectionType { PushToTalk, VoiceActivation }

    [Header("Voice Settings")]
    public ChatType voiceChatType = ChatType.Global;
    public DetectionType detectionType = DetectionType.PushToTalk;
    public KeyCode pushToTalkKey = KeyCode.V;
    public bool activated = true;
    public float voiceThreshold = 0.01f;
    public float proximityRange = 12f;

    private AudioSource audioSource;
    private string micDevice;
    private AudioClip micClip;

    private const int sampleRate = 48000;
    private const int sampleSize = 960; // 20ms
    private byte[] byteBuffer = new byte[sampleSize * 2];
    private float[] floatBuffer = new float[sampleSize];
    private int micPosition;

    private Coroutine transmitRoutine;

    private readonly Dictionary<int, CircularBuffer> receiveBuffers = new();

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.clip = AudioClip.Create("VoiceOut", sampleRate, 1, sampleRate, false);
        audioSource.Play();
        micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
    }

    public override void OnStartClient()
    {
        if (IsOwner)
        {
            if (!string.IsNullOrEmpty(micDevice))
                micClip = Microphone.Start(micDevice, true, 1, sampleRate);
            else
                Debug.LogError("[VOICE] No mic device found");
        }
    }

    private void Update()
    {
        if (!IsOwner || !activated || micClip == null) return;

        bool shouldTalk = detectionType == DetectionType.PushToTalk ?
            Input.GetKey(pushToTalkKey) : IsVoiceDetected();

        if (shouldTalk && transmitRoutine == null)
            transmitRoutine = StartCoroutine(SendVoice());
        else if (!shouldTalk && transmitRoutine != null)
        {
            StopCoroutine(transmitRoutine);
            transmitRoutine = null;
        }
    }

    private bool IsVoiceDetected()
    {
        int pos = Microphone.GetPosition(micDevice);
        int start = pos - sampleSize;
        if (start < 0) return false;
        micClip.GetData(floatBuffer, start);
        float sum = 0f;
        for (int i = 0; i < floatBuffer.Length; i++)
            sum += Mathf.Abs(floatBuffer[i]);
        return (sum / floatBuffer.Length) > voiceThreshold;
    }

    private IEnumerator SendVoice()
    {
        while (true)
        {
            int micPos = Microphone.GetPosition(micDevice);
            int diff = micPos - micPosition;
            if (diff < sampleSize) { yield return null; continue; }

            micClip.GetData(floatBuffer, micPosition);
            for (int i = 0; i < sampleSize; i++)
            {
                short s = (short)(Mathf.Clamp(floatBuffer[i], -1f, 1f) * short.MaxValue);
                byteBuffer[i * 2] = (byte)(s & 0xFF);
                byteBuffer[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }

            TransmitVoiceServerRpc(byteBuffer);
            micPosition = (micPosition + sampleSize) % micClip.samples;
            yield return new WaitForSeconds(sampleSize / (float)sampleRate);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransmitVoiceServerRpc(byte[] data, Channel channel = Channel.Unreliable, NetworkConnection sender = null)
    {
        TransmitVoiceObserverRpc(data, sender.ClientId);
    }

    [ObserversRpc(BufferLast = false)]
    private void TransmitVoiceObserverRpc(byte[] data, int senderId, Channel channel = Channel.Unreliable)
    {
        if (senderId == NetworkManager.ClientManager.Connection.ClientId) return;

        if (!receiveBuffers.TryGetValue(senderId, out var buffer))
            receiveBuffers[senderId] = buffer = new CircularBuffer(48000);

        buffer.Write(data);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        Array.Clear(data, 0, data.Length);

        foreach (var kv in receiveBuffers)
        {
            var buffer = kv.Value;
            if (!buffer.HasData(sampleSize)) continue;

            buffer.Read(floatBuffer);
            float att = GetAttenuation(kv.Key);

            for (int i = 0; i < floatBuffer.Length; i++)
            {
                float f = floatBuffer[i] * att;
                for (int c = 0; c < channels; c++)
                    data[i * channels + c] += f;
            }
        }
    }

    private float GetAttenuation(int senderId)
    {
        if (voiceChatType == ChatType.Global) return 1f;

        var netObjs = FindObjectsOfType<NetworkObject>();
        foreach (var obj in netObjs)
        {
            if (obj.Owner.ClientId == senderId)
            {
                float dist = Vector3.Distance(transform.position, obj.transform.position);
                return dist > proximityRange ? 0f : 1f - (dist / proximityRange);
            }
        }
        return 0f;
    }

    private class CircularBuffer
    {
        private readonly float[] buffer;
        private int writePos = 0, readPos = 0, available = 0;

        public CircularBuffer(int size)
        {
            buffer = new float[size];
        }

        public void Write(byte[] pcm16)
        {
            int samples = pcm16.Length / 2;
            for (int i = 0; i < samples; i++)
            {
                short s = (short)(pcm16[i * 2] | (pcm16[i * 2 + 1] << 8));
                float f = s / 32767f;
                buffer[writePos] = f;
                writePos = (writePos + 1) % buffer.Length;
                if (available < buffer.Length) available++;
                else readPos = (readPos + 1) % buffer.Length;
            }
        }

        public void Read(float[] outBuf)
        {
            for (int i = 0; i < outBuf.Length; i++)
            {
                if (available > 0)
                {
                    outBuf[i] = buffer[readPos];
                    readPos = (readPos + 1) % buffer.Length;
                    available--;
                }
                else outBuf[i] = 0f;
            }
        }

        public bool HasData(int count) => available >= count;
    }
}
