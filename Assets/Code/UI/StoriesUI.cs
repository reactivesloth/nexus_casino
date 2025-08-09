using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.API;
using Code.API.Models;
using Code.InteractionSystem;
using Code.Utility;
using CurvedUI;
using Proyecto26;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Pool;
using UnityEngine.UI;

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
        [SerializeField] private int storiesPerCycle = 5;
        [SerializeField] private float storyDisplayTime = 3f;

        [Header("Additional settings")]
        [SerializeField] private SlotMachineInteractable slotMachineInteractable;

        private Coroutine _storyCoroutine;
        private Coroutine _waitCoroutine;
        private bool _isCurved;

        private readonly List<Image> _progressBars = new();
        private readonly List<GetStoryData> _stories = new();
        private readonly Dictionary<int, Sprite> _idSpriteDictionaryCash = new();

        private ObjectPool<GameObject> _progressBarPool;

        private void Awake()
        {
            _isCurved = TryGetComponent(out CurvedUIRaycaster _) || TryGetComponent(out CurvedUIVertexEffect _);

            _progressBarPool = new ObjectPool<GameObject>(
                createFunc: () => Instantiate(progressBarPrefab, progressBarContainer),
                actionOnGet: bar => bar.SetActive(true),
                actionOnRelease: bar => bar.SetActive(false),
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 20
            );
        }

        private void OnEnable()
        {
            StartNewCycle();
        }

        private void OnDisable()
        {
            if (_storyCoroutine != null)
                StopCoroutine(_storyCoroutine);
            if (_waitCoroutine != null)
                StopCoroutine(_waitCoroutine);

            ClearCash();
            ClearProgressBars();
        }

        private void TryFetchStories(Action onStoriesFetched)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "limit", storiesPerCycle.ToString() }
            };

            if (slotMachineInteractable)
                queryParams.Add("slot_id", slotMachineInteractable.IDNumber.ToString());

            var getStoriesRequest = new RequestHelper
            {
                Uri = ApiRoutes.GetStoriesUrl(),
                Params = queryParams,
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Get(getStoriesRequest).Then(response =>
            {
                if (response.StatusCode != 200)
                {
                    StartNewWaitStories();
                    return;
                }

                var responseResult = JsonUtility.FromJson<SuccessResponse<StoryCollection>>(response.Text);

                if (responseResult.success != true)
                {
                    StartNewWaitStories();
                    return;
                }

                _stories.Clear();
                _stories.AddRange(responseResult.data.screenshots);
                ClearOldSprites();

                onStoriesFetched?.Invoke();
            }).Catch(error => { StartNewWaitStories(); });
        }

        private void StartNewWaitStories()
        {
            if (_waitCoroutine != null)
                StopCoroutine(_waitCoroutine);

            _waitCoroutine = StartCoroutine(WaitForStoriesCoroutine());
        }

        private IEnumerator WaitForStoriesCoroutine()
        {
            yield return new WaitForSeconds(storyDisplayTime);
            StartNewCycle();
        }

        public void StartNewCycle()
        {
            if (slotMachineInteractable && !slotMachineInteractable.IsUsing)
                return;

            TryFetchStories(() =>
            {
                if (_storyCoroutine != null)
                    StopCoroutine(_storyCoroutine);
                
                if (_stories == null || _stories.Count == 0)
                {
                    StartNewWaitStories();
                    return;
                }

                _storyCoroutine = StartCoroutine(PlayStories(_stories));
            });
        }

        private IEnumerator PlayStories(List<GetStoryData> batch)
        {
            ClearProgressBars();
            _progressBars.Clear();

            for (int i = 0; i < batch.Count; i++)
            {
                var go = _progressBarPool.Get();
                go.transform.SetParent(progressBarContainer, false);
                go.transform.SetAsLastSibling();
                var fillImage = go.transform.GetChild(0).GetComponent<Image>();

                if (_isCurved)
                {
                    go.AddComponentIfMissing<CurvedUIVertexEffect>();
                    fillImage.AddComponentIfMissing<CurvedUIVertexEffect>();
                }

                fillImage.fillAmount = 0f;
                _progressBars.Add(fillImage);
            }

            for (var i = 0; i < batch.Count; i++)
            {
                var story = batch[i];
                if (playerName.text != story.user.username)
                    playerName.text = story.user.username;

                var imageUrl = Uri.EscapeUriString(story.image_url);

                if (_idSpriteDictionaryCash.TryGetValue(story.id, out var cashedSprite))
                {
                    image.sprite = cashedSprite;
                }
                else
                {
                    using var imageLoadRequest = UnityWebRequest.Get(imageUrl);
                    yield return imageLoadRequest.SendWebRequest();
                    if (imageLoadRequest.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"Request failed: {imageLoadRequest.error}");
                    }
                    else
                    {
                        var texture = imageLoadRequest.downloadHandler.data;
                        image.sprite = ImageByteConverter.CreateSpriteFromBytes(texture);
                        _idSpriteDictionaryCash.TryAdd(story.id, image.sprite);
                    }
                }

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

        private void ClearOldSprites()
        {
            foreach (var storyId in _idSpriteDictionaryCash.Keys.Where(storyId =>
                         _stories.FirstOrDefault(s => s.id == storyId) == null))
            {
                var sprite = _idSpriteDictionaryCash[storyId];
                DestroyImmediate(sprite.texture);
                DestroyImmediate(sprite);
                _idSpriteDictionaryCash.Remove(storyId);
            }
        }

        private void ClearCash()
        {
            foreach (var sprite in _idSpriteDictionaryCash.Values)
            {
                if (sprite != null)
                {
                    if (sprite.texture != null)
                        DestroyImmediate(sprite.texture);
                    DestroyImmediate(sprite);
                }
            }

            _idSpriteDictionaryCash.Clear();
        }

        private void ClearProgressBars()
        {
            foreach (Transform child in progressBarContainer)
            {
                _progressBarPool.Release(child.gameObject);
            }
        }
    }
}