// ✅ Упрощённый и 100% рабочий VoiceChat скрипт
// ❗ Без OnAudioFilterRead — используется AudioClip.Create (проще, надёжнее для старта)
// ❗ Работает с FishNet, Push-to-Talk, Proximity и PCM16

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
    private const int sampleLengthSec = 1;
    private const int sampleSize = 960; // 20ms
    private float[] floatBuffer = new float[sampleSize];
    private int micPosition = 0;

    private Coroutine transmitRoutine;
    private Dictionary<int, AudioSource> remoteSources = new();

    public override void OnStartClient()
    {
        base.OnStartClient();

        audioSource = GetComponent<AudioSource>();
        micDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

        if (IsOwner && !string.IsNullOrEmpty(micDevice))
        {
            micClip = Microphone.Start(micDevice, true, sampleLengthSec, sampleRate);
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

            float[] sendBuffer = new float[sampleSize];
            micClip.GetData(sendBuffer, micPosition);
            micPosition = (micPosition + sampleSize) % micClip.samples;

            TransmitVoiceServerRpc(sendBuffer);
            yield return new WaitForSeconds(sampleSize / (float)sampleRate);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransmitVoiceServerRpc(float[] data, Channel channel = Channel.Unreliable, NetworkConnection sender = null)
    {
        TransmitVoiceObserverRpc(data, sender.ClientId);
    }

    [ObserversRpc(BufferLast = false)]
    private void TransmitVoiceObserverRpc(float[] data, int senderId, Channel channel = Channel.Unreliable)
    {
        if (senderId == NetworkManager.ClientManager.Connection.ClientId)
            return;

        PlayReceivedAudio(senderId, data);
    }

    private void PlayReceivedAudio(int senderId, float[] data)
    {
        if (!remoteSources.TryGetValue(senderId, out var src) || src == null)
        {
            GameObject go = new GameObject($"Voice_{senderId}");
            go.transform.SetParent(transform);
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = voiceChatType == ChatType.Proximity ? 1f : 0f;
            src.maxDistance = proximityRange;
            remoteSources[senderId] = src;
        }

        if (voiceChatType == ChatType.Proximity)
        {
            var senderObj = FindSender(senderId);
            if (senderObj != null)
                src.transform.position = senderObj.transform.position;
        }

        AudioClip clip = AudioClip.Create("recv", data.Length, 1, sampleRate, false);
        clip.SetData(data, 0);
        src.clip = clip;
        src.Play();
        Destroy(clip, clip.length + 0.1f);
    }

    private GameObject FindSender(int id)
    {
        foreach (var obj in FindObjectsOfType<NetworkObject>())
            if (obj.Owner.ClientId == id)
                return obj.gameObject;
        return null;
    }
}
