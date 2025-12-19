using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.API;
using PlayFlow;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace Code.Network
{
    public class VivoxVoiceManager : MonoBehaviour
    {
        public const string LobbyChannelName = "lobbyChannel";

        private static object m_Lock = new object();
        private static VivoxVoiceManager m_Instance;

        [SerializeField] private string key;
        [SerializeField] private string issuer;
        [SerializeField] private string domain;
        [SerializeField] private string server;

        public List <VivoxParticipant> Participants;
        
        public static VivoxVoiceManager Instance
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_Instance == null)
                    {
                        m_Instance = (VivoxVoiceManager)FindObjectOfType(typeof(VivoxVoiceManager));

                        if (m_Instance == null)
                        {
                            var singletonObject = new GameObject();
                            m_Instance = singletonObject.AddComponent<VivoxVoiceManager>();
                            singletonObject.name = typeof(VivoxVoiceManager).ToString() + " (Singleton)";
                        }
                    }

                    DontDestroyOnLoad(m_Instance.gameObject);
                    return m_Instance;
                }
            }
        }

        private async void Awake()
        {
            if (m_Instance != this && m_Instance != null)
            {
                Debug.LogWarning(
                    "Multiple VivoxVoiceManager detected in the scene. Only one VivoxVoiceManager can exist at a time. The duplicate VivoxVoiceManager will be destroyed.");
                Destroy(this);
            }

            var options = new InitializationOptions();
            if (CheckManualCredentials())
            {
                options.SetVivoxCredentials(server, domain, issuer, key);
            }
            
            await UnityServices.InitializeAsync(options);
            await VivoxService.Instance.InitializeAsync();

            VivoxService.Instance.AvailableInputDevicesChanged += OnAvailableInputDevicesChanged;
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
            
            Participants = new List<VivoxParticipant>();
            
            TrySelectBestInputDevice(); 
        }
        
        private void OnAvailableInputDevicesChanged()
        {
            Debug.Log("[VivoxVoiceManager] Input devices changed. Checking for valid microphone...");
            TrySelectBestInputDevice();
        }

        private void TrySelectBestInputDevice()
        {
            if (!VivoxService.Instance.IsLoggedIn) return;

            var current = VivoxService.Instance.ActiveInputDevice;
            if (current != null && !current.DeviceName.Contains("No Device") && !string.IsNullOrEmpty(current.DeviceName))
            {
                return;
            }

            var devices = VivoxService.Instance.AvailableInputDevices;
            var bestDevice = devices.FirstOrDefault(d => !d.DeviceName.Contains("No Device"));

            if (bestDevice != null)
            {
                Debug.Log($"[VivoxVoiceManager] Auto-switching input to: {bestDevice.DeviceName}");
                VivoxService.Instance.SetActiveInputDeviceAsync(bestDevice);
            }
        }

        private void OnParticipantAdded(VivoxParticipant participant)
        {
            if (Participants.Contains(participant)) return;
            
            Participants.Add(participant);
        }

        private void OnParticipantRemoved(VivoxParticipant participant)
        {
            if (!Participants.Contains(participant)) return;
            
            Participants.Remove(participant);
        }

        public VivoxParticipant GetParticipant(string n)
        {
            return Participants.FirstOrDefault(p => p.DisplayName == n);
        }

        public void SetLocalPosition(GameObject localObject)
        {
            if (PlayFlowLobbyManagerV2.Instance?.CurrentLobby == null) return;
            if (VivoxService.Instance == null || !VivoxService.Instance.IsLoggedIn) return;

            VivoxService.Instance.Set3DPosition(localObject, PlayFlowLobbyManagerV2.Instance.CurrentLobby.id);
        }

        public void MuteLocalPlayer()
        {
            VivoxService.Instance.MuteInputDevice();
        }

        public void UnmuteLocalPlayer()
        {
            VivoxService.Instance.UnmuteInputDevice();
        }

        public async Task LoginToVivoxAsync(string displayName)
        {
            if (VivoxService.Instance.IsLoggedIn)
            {
                Debug.Log("[VivoxVoiceManager] Already logged in.");
                return;
            }

            Debug.Log($"[VivoxVoiceManager] Logging in as {displayName}...");

            var loginOptions = new LoginOptions
            {
                DisplayName = displayName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
            };

            try 
            {
                await VivoxService.Instance.LoginAsync(loginOptions);
                Debug.Log("[VivoxVoiceManager] Login successful.");
                
                ApplyAudioProcessingSettings();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VivoxVoiceManager] Login failed: {e.Message}");
            }
        }
        
        public void OnMicrophonePermissionGranted()
        {
        #if !UNITY_ANDROID
            return; 
        #endif
            
            Debug.Log("[VivoxVoiceManager] Permission granted. Force restarting session for Android...");
            RestartVivoxSession();
        }
        
        public async void RestartVivoxSession()
        {
            if (!VivoxService.Instance.IsLoggedIn) return;

            Debug.Log("[VivoxVoiceManager] Restarting Vivox Session...");

            string oldName = ClientDataStorage.UserData.username;

            await VivoxService.Instance.LogoutAsync();

            await Task.Delay(500);

            await LoginToVivoxAsync(oldName);

            ConnectToLobbyChannel();
        }
        
        private void ApplyAudioProcessingSettings()
        {
            var audioSettings = VivoxService.Instance.VivoxGlobalAudioSettings;
            bool hasHardwareAEC = IsHardwareAECSupported();

            if (hasHardwareAEC)
            {
                Debug.Log("[VivoxVoiceManager] Configuring for Hardware AEC (Best Performance)");

                audioSettings.PlatformAcousticEchoCancellationEnabled = true;

                audioSettings.VivoxAcousticEchoCancellationEnabled = false;
            }
            else
            {
                Debug.Log("[VivoxVoiceManager] Configuring for Software AEC (Vivox Algorithm)");

                audioSettings.PlatformAcousticEchoCancellationEnabled = false;

                audioSettings.VivoxAcousticEchoCancellationEnabled = true;
            }

            audioSettings.NoiseSuppressionEnabled = true;
        
            audioSettings.AutomaticGainControlEnabled = true;
            audioSettings.AudioClippingProtectorEnabled = true;

            Debug.Log($"[VivoxVoiceManager] Audio Settings Applied: \n" +
                      $"PlatformAEC: {audioSettings.PlatformAcousticEchoCancellationEnabled}, \n" +
                      $"VivoxAEC: {audioSettings.VivoxAcousticEchoCancellationEnabled}");
        }
        
        private bool IsHardwareAECSupported()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var aecClass = new AndroidJavaClass("android.media.audiofx.AcousticEchoCanceler"))
            {
                bool available = aecClass.CallStatic<bool>("isAvailable");
                Debug.Log($"[VivoxVoiceManager] Hardware AEC Support: {available}");
                return available;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VivoxVoiceManager] Failed to check Hardware AEC: {e.Message}");
            return false;
        }
#else
            return false;
#endif
        }
        public void ConnectToLobbyChannel()
        {
            Participants.Clear();
            Debug.Log("[VivoxVoiceManager] Connecting to lobby channel]");
            VivoxService.Instance.JoinPositionalChannelAsync(PlayFlowLobbyManagerV2.Instance.CurrentLobby.id,
                ChatCapability.AudioOnly, new Channel3DProperties(40, 30, 1, AudioFadeModel.ExponentialByDistance), new ChannelOptions());
        }

        public void DisconnectFromLobbyChannel()
        {
            Participants.Clear();
            VivoxService.Instance.LeaveAllChannelsAsync();
        }

        private bool CheckManualCredentials()
        {
            return !(string.IsNullOrEmpty(issuer) && string.IsNullOrEmpty(domain) && string.IsNullOrEmpty(server));
        }

        private void OnDestroy()
        {
            if (VivoxService.Instance != null)
            {
                VivoxService.Instance.AvailableInputDevicesChanged -= OnAvailableInputDevicesChanged;
                VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
                VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            }
        }
    }
}