using UnityEngine;

namespace Code.Network.Stream
{
    public class StreamLoadBalancer : MonoBehaviour
    {
        public static StreamLoadBalancer Instance { get; private set; }

        [Header("Frame size limits")] public int maxFrameSizeBytes = 16000;
        public int minFrameSizeBytes = 3500;

        private int _activeStreams;
        private int _allStreamsCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            var nstreams = FindObjectsByType<NetworkImageStream>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _allStreamsCount = nstreams.Length;
        }

        public void RegisterStream()
        {
            _activeStreams = Mathf.Max(0, _activeStreams + 1);
        }

        public void UnregisterStream()
        {
            _activeStreams = Mathf.Max(0, _activeStreams - 1);
        }

        public int GetTargetFrameSize()
        {
            if (_activeStreams <= 1)
                return maxFrameSizeBytes;

            int clamped = Mathf.Clamp(_activeStreams, 1, _allStreamsCount);
            float t = (clamped - 1) / (float)(_allStreamsCount - 1);

            return Mathf.RoundToInt(Mathf.Lerp(maxFrameSizeBytes, minFrameSizeBytes, t));
        }

        public int GetActiveStreams() => _activeStreams;
    }
}