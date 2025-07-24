using System.Collections.Generic;
using Code.Stories;
using FishNet;
using UnityEngine;

namespace Code.UI
{
    public class StoriesBookUI : MonoBehaviour
    {
        [SerializeField] private Transform storiesContainer;
        [SerializeField] private StoryElement storyPrefab;

        private void Awake()
        {
            InstanceFinder.ClientManager.OnAuthenticated += ClientManagerOnOnAuthenticated;
        }

        private void ClientManagerOnOnAuthenticated()
        {
            UpdateShowStories(LocalStoriesStorage.Instance.Stories);
        }

        private void OnEnable()
        {
            LocalStoriesStorage.Instance.StoriesUpdated += InstanceOnStoriesUpdated;
        }

        private void OnDisable()
        {
            LocalStoriesStorage.Instance.StoriesUpdated -= InstanceOnStoriesUpdated;
        }

        private void InstanceOnStoriesUpdated(List<Story> stories)
        {
            UpdateShowStories(stories);
        }

        private void UpdateShowStories(List<Story> stories)
        {
            foreach (Transform storyElement in storiesContainer)
                Destroy(storyElement.gameObject);

            foreach (var story in stories)
            {
                var newElement = Instantiate(storyPrefab, storiesContainer);
                newElement.Initialize(story);
            }
        }
    }
}