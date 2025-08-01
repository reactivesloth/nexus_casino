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
    public ChatType VoiceChatType = ChatType.Global;

    public enum DetectionType { PushToTalk, VoiceActivation }
    public DetectionType VoiceDetectionType = DetectionType.PushToTalk;

    [Header("General")]
    public bool Activated = true;
    public KeyCode PushToTalkKey = KeyCode.V;

    [Header("Proximity Settings")]
    public float proximityRange = 10f;
    [Header("Voice Activation")]
    public float voiceActivationThreshold = 0.002f;

    private AudioSource _audioSource;
    private string _deviceName;
    private const int SampleRate = 48000;
    private const int BufferSize = 16384;
    private float[] _audioBuffer;
    private int _writePosition;
    private AudioClip _microphoneClip;

    // Playback reuse
    private bool _playbackInitialized = false;
    private AudioClip _playbackClip;

    // Coroutine reuse
    private WaitForSeconds _sendDelay;
    private Coroutine _transmitCoroutine;

    private bool _canTalk;
    private bool _previousCanTalk;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner) return;

        // Owner-only initialization for microphone capture
        _deviceName = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        if (string.IsNullOrEmpty(_deviceName))
            Debug.LogError("[VOICE] No microphone device found!");

        _audioBuffer = new float[BufferSize];
        _writePosition = 0;

        _sendDelay = new WaitForSeconds(BufferSize / (float)SampleRate);
    }

    private void Update()
    {
        if (!Activated || !IsOwner) return;

        // Handle mic device change if you have a manager
        string selectedDevice = _deviceName; // e.g. MicrophoneManager.Instance.GetCurrentDeviceName();
        if (selectedDevice != _deviceName)
        {
            ChangeMicrophone(selectedDevice);
        }

        // Determine talking state
        switch (VoiceDetectionType)
        {
            case DetectionType.PushToTalk:
                _canTalk = Input.GetKey(PushToTalkKey);
                break;
            case DetectionType.VoiceActivation:
                if (_microphoneClip == null)
                    StartMicrophone();
                _canTalk = CheckVoiceLevel();
                break;
        }

        // Transition
        if (!_previousCanTalk && _canTalk) StartTalking();
        if (_previousCanTalk && !_canTalk) StopTalking();
        _previousCanTalk = _canTalk;
    }

    private void ChangeMicrophone(string newDevice)
    {
        StopTalking();
        StopMicrophone();
        _deviceName = newDevice;
        if (_canTalk)
            StartTalking();
    }

    private void StartMicrophone()
    {
        if (string.IsNullOrEmpty(_deviceName) || _microphoneClip != null) return;
        _writePosition = 0;
        _microphoneClip = Microphone.Start(_deviceName, true, 10, SampleRate);
    }

    private void StopMicrophone()
    {
        if (string.IsNullOrEmpty(_deviceName) || _microphoneClip == null) return;
        Microphone.End(_deviceName);
        Destroy(_microphoneClip);
        _microphoneClip = null;
    }

    private void StartTalking()
    {
        if (_microphoneClip == null)
            StartMicrophone();

        if (_transmitCoroutine == null)
            _transmitCoroutine = StartCoroutine(TransmitVoice());
    }

    private void StopTalking()
    {
        if (_transmitCoroutine != null)
        {
            StopCoroutine(_transmitCoroutine);
            _transmitCoroutine = null;
        }
        StopMicrophone();
    }

    private IEnumerator TransmitVoice()
    {
        while (_canTalk)
        {
            if (_microphoneClip == null) yield break;

            int micPos = Microphone.GetPosition(_deviceName);
            if (micPos < _writePosition)
                _writePosition = micPos;

            int needed = BufferSize;
            while (_writePosition + needed > micPos)
            {
                yield return null;
                micPos = Microphone.GetPosition(_deviceName);
            }

            _microphoneClip.GetData(_audioBuffer, _writePosition);
            _writePosition = (_writePosition + BufferSize) % _microphoneClip.samples;

            // Send to server
            TransmitAudioServerRpc(_audioBuffer);

            yield return _sendDelay;
        }
    }

    private bool CheckVoiceLevel()
    {
        int micPos = Microphone.GetPosition(_deviceName);
        int start = micPos - BufferSize;
        if (start < 0) return false;

        float[] temp = new float[BufferSize];
        _microphoneClip.GetData(temp, start);
        float sum = 0f;
        for (int i = 0; i < temp.Length; i++) sum += Mathf.Abs(temp[i]);
        return (sum / temp.Length) > voiceActivationThreshold;
    }

    [ServerRpc(RequireOwnership = false)]
    private void TransmitAudioServerRpc(float[] audioData, Channel channel = Channel.Unreliable, NetworkConnection sender = null)
    {
        TransmitAudioObserversRpc(audioData, sender.ClientId);
    }

    [ObserversRpc]
    private void TransmitAudioObserversRpc(float[] audioData, int senderClientId, Channel channel = Channel.Unreliable)
    {
        // Don't play on sender
        if (senderClientId == NetworkManager.ClientManager.Connection.ClientId)
            return;

        // Lazy-init playback clip
        if (!_playbackInitialized)
        {
            _playbackClip = AudioClip.Create("VoiceReceiver", BufferSize, 1, SampleRate, false);
            _audioSource.clip = _playbackClip;
            _playbackInitialized = true;
        }

        // Proximity adjustment
        if (VoiceChatType == ChatType.Proximity)
        {
            _audioSource.spatialBlend = 1f;
            _audioSource.maxDistance = proximityRange;
        }
        else
        {
            _audioSource.spatialBlend = 0f;
        }

        _playbackClip.SetData(audioData, 0);
        _audioSource.Play();
    }

    private void OnDestroy()
    {
        if (IsOwner)
        {
            StopTalking();
        }

        // Cleanup playback clip
        if (_playbackInitialized && _audioSource.clip != null)
            Destroy(_audioSource.clip);
    }
}
