using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.API;
using PlayFlow;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Network
{
    public class VivoxVoiceManager : MonoBehaviour
    {
        public const string LobbyChannelName = "lobbyChannel";

        // Check to see if we're about to be destroyed.
        private static object m_Lock = new object();
        private static VivoxVoiceManager m_Instance;

        //These variables should be set to the projects Vivox credentials if the authentication package is not being used
        //Credentials are available on the Vivox Developer Portal (developer.vivox.com) or the Unity Dashboard (dashboard.unity3d.com), depending on where the organization and project were made
        [SerializeField] private string key;
        [SerializeField] private string issuer;
        [SerializeField] private string domain;
        [SerializeField] private string server;

        public List <VivoxParticipant> Participants;
        
        /// <summary>
        /// Access singleton instance through this propriety.
        /// </summary>
        public static VivoxVoiceManager Instance
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_Instance == null)
                    {
                        // Search for existing instance.
                        m_Instance = (VivoxVoiceManager)FindObjectOfType(typeof(VivoxVoiceManager));

                        // Create new instance if one doesn't already exist.
                        if (m_Instance == null)
                        {
                            // Need to create a new GameObject to attach the singleton to.
                            var singletonObject = new GameObject();
                            m_Instance = singletonObject.AddComponent<VivoxVoiceManager>();
                            singletonObject.name = typeof(VivoxVoiceManager).ToString() + " (Singleton)";
                        }
                    }

                    // Make instance persistent even if its already in the scene
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
            
            VivoxService.Instance.ParticipantAddedToChannel += OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel += OnParticipantRemoved;
            Participants = new List<VivoxParticipant>();
            
            VivoxService.Instance.VivoxGlobalAudioSettings.PlatformAcousticEchoCancellationEnabled = false;
            VivoxService.Instance.VivoxGlobalAudioSettings.AudioClippingProtectorEnabled = true;
            VivoxService.Instance.VivoxGlobalAudioSettings.VivoxAcousticEchoCancellationEnabled = true;
            VivoxService.Instance.VivoxGlobalAudioSettings.AutomaticGainControlEnabled = true;
            VivoxService.Instance.VivoxGlobalAudioSettings.NoiseSuppressionEnabled = true;
            
            VivoxService.Instance.EnableAcousticEchoCancellation();
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
            VivoxService.Instance.ParticipantAddedToChannel -= OnParticipantAdded;
            VivoxService.Instance.ParticipantRemovedFromChannel -= OnParticipantRemoved;
            LogoutOfVivoxServiceAsync();
        }
        
        public async void LoginToVivox()
        {
            var correctedDisplayName = ClientDataStorage.UserData.username;
                
            var loginOptions = new LoginOptions
            {
                DisplayName = correctedDisplayName,
                ParticipantUpdateFrequency = ParticipantPropertyUpdateFrequency.FivePerSecond
            };
            await VivoxService.Instance.LoginAsync(loginOptions);
            ConnectToLobbyChannel();
            MuteLocalPlayer();
        }

        public void LogoutOfVivoxServiceAsync(bool rejoinAfter = false)
        {
            VivoxService.Instance.LogoutAsync();

            DisconnectFromLobbyChannel();
            
            if (rejoinAfter)
            {
                LoginToVivox();
            }
        }

        private void OnApplicationQuit()
        {
            LogoutOfVivoxServiceAsync();
        }
    }
}