using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class SimpleMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject mainMenuPanel;
    public GameObject levelsPanel;
    public Button startButton;
    public Button backButton;
    public GameObject levelButtonPrefab;
    public Transform levelsGrid;

    [Header("Settings")]
    public string levelPrefix = "LEVEL"; // Префикс для поиска уровней

    private List<string> levelScenes = new List<string>();

    private void Start()
    {
        startButton.onClick.AddListener(ShowLevelsMenu);
        backButton.onClick.AddListener(ShowMainMenu);
        FindLevelScenes();
    }

    private void FindLevelScenes()
    {
        levelScenes.Clear();

        int totalScenes = SceneManager.sceneCountInBuildSettings;

        for (int i = 0; i < totalScenes; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            // Ищем все сцены, которые начинаются с указанного префикса
            if (sceneName.StartsWith(levelPrefix))
            {
                levelScenes.Add(sceneName);
            }
        }

        Debug.Log($"Найдено уровней: {levelScenes.Count}");
    }

    private void ShowLevelsMenu()
    {
        mainMenuPanel.SetActive(false);
        levelsPanel.SetActive(true);
        CreateLevelButtons();
    }

    private void ShowMainMenu()
    {
        levelsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    private void CreateLevelButtons()
    {
        // Очищаем старые кнопки
        foreach (Transform child in levelsGrid)
        {
            Destroy(child.gameObject);
        }

        // Создаем новые кнопки
        for (int i = 0; i < levelScenes.Count; i++)
        {
            GameObject buttonObj = Instantiate(levelButtonPrefab, levelsGrid);
            Button button = buttonObj.GetComponent<Button>();
            Text text = buttonObj.GetComponentInChildren<Text>();

            if (text != null)
            {
                text.text = $"Уровень {i + 1}";
            }

            // Сохраняем имя сцены в переменную для использования в лямбде
            string sceneName = levelScenes[i];
            button.onClick.AddListener(() => LoadLevel(sceneName));
        }
    }

    private void LoadLevel(string sceneName)
    {
        Debug.Log($"Загружаем сцену: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}