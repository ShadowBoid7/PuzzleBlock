using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuPanel;
    public GameObject gamePanel;

    [Header("Game UI")]
    public TMP_Text levelText;
    public GameObject victoryText;
    public Button nextLevelButton;

    [Header("Buttons")]
    public Button startButton;
    public Button menuButton;
    public Button restartButton;
    public Button quitButton;

    [Header("Game Reference")]
    public PuzzleBlockGame game;

    void Awake()
    {
        // Liga botões
        startButton.onClick.AddListener(OnStart);
        menuButton.onClick.AddListener(OnMenu);
        restartButton.onClick.AddListener(OnRestart);
        nextLevelButton.onClick.AddListener(OnNextLevel);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);

        ShowMenu();
    }

    public void ShowMenu()
    {
        menuPanel.SetActive(true);
        gamePanel.SetActive(false);
    }

    public void ShowGame()
    {
        menuPanel.SetActive(false);
        gamePanel.SetActive(true);

        victoryText.SetActive(false);
        nextLevelButton.gameObject.SetActive(false);
    }

    void OnStart()
    {
        ShowGame();
        game.StartGameFromLevel(0);
        RefreshLevel(game.CurrentLevelIndex, game.LevelCount);
    }

    void OnMenu()
    {
        ShowMenu();
        game.StopGameVisuals();
    }

    void OnRestart()
    {
        victoryText.SetActive(false);
        nextLevelButton.gameObject.SetActive(false);

        game.RestartLevel();
        RefreshLevel(game.CurrentLevelIndex, game.LevelCount);
    }

    void OnNextLevel()
    {
        victoryText.SetActive(false);
        nextLevelButton.gameObject.SetActive(false);

        game.LoadNextLevel();
        RefreshLevel(game.CurrentLevelIndex, game.LevelCount);
    }

    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
    }

    public void OnVictory()
    {
        victoryText.SetActive(true);

        // Se houver próximo nível, mostrar botão
        bool hasNext = game.CurrentLevelIndex < game.LevelCount - 1;
        nextLevelButton.gameObject.SetActive(hasNext);
    }

    public void RefreshLevel(int current, int total)
    {
        if (levelText != null)
            levelText.text = $"Nível: {current + 1}/{total}";
    }
}

