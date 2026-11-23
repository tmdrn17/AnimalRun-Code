using System;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    public static FinishLine instance;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
        {
            Destroy(gameObject);
        }
    }
}