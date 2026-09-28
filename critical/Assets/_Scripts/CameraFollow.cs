using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player;

    public Vector3 distancia = new Vector3(0, 4, -7);

    void LateUpdate()
    {
        transform.position = player.position + distancia;
    }
}