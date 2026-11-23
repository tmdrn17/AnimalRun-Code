using UnityEngine;

public class Coin : MonoBehaviour
{
    private void Start()
    {
        ListManager.Coins.Add(this);
    }
    
    private void OnDestroy()
    {
        ListManager.Coins.Remove(this);
    }
}