using UnityEngine;
using System;

public enum playerAction { Free, Attack, Guard, Parry, Recovery, Riposte, Down }
public enum defenceResult { Hit, Guarded, Parried }

public class playerCombat : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private beatClock clock;
    [SerializeField] private playerMove playerMoveScript;
    [SerializeField] private Renderer bodyRenderer; //check status with colour

    [Header("攻撃")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private double attackBeats = 1.0; //攻撃動作の長さ
    [SerializeField] private double justMs = 40; //JUST判定のズレ(ミリ秒)
    [SerializeField] private double goodMs = 90; //Good判定のズレ(ミリ秒)
    [SerializeField] private float falloffMs = 200f; //このms以上ズレたら最小倍率
    [SerializeField] private AnimationCurve damageCurve = AnimationCurve.Linear(0f, 2f, 1f, 0.5f); //X: 拍の頭からのズレ(0=ぴったり 1=falloffMs以上) Y:ダメージ倍率

    [Header("致命攻撃")]
    [SerializeField] private float riposteRange = 3f;
    [SerializeField] private double riposteBeats = 1.0f;
    [SerializeField] private float riposteDamage = 2f;

    [Header("パリィ")]
    [SerializeField] private double parryWindow = 0.12; //入力後に判定が続く長さ(秒)
    [SerializeField] private double parryLate = 0.06; //着弾より遅く入力した場合の猶予(秒)
    [SerializeField] private double parryRecovery = 0.4; //判定後の硬直(秒)

    public double parryLateTolerance => parryLate; //敵が「着弾の何秒後に判定を呼ぶか」を知るため

    [Header("防御")]
    [SerializeField] private float guardSpeedMultiplier = 0.4f;

    [Header("体力")]
    [SerializeField] private float maxHp = 200f;
    [SerializeField] private float guardDamageMultiplier = 0.3f;
    public float hp { get; private set; }

    public playerAction action { get; private set; } = playerAction.Free;

    private double stateEndDsp; //攻撃・硬直が終わるdspTime
    private double parryStartDsp = double.NegativeInfinity; //パリィ判定開始dspTime
    private double parryEndDsp; //パリィ判定終了dspTime

    //------入力------
    [Header("入力")]
    [SerializeField] private playerInput input;

    void Start()
    {
        hp = maxHp;
        ApplyView();
    }
    void Update()
    {
        double now = AudioSettings.dspTime;
        if (action == playerAction.Down) return; //倒れたら何もできない

        bool attackPressed = input.attack.pressed;
        bool parryPressed = input.parry.pressed;
        bool guardHeld = input.guard.held;

        switch (action)
        {
            case playerAction.Free:
            case playerAction.Guard:
                if (parryPressed) StartParry(now);
                else if (attackPressed)
                {
                    enemyBase riposteTarget = FindTarget(riposteRange, true);
                    if (riposteTarget != null) StartRiposte(now, riposteTarget);
                    else StartAttack(now);
                }
                else SetAction(guardHeld ? playerAction.Guard : playerAction.Free);
                break;

            case playerAction.Attack:
            case playerAction.Riposte:
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

        float t = Mathf.Clamp01((float)(abs / falloffMs)); //ズレを0~1に
        float mul = damageCurve.Evaluate(t);
        float damage = attackDamage * mul;
        
        Debug.Log($"Attack{rating}({offsetMs:+0;-0}ms) x{mul:0.00} = {damage:0.0}");

        enemyBase hitTarget = FindTarget(attackRange, false);
        if (hitTarget != null) hitTarget.TakeDamage(damage);
    }

    private void StartParry(double now)
    {
        parryStartDsp = now;
        parryEndDsp = now + parryWindow;
        stateEndDsp = parryEndDsp + parryRecovery;
        SetAction(playerAction.Parry);
    }

    //範囲内で最も近い敵を探す(requireStaggeredなら崩れている敵だけが対象)
    private enemyBase FindTarget(float range, bool requireStaggered)
    {
        enemyBase best = null;
        float bestDist = range;
        foreach(enemyBase e in enemyBase.all)
        {
            if (requireStaggered && !e.isStaggered) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if(d <= bestDist)
            {
                best = e;
                bestDist = d;
            }
        }

        return best;
    }

    private void StartRiposte(double now, enemyBase t)
    {
        stateEndDsp = now + riposteBeats * clock.secPerBeat;
        SetAction(playerAction.Riposte);
        Debug.Log("RIPOSTE!");
        t.TakeRiposte(riposteDamage);
    }

    /// <summary>
    /// 敵が攻撃の着弾時に呼ぶメソッド。引数は着弾時のDspTime
    /// </summary>
    /// <param name="impactDspTime"></param>
    /// <returns></returns>
    public defenceResult ReceiveAttack(double impactDspTime, float damage)
    {
        if (action == playerAction.Down) return defenceResult.Hit; //倒れた後は何もしない

        if(impactDspTime >= parryStartDsp - parryLate && impactDspTime <= parryStartDsp + parryWindow)
        {
            double ms = (impactDspTime - parryStartDsp) * 1000.0;
            Debug.Log($"Parry! (入力から着弾まで{ms:0}ms)");
            parryStartDsp = double.NegativeInfinity;
            SetAction(playerAction.Free);
            return defenceResult.Parried;
        }

        if(action == playerAction.Guard)
        {
            float taken = damage * guardDamageMultiplier;
            Debug.Log("GUARD");
            TakeDamage(taken);
            return defenceResult.Guarded;
        }

        Debug.Log("HIT!!");
        TakeDamage(damage);
        return defenceResult.Hit;
    }

    private void TakeDamage(float damage)
    {
        hp = Mathf.Max(hp - damage, 0f);
        if(hp <= 0f)
        {
            Debug.Log("PLAYER DOWN");
            SetAction(playerAction.Down);
        }
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
            case playerAction.Free:     playerMoveScript.speedMultiplier = 1f; SetColor(Color.white); break;
            case playerAction.Guard:    playerMoveScript.speedMultiplier = guardSpeedMultiplier; SetColor(Color.blue); break;
            case playerAction.Attack:   playerMoveScript.speedMultiplier = 0f; SetColor(Color.red); break;
            case playerAction.Parry:    playerMoveScript.speedMultiplier = 0f; SetColor(Color.cyan); break;
            case playerAction.Recovery: playerMoveScript.speedMultiplier = 0f; SetColor(Color.gray); break;
            case playerAction.Riposte:  playerMoveScript.speedMultiplier = 0f; SetColor(Color.magenta); break;
            case playerAction.Down:     playerMoveScript.speedMultiplier = 0f; SetColor(Color.black); break;
        }
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.Label(new Rect(10, 10, 300, 40), $"HP: {hp:0} / {maxHp:0}");
    }

    private void SetColor(Color c)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = c;
    }
}