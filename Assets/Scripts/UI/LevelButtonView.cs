using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One level card on the level select screen: level number, name, stars and a lock / "soon" state.
public sealed class LevelButtonView : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TMP_Text numberText;
    [SerializeField] TMP_Text bigNumberText;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] GameObject lockIcon;
    [SerializeField] StarRow starRow;
    [SerializeField] Color unlockedTextColor = new(0.96f, 0.92f, 0.82f, 1f);
    [SerializeField] Color lockedTextColor = new(0.55f, 0.53f, 0.66f, 1f);

    public Button Button => button;

    public void Bind(int level, string levelName, System.Action<int> onClick)
    {
        bool unlocked = ProgressService.IsUnlocked(level);
        bool available = SceneRouter.IsLevelAvailable(level);
        bool completed = ProgressService.IsCompleted(level);
        numberText.text = $"LEVEL {level + 1}";
        bigNumberText.text = unlocked ? (level + 1).ToString() : string.Empty;
        bigNumberText.color = completed ? new Color(0.96f, 0.72f, 0.24f, 1f) : unlockedTextColor;
        nameText.text = unlocked ? levelName : "???";
        numberText.color = nameText.color = unlocked ? unlockedTextColor : lockedTextColor;
        lockIcon.SetActive(!unlocked);
        starRow.gameObject.SetActive(unlocked && available);
        starRow.Set(completed ? ProgressService.GetStars(level) : 0);
        statusText.text = !unlocked ? "KİLİTLİ" : !available ? "YAKINDA" : completed ? "TAMAMLANDI" : "YENİ";
        statusText.color = !unlocked || !available ? lockedTextColor : completed ? new Color(0.5f, 0.8f, 0.43f, 1f) : new Color(0.96f, 0.72f, 0.24f, 1f);
        button.interactable = unlocked;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick(level));
    }
}
