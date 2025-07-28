using System;
using System.Collections;
using System.Collections.Generic;
using Code.InteractionSystem;
using Code.Stories;
using CurvedUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class StoriesUI : MonoBehaviour
    {
        [Header("UI References")] [SerializeField]
        private Image image;

        [SerializeField] private TMP_Text playerName;
        [SerializeField] private Transform progressBarContainer;
        [SerializeField] private GameObject progressBarPrefab;

        [Header("Story Settings")] [SerializeField]
        private int storiesPerCycle = 5;

        [SerializeField] private float storyDisplayTime = 3f;

        [Header("Additional settings")] [SerializeField]
        private SlotMachineInteractable slotMachineInteractable;

        private List<Story> _stories = new();
        private int _batchStartIndex = 0;
        private Coroutine _storyCoroutine;
        private Coroutine _waitCoroutine;
        private List<Image> _progressBars = new();

        private bool _isCurved;
        
        private void Awake()
        {
            _isCurved = TryGetComponent(out CurvedUIRaycaster _) || TryGetComponent(out CurvedUIVertexEffect _);
        }

        private void OnEnable()
        {
            StartNewCycle();
        }

        private IEnumerator WaitForStoriesCoroutine()
        {
            while (_stories.Count == 0)
            {
                _stories = LocalStoriesStorage.Instance.GetStories(storiesPerCycle,
                    slotMachineInteractable ? slotMachineInteractable.IDNumber : -1);
                if (_stories.Count > 0)
                {
                    StartNewCycle();
                    yield break;
                }

                yield return new WaitForSeconds(storyDisplayTime);
            }
        }

        private void StartNewCycle()
        {
            _stories = LocalStoriesStorage.Instance?.GetStories(storiesPerCycle,
                slotMachineInteractable ? slotMachineInteractable.IDNumber : -1);

            if (_stories == null) return;
            
            if (_stories.Count == 0)
            {
                if (_waitCoroutine != null)
                    StopCoroutine(_waitCoroutine);
                _waitCoroutine = StartCoroutine(WaitForStoriesCoroutine());
                return;
            }

            if (_storyCoroutine != null)
                StopCoroutine(_storyCoroutine);

            _storyCoroutine = StartCoroutine(PlayStories(_stories));
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
                
                if (_isCurved)
                {
                    go.AddComponentIfMissing<CurvedUIVertexEffect>();
                    fillImage.AddComponentIfMissing<CurvedUIVertexEffect>();
                }
                
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