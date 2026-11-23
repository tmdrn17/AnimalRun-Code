using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviour
{
    public static PlayerController instance;

    [Header(" Elements ")]
    [SerializeField] private AnimalSystem animalSystem;
    [SerializeField] private PlayerAnimator playerAnimator;

    [Header(" Settings ")]
    public float moveSpeed;
    [SerializeField] private float roadWidth;

    [Header(" Control ")]
    [SerializeField] private float slideSpeed;

    private bool canMove;

    private Vector3 clickedScreenPosition;
    private Vector3 clickedPlayerPosition;

    private void Awake()
    {
        if (instance != null)
            Destroy(gameObject);
        else
            instance = this;
    }
    
    private void Start()
    {
        GameManager.onGameStateChanged += GameStateChangedCallback;
    }

    private void OnEnable()
    {
        animalSystem = GetComponent<AnimalSystem>();
        playerAnimator = GetComponent<PlayerAnimator>();
    }

    private void OnDestroy()
    {
        GameManager.onGameStateChanged -= GameStateChangedCallback;
    }
    
    private void Update()
    {
        if (canMove)
        {
            MoveForward();
            ManageControl();
        }
    }

    private void GameStateChangedCallback(GameManager.GameState gameState)
    {
        if (gameState == GameManager.GameState.Game)
            StartMoving();
        else if (gameState == GameManager.GameState.Gameover)
            StopMoving();
        else if (gameState == GameManager.GameState.LevelComplete)
            StopMoving();
    }

    public void StartMoving()
    {
        canMove = true;

        playerAnimator.Run();
    }

    public void StopMoving()
    {
        canMove = false;

        playerAnimator.Idle();
    }

    private void MoveForward()
    {
        transform.position += Vector3.forward * (Time.deltaTime * moveSpeed);
    }

    private void ManageControl()
    {
        if(Input.GetMouseButtonDown(0))
        {
            clickedScreenPosition = Input.mousePosition;
            clickedPlayerPosition = transform.position;
        }
        else if (Input.GetMouseButton(0))
        {
            float xScreenDifference = Input.mousePosition.x - clickedScreenPosition.x;

            xScreenDifference /= Screen.width;
            xScreenDifference *= slideSpeed;

            Vector3 position = transform.position;
            position.x = clickedPlayerPosition.x + xScreenDifference;

            position.x = Mathf.Clamp(position.x, -roadWidth / 2 + animalSystem.GetAnimalGroupRadius(),
                roadWidth / 2 - animalSystem.GetAnimalGroupRadius());

            transform.position = position;
        }
    }
}
