using Code.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class StoryElement: MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text automatNumberText;
        [SerializeField] private Image screenshotImage;
        
        public Story Story;

        public void Initialize(Story story)
        {
            Story = story;
            titleText.text = story.playerName;
            automatNumberText.text = story.automatId.ToString();
            screenshotImage.sprite = story.ScreenshotSprite;
        }
    }
}