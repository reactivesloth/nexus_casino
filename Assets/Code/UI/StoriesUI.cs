using System;
using System.Collections;
using System.Collections.Generic;
using Code.API;
using Code.Network;
using Code.Network.InteractionSystem;
using Code.Utility;
using CurvedUI;
using Proyecto26;
using PurrNet;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Pool;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Code.UI
{
    public class StoriesUI : PurrMonoBehaviour
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
        [SerializeField] private GameObject loadingScreen;
        [SerializeField] private SlotMachineInteractable slotMachineInteractable;

        private Coroutine _storyCoroutine;
        private Coroutine _waitCoroutine;
        private bool _isCurved;

        private readonly List<Image> _progressBars = new List<Image>(16);
        private readonly List<GetStoryData> _stories = new List<GetStoryData>(16);
        private readonly Dictionary<int, Sprite> _idSpriteCache = new Dictionary<int, Sprite>(32);

        private ObjectPool<GameObject> _progressBarPool;

        private void OnConnectedToServer()
        {
            _isCurved = TryGetComponent(out CurvedUIRaycaster raycaster) || TryGetComponent(out CurvedUIVertexEffect vertexEffects);

            _progressBarPool = new ObjectPool<GameObject>(
                createFunc: CreateFunc,
                actionOnGet: ActionOnGet,
                actionOnRelease: ActionOnRelease,
                actionOnDestroy: Destroy,
                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 20
            );
        }

        GameObject CreateFunc() => Instantiate(progressBarPrefab, progressBarContainer);

        void ActionOnGet(GameObject go) => go.SetActive(true);

        void ActionOnRelease(GameObject go) => go.SetActive(false);

        public override void OnEnable()
        {
            if(loadingScreen != null)
                loadingScreen.SetActive(true);
            StartNewCycle();
        }

        public override  void OnDisable()
        {
            if (_storyCoroutine != null) { StopCoroutine(_storyCoroutine); _storyCoroutine = null; }
            if (_waitCoroutine != null) { StopCoroutine(_waitCoroutine); _waitCoroutine = null; }

            ClearCache();
            ClearProgressBars();
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            throw new NotImplementedException();
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            throw new NotImplementedException();
        }

        public void StartNewCycle()
        {
            TryFetchStories(() =>
            {
                if (_storyCoroutine != null) { StopCoroutine(_storyCoroutine); _storyCoroutine = null; }

                if (_stories.Count == 0)
                {
                    StartNewWaitStories();
                    return;
                }

                _storyCoroutine = StartCoroutine(PlayStories(_stories));
            });
        }

        private void TryFetchStories(Action onStoriesFetched)
        {
            var queryParams = new Dictionary<string, string> { { "limit", storiesPerCycle.ToString() } };
            if (slotMachineInteractable != null)
                queryParams["slot_id"] = slotMachineInteractable.IDNumber.ToString();

            var req = new RequestHelper
            {
                Uri = ApiRoutes.GetStoriesUrl(),
                Params = queryParams,
                Headers = ClientDataStorage.GetJwtHeader()
            };

            RestClient.Get(req).Then(resp =>
            {
                if (resp.StatusCode != 200) { StartNewWaitStories(); return; }

                var result = JsonUtility.FromJson<SuccessResponse<StoryCollection>>(resp.Text);
                if (result == null || !result.success)
                {
                    StartNewWaitStories();
                    return;
                }

                _stories.Clear();
                if (result.data != null && result.data.screenshots != null)
                    _stories.AddRange(result.data.screenshots);

                ClearOldSprites(); // держим кэш свежим
                onStoriesFetched?.Invoke();
            }).Catch(_ => { StartNewWaitStories(); });
        }

        private void StartNewWaitStories()
        {
            if (_waitCoroutine != null) { StopCoroutine(_waitCoroutine); _waitCoroutine = null; }
            _waitCoroutine = StartCoroutine(WaitForStoriesCoroutine());
        }

        private IEnumerator WaitForStoriesCoroutine()
        {
            yield return new WaitForSeconds(storyDisplayTime);
            StartNewCycle();
        }

        private IEnumerator PlayStories(List<GetStoryData> batch)
        {
            ClearProgressBars();
            _progressBars.Clear();

            // создать полоски прогресса
            for (int i = 0; i < batch.Count; i++)
            {
                var go = _progressBarPool.Get();
                go.transform.SetParent(progressBarContainer, false);
                go.transform.SetAsLastSibling();

                var fill = go.transform.GetChild(0).GetComponent<Image>();
                if (fill == null) continue;

                if (_isCurved)
                {
                    go.AddComponentIfMissing<CurvedUIVertexEffect>();
                    fill.AddComponentIfMissing<CurvedUIVertexEffect>();
                }

                fill.fillAmount = 0f;
                _progressBars.Add(fill);
            }

            // показ историй
            for (int i = 0; i < batch.Count; i++)
            {
                var story = batch[i];
                if (playerName != null)
                {
                    var n = story != null && !string.IsNullOrEmpty(story.user.username) ? story.user.username : "";
                    if (playerName.text != n) playerName.text = n;
                }

                if (story != null)
                    yield return LoadAndShowStoryImage(story);

                if (i < _progressBars.Count)
                    yield return AnimateProgressBar(_progressBars[i], storyDisplayTime);
                else
                    yield return new WaitForSeconds(storyDisplayTime);
            }

            StartNewCycle();
        }

        private IEnumerator LoadAndShowStoryImage(GetStoryData story)
        {
            if (story == null || image == null) yield break;

            // из кэша
            if (_idSpriteCache.TryGetValue(story.id, out var cached))
            {
                image.sprite = cached;
                yield break;
            }

            if(loadingScreen != null)
                loadingScreen.SetActive(true);
            
            string url = string.IsNullOrEmpty(story.image_url) ? "" : Uri.EscapeUriString(story.image_url);
            if (string.IsNullOrEmpty(url)) yield break;

            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if(loadingScreen != null)
                    loadingScreen.SetActive(false);
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("StoriesUI: download failed: " + req.error);
                    yield break;
                }

                var data = req.downloadHandler.data;
                var sprite = ImageUtility.CreateSpriteFromBytes(data);
                if (sprite != null)
                {
                    image.sprite = sprite;
                    if (!_idSpriteCache.ContainsKey(story.id))
                        _idSpriteCache.Add(story.id, sprite);
                }
            }
        }

        private IEnumerator AnimateProgressBar(Image bar, float duration)
        {
            if (bar == null || duration <= 0f) yield break;

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
            // удаляем из кэша те, которых нет в свежем списке
            if (_idSpriteCache.Count == 0) return;

            // построим список id актуальных историй
            // (без LINQ)
            var keepIds = new HashSet<int>();
            for (int i = 0; i < _stories.Count; i++)
                keepIds.Add(_stories[i].id);

            // соберём удаляемые
            var toRemove = new List<int>(_idSpriteCache.Count);
            foreach (var kv in _idSpriteCache)
                if (!keepIds.Contains(kv.Key)) toRemove.Add(kv.Key);

            // уничтожаем
            for (int i = 0; i < toRemove.Count; i++)
            {
                int id = toRemove[i];
                var spr = _idSpriteCache[id];
#if UNITY_EDITOR
                if (spr != null && spr.texture != null) Object.DestroyImmediate(spr.texture);
                if (spr != null) Object.DestroyImmediate(spr);
#else
                if (spr != null && spr.texture != null) Object.Destroy(spr.texture);
                if (spr != null) Object.Destroy(spr);
#endif
                _idSpriteCache.Remove(id);
            }
        }

        private void ClearCache()
        {
            foreach (var kv in _idSpriteCache)
            {
                var spr = kv.Value;
#if UNITY_EDITOR
                if (spr != null && spr.texture != null) Object.DestroyImmediate(spr.texture);
                if (spr != null) Object.DestroyImmediate(spr);
#else
                if (spr != null && spr.texture != null) Object.Destroy(spr.texture);
                if (spr != null) Object.Destroy(spr);
#endif
            }
            _idSpriteCache.Clear();
        }

        private void ClearProgressBars()
        {
            if (progressBarContainer == null) return;
            for (int i = progressBarContainer.childCount - 1; i >= 0; i--)
            {
                var child = progressBarContainer.GetChild(i);
                if (child != null) _progressBarPool.Release(child.gameObject);
            }
        }
    }
}
