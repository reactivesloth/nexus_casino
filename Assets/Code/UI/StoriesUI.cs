using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Stories;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Code.UI
{
    public class StoriesUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private Transform progressBarContainer;
        [SerializeField] private GameObject progressBarPrefab;

        [Header("Story Settings")]
        [SerializeField] private int storiesPerCycle = 5; // Кол-во историй в одном круге
        [SerializeField] private float storyDisplayTime = 3f; // Секунд на одну историю

        private List<Story> _stories = new();
        private int _currentIndex = 0;
        private int _batchStartIndex = 0;
        private Coroutine _storyCoroutine;
        private List<Image> _progressBars = new();

        private void OnEnable()
        {
            InstanceFinder.ClientManager.OnAuthenticated += ClientManagerOnOnAuthenticated;
        }

        private void OnDisable()
        {
            InstanceFinder.ClientManager.OnAuthenticated -= ClientManagerOnOnAuthenticated;
            LocalStoriesStorage.Instance.StoriesUpdated -= OnStoriesUpdated;
        }

        private void ClientManagerOnOnAuthenticated()
        {
            _stories = LocalStoriesStorage.Instance.Stories.ToList();

            if (_stories.Count == 0)
            {
                LocalStoriesStorage.Instance.StoriesUpdated += OnStoriesUpdated;
                return;
            }

            StartNewCycle();
        }

        private void OnStoriesUpdated(List<Story> updatedStories)
        {
            if (updatedStories == null || updatedStories.Count == 0)
                return;

            LocalStoriesStorage.Instance.StoriesUpdated -= OnStoriesUpdated;
            _stories = updatedStories;
            StartNewCycle();
        }

        private void StartNewCycle()
        {
            if (_stories.Count == 0)
                return;

            var batch = new List<Story>();
            int total = _stories.Count;
            int count = Mathf.Min(storiesPerCycle, total);

            for (int i = 0; i < count; i++)
            {
                int index = total - 1 - ((_batchStartIndex + i) % total);
                batch.Add(_stories[index]);
            }

            _batchStartIndex = (_batchStartIndex + storiesPerCycle) % _stories.Count;

            if (_storyCoroutine != null)
                StopCoroutine(_storyCoroutine);

            _storyCoroutine = StartCoroutine(PlayStories(batch));
        }

        private IEnumerator PlayStories(List<Story> batch)
        {
            foreach (Transform child in progressBarContainer)
                Destroy(child.gameObject);

            _progressBars.Clear();

            for (int i = 0; i < batch.Count; i++)
            {
                var go = Instantiate(progressBarPrefab, progressBarContainer);
                var fillImage = go.transform.GetChild(0).GetComponent<Image>();
                fillImage.fillAmount = 0f;
                _progressBars.Add(fillImage);
            }

            for (int i = 0; i < batch.Count; i++)
            {
                var story = batch[i];
                playerName.text = story.playerName;
                image.sprite = story.ScreenshotSprite;

                yield return AnimateProgressBar(_progressBars[i], storyDisplayTime);
            }

            StartNewCycle();
        }

        private IEnumerator AnimateProgressBar(Image bar, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                bar.fillAmount = t / duration;
                t += Time.deltaTime;
                yield return null;
            }
            bar.fillAmount = 1f;
        }
    }
}