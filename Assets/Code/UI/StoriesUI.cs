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

        private List<GetStoryData> _stories = new();
        private Coroutine _storyCoroutine;
        private Coroutine _waitCoroutine;
        private List<Image> _progressBars = new();
        private bool _isCurved;

        private readonly Dictionary<int, Sprite> _idSpriteDictionaryCash = new();

        private void Awake()
        {
            _isCurved = TryGetComponent(out CurvedUIRaycaster _) || TryGetComponent(out CurvedUIVertexEffect _);
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

                _stories = responseResult.data.screenshots;
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

        private void StartNewCycle()
        {
            if (slotMachineInteractable && !slotMachineInteractable.IsUsing)
                return;

            if (_storyCoroutine != null)
                StopCoroutine(_storyCoroutine);

            TryFetchStories(() =>
            {
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

            StartNewCycle(); // новый цикл после показа всех историй
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
            print($"[Stories] ClearCash");
            foreach (var sprite in _idSpriteDictionaryCash.Values)
            {
                DestroyImmediate(sprite.texture);
                DestroyImmediate(sprite);
            }
            
            _idSpriteDictionaryCash.Clear();
        }
        
    }
}