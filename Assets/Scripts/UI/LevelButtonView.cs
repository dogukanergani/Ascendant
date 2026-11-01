using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelButtonView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TMP_Text numberText;
    [SerializeField] GameObject lockIcon;
    [SerializeField] StarRow starRow;
    [SerializeField] Image background;
    [SerializeField] Color openColor = new(0.25f, 0.45f, 0.8f, 1f);
    [SerializeField] Color lockedColor = new(0.22f, 0.22f, 0.27f, 1f);

    public void Bind(int level, System.Action<int> onClick)
    {
        bool unlocked = ProgressService.IsUnlocked(level);
        numberText.text = unlocked ? (level + 1).ToString() : string.Empty;
        lockIcon.SetActive(!unlocked);
        starRow.gameObject.SetActive(ProgressService.IsCompleted(level));
        starRow.Set(ProgressService.GetStars(level));
        background.color = unlocked ? openColor : lockedColor;
        button.interactable = unlocked;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(level));
    }
}
