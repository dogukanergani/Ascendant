using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelSelectController : MonoBehaviour
{
    [SerializeField] LevelButtonView buttonPrefab;
    [SerializeField] Transform container;
    [SerializeField] Button backButton;
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button skillTreeButton;
    [SerializeField] SkillTreePanel skillTreePanel;

    void Awake()
    {
        skillTreeButton.onClick.AddListener(skillTreePanel.Open);
        backButton.onClick.AddListener(SceneRouter.LoadMenu);
        for (int i = 0; i < ProgressService.LevelCount; i++)
        {
            Instantiate(buttonPrefab, container).Bind(i, OnLevelClicked);
        }

        messageText.text = string.Empty;
    }

    void OnLevelClicked(int level)
    {
        if (!SceneRouter.LoadLevel(level))
        {
            messageText.text = "Level henüz hazır değil";
        }
    }
}
