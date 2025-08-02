using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using Dissonance.Integrations.FishNet.Utils;
using FishNet.Transporting;

namespace Dissonance.Integrations.FishNet
{
    /// <summary>
    /// When added to the player prefab, allows Dissonance to automatically track
    /// the location of remote players for positional audio for games using the
    /// FishNet API.
    /// </summary>
    public class DissonanceFishNetPlayer
        : NetworkBehaviour, IDissonancePlayer
    {
        [Tooltip("This transform will be used in positional voice processing. If unset, then GameObject's transform will be used.")]
        [SerializeField] private Transform trackingTransform;

        private static Log _log = Logs.Create(LogCategory.Network, "FishNet Player Component");
        private static Log Log => _log;
#if UNITY_EDITOR
#pragma warning disable IDE0051
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void OnEnterPlaymodeInEditor(UnityEditor.EnterPlayModeOptions options)
        {
            if (options.HasFlag(UnityEditor.EnterPlayModeOptions.DisableDomainReload))
            {
                _log = Logs.Create(LogCategory.Network, "FishNet Player Component");
            }
        }
#pragma warning restore IDE0051
#endif

        private DissonanceFishNetComms _comms;

        public bool IsTracking { get; private set; }

        /// <summary>
        /// The name of the player
        /// </summary>
        /// <remarks>
        /// This is a syncvar, this means unity will handle setting this value.
        /// This is important for Join-In-Progress because new clients will join and instantly have the player name correctly set without any effort on our part.
        /// https://fish-networking.gitbook.io/docs/manual/guides/synchronizing/syncvar
        /// </remarks>
        private readonly SyncVar<string> _playerId = new(settings: new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.Observers));
        public string PlayerId { get { return _playerId.Value; } }

        public Vector3 Position => trackingTransform != null ? trackingTransform.position : transform.position;
        public Quaternion Rotation => trackingTransform != null ? trackingTransform.rotation : transform.rotation;
        public NetworkPlayerType Type
        {
            get
            {
                if (_comms == null || _playerId.Value == null)
                    return NetworkPlayerType.Unknown;
                return _comms.Comms.LocalPlayerName.Equals(_playerId.Value) ? NetworkPlayerType.Local : NetworkPlayerType.Remote;
            }
        }

        private void OnEnable()
        {
            _comms = DissonanceFishNetComms.Instance;
        }

        private void OnDisable()
        {
            if (IsTracking)
                StopTracking();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            _playerId.OnChange += OnPlayerIdChanged;
            if (_comms != null)
                _comms.Comms.LocalPlayerNameChanged += LocalPlayerNameChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            _playerId.OnChange -= OnPlayerIdChanged;
            if (_comms != null)
                _comms.Comms.LocalPlayerNameChanged -= LocalPlayerNameChanged;
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            if (this.IsOwner == false) return;

            if (_comms == null)
            {
                LoggingHelper.Logger.Error(
                    "cannot find DissonanceFishNetComms component in scene\r\n" +
                    "not placing a DissonanceFishNetComms component on a game object in the scene");
            }

            Log.Debug("Tracking `OnOwnershipClient` Name={0}", _comms.Comms.LocalPlayerName);

            if (_comms.Comms.LocalPlayerName != null)
                SetLocalPlayerNameAsOwner(_comms.Comms.LocalPlayerName);
        }

        private void LocalPlayerNameChanged(string playerName)
        {
            if (this.IsOwner)
            {
                // When LocalPlayerName changes, the Owner updates _playerId.
                SetLocalPlayerNameAsOwner(playerName);
            }
        }

        [Client(RequireOwnership = true)]
        private void SetLocalPlayerNameAsOwner(string playerName)
        {
            // At this stage, the value is only changed locally and is not synchronized. See: WritePermission.ClientUnsynchronized
            // To synchronize the value, use RpcSetPlayerName.
            this._playerId.Value = playerName;
            RestartTracking();

            // This method is called on the server. The owner sends a request to the server to update _playerId,
            // and when the server changes the value of _playerId, OnPlayerIdChanged is invoked.
            RpcSetPlayerName(playerName);
        }

        /// <summary>
        /// Invoking on client will cause it to run on the server 
        /// </summary>
        /// <param name="playerName">PlayerName</param>
        /// <param name="channel">The channel through which data is transmitted. Use Reliable to ensure no data loss occurs.</param>
        [ServerRpc(RequireOwnership = true)]
        private void RpcSetPlayerName(string playerName, Channel channel = Channel.Reliable)
        {
            // The server changes the value of _playerId, and since it is of the SyncVar type,
            // the OnPlayerIdChanged callback will be triggered on all clients
            _playerId.Value = playerName;
        }

        /// <summary>
        /// When the server changes the value of _playerId, it is run on all clients
        /// </summary>
        /// <param name="prev">Previous _playerId value</param>
        /// <param name="next">Current _playerId value</param>
        /// <param name="asServer">Indicates if the callback is occurring on the server or on the client.</param>
        private void OnPlayerIdChanged(string prev, string next, bool asServer)
        {
            // To enable tracking, clients except the owner call RestartTracking.
            // (The owner has already called this in SetLocalPlayerNameAsOwner)
            if (this.IsOwner == false)
                RestartTracking();
        }

        private void RestartTracking()
        {
            // We need the player name to be set on all the clients and then tracking to be started (on each client).

            // We need to stop and restart tracking to handle the name change
            if (IsTracking)
                StopTracking();

            // Perform the actual work
            StartTracking();
        }

        private void StartTracking()
        {
            if (IsTracking)
                throw Log.CreatePossibleBugException("Attempting to start player tracking, but tracking is already started", "31971B1F-52FD-4FCF-89E9-67A17A917921");

            if (_comms != null)
            {
                _comms.Comms.TrackPlayerPosition(this);
                IsTracking = true;
            }
        }

        private void StopTracking()
        {
            if (!IsTracking)
                throw Log.CreatePossibleBugException("Attempting to stop player tracking, but tracking is not started", "C7CF0174-0667-4F07-88E3-800ED652142D");

            if (_comms != null)
            {
                _comms.Comms.StopTracking(this);
                IsTracking = false;
            }
        }
    }
}