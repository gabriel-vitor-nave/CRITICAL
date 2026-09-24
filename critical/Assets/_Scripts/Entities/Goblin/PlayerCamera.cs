using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    private Transform cameraTransform;

    private void Awake()
    {
        cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        Vector3 position = transform.position;

        position.z = cameraTransform.position.z;

        cameraTransform.position = position;
    }
}