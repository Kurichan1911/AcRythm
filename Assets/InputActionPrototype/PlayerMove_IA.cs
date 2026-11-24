using UnityEngine;

/// <summary>
/// InputAction 版のプレイヤー移動スクリプト（playerMove の比較用）。
/// 入力取得のみ PlayerInputReader 経由に変更しており、移動ロジックは既存と同一。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMove_IA : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private Transform cameraTransform;

    [Header("移動パラメータ")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 720f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private float verticalVelocity;

    /// <summary>攻撃や防御などアクション状態に応じた移動速度倍率</summary>
    public float speedMultiplier { get; set; } = 1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Vector2 input = inputReader != null ? inputReader.MoveInput : Vector2.zero;

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDir = forward * input.y + right * input.x;

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
        }

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = moveDir * moveSpeed * speedMultiplier + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
