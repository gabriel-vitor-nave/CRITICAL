using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    private Transform cameraTransform;

    private void Awake()
    {
        cameraTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        Vector3 targetPosition = transform.position + offset;

        cameraTransform.position = Vector3.Lerp(
            cameraTransform.position,
            targetPosition,
            smoothSpeed * Time.deltaTime
        );
    }
}