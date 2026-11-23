using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header(" Elements ")]
    public Transform player;

    [Header(" Settings ")]
    public float playerDistance;

    private void Update()
    {
        Vector3 newPosition = transform.position;
        newPosition.z = player.position.z - playerDistance; 
        transform.position = newPosition;
    }
}
