using UnityEngine;

public class SoundsManager : MonoBehaviour
{
    [Header(" Sounds ")]
    [SerializeField] private AudioSource buttonSound;
    [SerializeField] private AudioSource doorHitSound;
    [SerializeField] private AudioSource levelCompleteSound;
    [SerializeField] private AudioSource gameoverSound;

    // Start is called before the first frame update
    private void Start()
    {
        PlayerDetection.onDoorsHit += PlayDoorHitSound;

        GameManager.onGameStateChanged += GameStateChangedCallback;
    }

    private void OnDestroy()
    {
        PlayerDetection.onDoorsHit -= PlayDoorHitSound;

        GameManager.onGameStateChanged -= GameStateChangedCallback;
    }

    // Update is called once per frame
    private void Update()
    {
        
    }

    private void GameStateChangedCallback(GameManager.GameState gameState)
    {
        if (gameState == GameManager.GameState.LevelComplete)
            levelCompleteSound.Play();
        else if (gameState == GameManager.GameState.Gameover)
            gameoverSound.Play();
    }

    private void PlayDoorHitSound()
    {
        doorHitSound.Play();
    }

    public void DisableSounds()
    {
        doorHitSound.volume = 0;
        levelCompleteSound.volume = 0;
        gameoverSound.volume = 0;
        buttonSound.volume = 0;
    }

    public void EnableSounds()
    {
        buttonSound.volume = 1;
        doorHitSound.volume = 1;
        levelCompleteSound.volume = 1;
        gameoverSound.volume = 1;
    }
}
