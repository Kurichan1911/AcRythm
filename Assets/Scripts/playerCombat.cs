using UnityEngine;
using System;
using UnityEngine.InputSystem;

public enum playerAction { Free, Attack, Guard, Parry, Recovery }
public enum defenceResult { Hit, Guarded, Parried }

public class playerCombat : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private beatClock clock;
    [SerializeField] private playerMove playerMove;
    [SerializeField] private Renderer bodyRenderer; //check status with colour

    [Header("攻撃")]
    [SerializeField] private double attackBeats = 1.0; //攻撃動作の長さ
    [SerializeField] private double justMs = 40; //JUST判定のズレ(ミリ秒)
    [SerializeField] private double goodMs = 90; //Good判定のズレ(ミリ秒)

    [Header("パリィ")]
    [SerializeField] private double parryWindow = 0.1; //入力後に判定が続く長さ(秒)
    [SerializeField] private double parryRecovery = 0.4; //入力後の硬直

    [Header("防御")]
    [SerializeField] private float guardSpeedMultiplier = 0.4f;

    public playerAction action { get; private set; } = playerAction.Free;

    private double stateEndDsp; //攻撃・硬直が終わるdspTime
    private double parryStartDsp; //パリィ判定開始dspTime
    private double parryEndDsp; //パリィ判定終了dspTime

    //------入力------
    private bool attackPressed =>
        (Mouse.current?.leftButton.wasPressedThisFrame ?? false) ||
        (Gamepad.current?.rightShoulder.wasPressedThisFrame ?? false);
    private bool parryPressed =>
        (Keyboard.current?.fKey.wasPressedThisFrame ?? false) ||
        (Gamepad.current?.leftTrigger.wasPressedThisFrame ?? false);
    private bool GuardHeld =>
        (Mouse.current?.rightButton.isPressed ?? false) ||
        (Gamepad.current?.leftShoulder.isPressed ?? false);

    void Start()
    {
        ApplyView();
    }
    void Update()
    {
        double now = AudioSettings.dspTime;

        switch(action)
        {
            case playerAction.Free:
            case playerAction.Guard:
                if (parryPressed) StartParry(now);
                else if (attackPressed) StartAttack(now);
                else SetAction(GuardHeld ? playerAction.Guard : playerAction.Free);
                break;

            case playerAction.Attack:
                if (now >= stateEndDsp) SetAction(playerAction.Free);
                break;

            case playerAction.Parry:
                if (now > parryEndDsp) SetAction(playerAction.Recovery);
                break;

            case playerAction.Recovery:
                if (now >= stateEndDsp) SetAction(playerAction.Free);
                break;
        }
    }

    private void StartAttack(double now)
    {
        stateEndDsp = now + attackBeats * clock.secPerBeat;
        SetAction(playerAction.Attack);

        //最寄りの拍とのズレ(ms) +=遅 -=早
        double beat = clock.currentBeat;
        double offsetMs = (beat - Math.Round(beat)) * clock.secPerBeat * 1000.0;
        double abs = Math.Abs(offsetMs);
        string rating = abs <= justMs ? "JUST" : abs <= goodMs ? "GOOD" : "OFF";
        Debug.Log($"Attack{rating}({offsetMs:+0;-0}ms");
        
    }

    private void StartParry(double now)
    {
        parryStartDsp = now;
        parryEndDsp = now + parryWindow;
        stateEndDsp = parryEndDsp + parryRecovery;
        SetAction(playerAction.Parry);
    }

    /// <summary>
    /// 敵が攻撃の着弾時に呼ぶメソッド。引数は着弾時のDspTime
    /// </summary>
    /// <param name="impactDspTime"></param>
    /// <returns></returns>
    public defenceResult ReceiveAttack(double impactDspTime)
    {
        if(action == playerAction.Parry && impactDspTime >= parryStartDsp && impactDspTime <= parryEndDsp)
        {
            double ms = (impactDspTime - parryStartDsp) * 1000.0;
            Debug.Log($"Parry! (入力から着弾まで{ms:0}ms");
            SetAction(playerAction.Free);
            return defenceResult.Parried;
        }

        if(action == playerAction.Guard)
        {
            Debug.Log("GUARD");
            return defenceResult.Guarded;
        }

        Debug.Log("HIT!!");
        return defenceResult.Hit;
    }

    private void SetAction(playerAction next)
    {
        if (action == next) return;
        action = next;
        ApplyView();
    }

    private void ApplyView()
    {
        switch(action)
        {
            case playerAction.Free:     playerMove.speedMultipilir = 1f; SetColor(Color.white); break;
            case playerAction.Guard:    playerMove.speedMultipilir = guardSpeedMultiplier; SetColor(Color.blue); break;
            case playerAction.Attack:   playerMove.speedMultipilir = 0f; SetColor(Color.red); break;
            case playerAction.Parry:    playerMove.speedMultipilir = 0f; SetColor(Color.cyan); break;
            case playerAction.Recovery: playerMove.speedMultipilir = 0f; SetColor(Color.gray); break;
        }
    }

    private void SetColor(Color c)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = c;
    }
}
