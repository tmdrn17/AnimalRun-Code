using UnityEngine;
using System;

public class PlayerDetection : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private AnimalSystem animalSystem;
    [SerializeField] private float detectDistance;

    [Header(" Events ")]
    public static Action onDoorsHit;

    private void Update()
    {
        if(GameManager.instance.IsGameState(GameManager.GameState.Game))
            DetectedObject();
    }

    private void DetectedObject()
    {
        float groupRadius = animalSystem.GetAnimalGroupRadius();
        Vector3 center = transform.position;
        float frontZ = center.z + groupRadius;
        
        for (int i = ListManager.Doors.Count - 1; i >= 0; i--)
        {
            Doors door = ListManager.Doors[i];

            if (door.transform.position.z > frontZ)
                continue;

            int bonusAmount = door.GetBonusAmount(center.x);
            BonusType bonusType = door.GetBonusType(center.x);

            door.Disable();

            onDoorsHit?.Invoke();

            animalSystem.ApplyBonus(bonusType, bonusAmount);
        }
        
        float coinRange = groupRadius + detectDistance;
        float sqrCoinRange = coinRange * coinRange;
        
        for (int i = ListManager.Coins.Count - 1; i >= 0; i--)
        {
            Coin coin = ListManager.Coins[i];
            Vector3 offset = coin.transform.position - center;
            offset.y = 0;

            if (offset.sqrMagnitude > sqrCoinRange)
                continue;

            ListManager.Coins.RemoveAt(i);
            coin.gameObject.SetActive(false);
            DataManager.instance.AddExp(1);
        }

        if (FinishLine.instance.transform.position.z <= frontZ)
        {
            PlayerPrefs.SetInt("level", PlayerPrefs.GetInt("level") + 1);
            GameManager.instance.SetGameState(GameManager.GameState.LevelComplete);
        }
    }
}
