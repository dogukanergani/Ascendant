using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LevelSelectController : MonoBehaviour
{
    [SerializeField] LevelButtonView buttonPrefab;
    [SerializeField] Transform container;
    [SerializeField] Button backButton;
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button skillTreeButton;
    [SerializeField] SkillTreePanel skillTreePanel;
    [SerializeField] string[] levelNames = { "Orc Mağarası", "Gölge Geçidi", "Kristal Mahzen", "Çürük Orman", "Yıldız Kulesi" };

    readonly System.Collections.Generic.List<LevelButtonView> cards = new();

    void Awake()
    {
        skillTreeButton.onClick.AddListener(skillTreePanel.Open);
        backButton.onClick.AddListener(SceneRouter.LoadMenu);
        for (int i = 0; i < ProgressService.LevelCount; i++)
        {
            LevelButtonView card = Instantiate(buttonPrefab, container);
            card.Bind(i, i < levelNames.Length ? levelNames[i] : $"Level {i + 1}", OnLevelClicked);
            cards.Add(card);
        }

        messageText.text = string.Empty;
    }

    void Start()
    {
        // Keyboard / gamepad start on the level the player should play next.
        int next = Mathf.Clamp(ProgressService.GetNextLevel(), 0, cards.Count - 1);
        EventSystem.current?.SetSelectedGameObject(cards[next].Button.gameObject);
    }

    void OnLevelClicked(int level)
    {
        if (!SceneRouter.LoadLevel(level))
        {
            messageText.text = "Level henüz hazır değil";
        }
    }
}
