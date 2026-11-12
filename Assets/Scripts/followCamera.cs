using UnityEngine;
using UnityEngine.InputSystem;

public class followCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 5f;
    [SerializeField] private float pivotHeight = 1.5f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float stickSensitivity = 120f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;

    private float yaw;
    private float pitch = 15f;

    void Start()
    {
        yaw = target.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked; //hide cursor.
        Cursor.visible = false; //lock cursor.
    }

    private void LateUpdate()
    {
        if(Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 look = Vector2.zero;
        if (Mouse.current != null)
            look += Mouse.current.delta.ReadValue() * mouseSensitivity;
        if (Gamepad.current != null)
            look += Gamepad.current.rightStick.ReadValue() * mouseSensitivity;

        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * pivotHeight;

        transform.position = pivot - rot * Vector3.forward * distance;
        transform.rotation = rot;
    }

}
