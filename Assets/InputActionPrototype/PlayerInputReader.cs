using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// InputActionAsset を仲介してプレイヤー操作を読み取る管理クラス。
/// 既存スクリプトを変更せずに比較検証できるよう独立して実装しており、
/// main 統合時は各スクリプトのデバイス直接参照をこのクラス経由に差し替えられる。
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    [Header("Input Action アセット")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMapName = "Player";

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction attackAction;
    private InputAction parryAction;
    private InputAction guardAction;
    private InputAction unlockCursorAction;

    /// <summary>移動入力（WASD / 左スティック、長さ1以下にクランプ済み）</summary>
    public Vector2 MoveInput => moveAction != null
        ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f)
        : Vector2.zero;

    /// <summary>視点操作入力（マウス移動量 / 右スティック）</summary>
    public Vector2 LookInput => lookAction != null
        ? lookAction.ReadValue<Vector2>()
        : Vector2.zero;

    /// <summary>現在の視点入力がマウス由来かどうか</summary>
    public bool IsLookFromMouse => lookAction?.activeControl?.device is Mouse;

    /// <summary>このフレームで攻撃ボタンが押されたか</summary>
    public bool AttackPressedThisFrame => attackAction != null && attackAction.WasPressedThisFrame();

    /// <summary>このフレームでパリィボタンが押されたか</summary>
    public bool ParryPressedThisFrame => parryAction != null && parryAction.WasPressedThisFrame();

    /// <summary>ガードボタンが押され続けているか</summary>
    public bool IsGuardHeld => guardAction != null && guardAction.IsPressed();

    /// <summary>このフレームでカーソル解除キーが押されたか</summary>
    public bool UnlockCursorPressedThisFrame => unlockCursorAction != null && unlockCursorAction.WasPressedThisFrame();

    /// <summary>攻撃入力イベント（イベント駆動で使いたい場合）</summary>
    public event Action AttackPerformed;

    /// <summary>パリィ入力イベント（イベント駆動で使いたい場合）</summary>
    public event Action ParryPerformed;

    /// <summary>カーソル解除入力イベント（イベント駆動で使いたい場合）</summary>
    public event Action UnlockCursorPerformed;

    private void Awake()
    {
        if (inputActions == null)
        {
            Debug.LogError("[PlayerInputReader] InputActionAsset が Inspector で設定されていません。", this);
            return;
        }

        playerMap = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (playerMap == null)
        {
            Debug.LogError($"[PlayerInputReader] ActionMap '{actionMapName}' が見つかりません。", this);
            return;
        }

        moveAction = playerMap.FindAction("Move");
        lookAction = playerMap.FindAction("Look");
        attackAction = playerMap.FindAction("Attack");
        parryAction = playerMap.FindAction("Parry");
        guardAction = playerMap.FindAction("Guard");
        unlockCursorAction = playerMap.FindAction("UnlockCursor");
    }

    private void OnEnable()
    {
        if (playerMap == null) return;

        if (attackAction != null) attackAction.performed += OnAttackPerformed;
        if (parryAction != null) parryAction.performed += OnParryPerformed;
        if (unlockCursorAction != null) unlockCursorAction.performed += OnUnlockCursorPerformed;

        playerMap.Enable();
    }

    private void OnDisable()
    {
        if (playerMap == null) return;

        if (attackAction != null) attackAction.performed -= OnAttackPerformed;
        if (parryAction != null) parryAction.performed -= OnParryPerformed;
        if (unlockCursorAction != null) unlockCursorAction.performed -= OnUnlockCursorPerformed;

        playerMap.Disable();
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx) => AttackPerformed?.Invoke();
    private void OnParryPerformed(InputAction.CallbackContext ctx) => ParryPerformed?.Invoke();
    private void OnUnlockCursorPerformed(InputAction.CallbackContext ctx) => UnlockCursorPerformed?.Invoke();
}
