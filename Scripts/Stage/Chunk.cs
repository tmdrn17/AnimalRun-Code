using UnityEngine;

public class Chunk : MonoBehaviour
{
    [Header(" Settings ")]
    [SerializeField] private Vector3 size;

    public float GetLength()
    {
        return size.z;
    }
}
