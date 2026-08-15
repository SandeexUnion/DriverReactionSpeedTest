using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button button;
    [SerializeField] private Image lockIcon;
    [SerializeField] private Image completedIcon;

    private string sceneName;
    private System.Action<string> onLevelSelected;

    public void Initialize(int levelNumber, string sceneName, System.Action<string> callback)
    {
        this.sceneName = sceneName;
        this.onLevelSelected = callback;

        levelText.text = levelNumber.ToString();

        // ѕровер€ем доступность уровн€ (можно расширить системой сохранений)
        bool isUnlocked = IsLevelUnlocked(levelNumber);
        bool isCompleted = IsLevelCompleted(levelNumber);

        button.interactable = isUnlocked;
        lockIcon.gameObject.SetActive(!isUnlocked);
        completedIcon.gameObject.SetActive(isCompleted);

        if (isUnlocked)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private bool IsLevelUnlocked(int levelNumber)
    {
        // «десь можно добавить логику разблокировки уровней
        // Ќапример, через PlayerPrefs или систему сохранений
        return levelNumber == 1 || PlayerPrefs.GetInt($"Level_{levelNumber - 1}_Completed", 0) == 1;
    }

    private bool IsLevelCompleted(int levelNumber)
    {
        // ѕровер€ем пройден ли уровень
        return PlayerPrefs.GetInt($"Level_{levelNumber}_Completed", 0) == 1;
    }

    private void OnButtonClick()
    {
        onLevelSelected?.Invoke(sceneName);
    }

    private void OnDestroy()
    {
        button.onClick.RemoveAllListeners();
    }
}