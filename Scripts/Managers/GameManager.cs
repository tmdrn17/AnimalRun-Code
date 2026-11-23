using UnityEngine;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;
    public static Action<GameState> onGameStateChanged;

    public enum GameState {Menu, Select, Game, LevelComplete, Gameover}

    [Header(" Elements ")]
    [SerializeField] private UIManager uiManager;

    public GameState gameState { get; private set; }

    private void Awake()
    {
        if (instance != null)
            Destroy(gameObject);
        else
            instance = this;
    }

    private void Start()
    {
        Time.timeScale = 1.0f;
    }

    private void Update()
    {
        PauseGame();
    }

    public void SetGameState(GameState gameState)
    {
        this.gameState = gameState;
        onGameStateChanged?.Invoke(gameState);
    }

    public bool IsGameState(GameState state)
    {
        return gameState == state;
    }

    private void PauseGame()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            switch (gameState)
            {
                case GameState.Menu:
                    uiManager.OpenPanel(PanelType.Exit);
                    break;

                case GameState.Select:
                case GameState.Game:
                    // 이미 멈춘 상태(일시정지 또는 증강 선택 중)라면 무시한다.
                    if (Time.timeScale == 0.0f)
                        break;

                    Time.timeScale = 0.0f;
                    uiManager.OpenPanel(PanelType.Pause);
                    break;

                case GameState.LevelComplete:
                    #if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
                    #else
                        Application.Quit();
                    #endif
                    break;

                case GameState.Gameover:
                    #if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
                    #else
                        Application.Quit();
                    #endif
                    break;

            }


        }
    }
}