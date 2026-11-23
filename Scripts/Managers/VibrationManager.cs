using UnityEngine;

public class VibrationManager : MonoBehaviour
{

    [Header(" Settings ")]
    private bool haptics;

    // Start is called before the first frame update
    private void Start()
    {
        PlayerDetection.onDoorsHit += Vibrate;
        GameManager.onGameStateChanged += GameStateChangedCallback;
    }

    private void OnDestroy()
    {
        PlayerDetection.onDoorsHit -= Vibrate;

        GameManager.onGameStateChanged -= GameStateChangedCallback;
    }

    private void GameStateChangedCallback(GameManager.GameState gameState)
    {
        if (gameState == GameManager.GameState.LevelComplete)
            Vibrate();
        else if (gameState == GameManager.GameState.Gameover)
            Vibrate();
    }

    private void Vibrate()
    {
        if(haptics)
            Taptic.Light();
    }

    public void DisableVibrations()
    {
        haptics = false;
    }

    public void EnableVibrations()
    {
        haptics = true;
    }
}
