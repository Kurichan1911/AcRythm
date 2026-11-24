using UnityEngine;

/// <summary>
/// InputAction 版の追従カメラスクリプト（followCamera の比較用）。
/// 視点操作・カーソル解除のみ PlayerInputReader 経由に変更している。
/// </summary>
public class FollowCamera_IA : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private Transform target;

    [Header("カメラ設定")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float pivotHeight = 1.5f;
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float stickSensitivity = 120f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 60f;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        yaw = target.eulerAngles.y;
        Cursor.lockState = CursorLockMode.Locked; // カーソルをロック
        Cursor.visible = false; // カーソルを非表示
    }

    private void LateUpdate()
    {
        if (inputReader != null && inputReader.UnlockCursorPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        Vector2 look = Vector2.zero;
        if (inputReader != null)
        {
            Vector2 rawLook = inputReader.LookInput;
            if (inputReader.IsLookFromMouse)
                look = rawLook * mouseSensitivity;
            else
                look = rawLook * stickSensitivity * Time.deltaTime;
        }

        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * pivotHeight;

        transform.position = pivot - rot * Vector3.forward * distance;
        transform.rotation = rot;
    }
}
