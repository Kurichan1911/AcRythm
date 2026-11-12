using System.ComponentModel.Design.Serialization;
using UnityEngine.InputSystem;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class playerMove : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 7f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private float verticalVelocity;
    public float speedMultipilir { get; set; } = 1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector2 input = ReadMoveInput();

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

        Vector3 velocity = moveDir * moveSpeed * speedMultipilir + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 v = Vector2.zero;

        Keyboard kb = Keyboard.current;
        if(kb != null)
        {
            if (kb.wKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed) v.x -= 1f;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
            v += pad.leftStick.ReadValue();

        return Vector2.ClampMagnitude(v, 1f);
    }
}
