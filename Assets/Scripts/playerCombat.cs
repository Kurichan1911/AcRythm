using UnityEngine;
using System;

public enum playerAction { Free, Holding, SubCharging, Attack, Guard, Parry, Recovery, Riposte, Down }
public enum defenceResult { Hit, Guarded, Parried }

public class playerCombat : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private beatClock clock;
    [SerializeField] private playerMove playerMoveScript;
    [SerializeField] private Renderer bodyRenderer; //check status with colour

    [Header("武器")]
    [SerializeField] private weaponData currentWeapon;
    public weaponData weapon => currentWeapon;
    public float currentAttackRange => currentWeapon != null ? currentWeapon.attackRange : attackRange;

    [Header("攻撃")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private double attackBeats = 1.0; //攻撃動作の長さ
    [SerializeField] private double justMs = 40; //JUST判定のズレ(ミリ秒)
    [SerializeField] private double goodMs = 90; //Good判定のズレ(ミリ秒)
    [SerializeField] private float falloffMs = 200f; //このms以上ズレたら最小倍率
    [SerializeField] private AnimationCurve damageCurve = AnimationCurve.Linear(0f, 2f, 1f, 0.5f); //X: 拍の頭からのズレ(0=ぴったり 1=falloffMs以上) Y:ダメージ倍率
    [SerializeField] private float holdSpeedMultiplier = 0.7f; //構えホールド中の移動速度倍率
    [SerializeField] private float subChargeSpeedMultiplier = 0.5f;
    [SerializeField] private Color subChargeColor = new Color(1f, 0.9f, 0.1f); //サブチャージ中の色

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
    private double holdTargetPressBeat;
    private double holdPressOffsetMs;
    private float holdPressMul;
    private double chainReadyDsp; //次の攻撃・パリィを先行受付できるdspTime
    private double lastTapTargetBeat = double.NegativeInfinity; //直前のタップ攻撃で判定した拍 二重打ち防止用

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

        bool subPressed = input.sub.pressed;
        bool subHeld = input.sub.held;
        bool attackPressed = input.attack.pressed;
        bool parryPressed = input.parry.pressed;
        bool guardHeld = input.guard.held;

        switch(action)
        {
            case playerAction.Free:
            case playerAction.Guard:
                if (parryPressed && CanParry()) StartParry(now);
                else if (subPressed && IsSubChargeWeapon()) SetAction(playerAction.SubCharging);
                else if (attackPressed) StartAttackOrRiposte(now);
                else SetAction(guardHeld ? playerAction.Guard : playerAction.Free);
                break;

            case playerAction.Holding:
                if (parryPressed && CanParry()) StartParry(now);
                else if (!input.attack.held) ReleaseHoldAttack(now);
                break;

            case playerAction.SubCharging:
                if (parryPressed && CanParry()) StartParry(now);
                else if (!IsSubChargeWeapon() || !subHeld)
                    SetAction(guardHeld ? playerAction.Guard : playerAction.Free);
                else if (attackPressed)
                    OnSubChargeAttackPressed(now);
                break;

            case playerAction.Attack:
                if (now >= stateEndDsp)
                    SetAction(playerAction.Free);
                else if (now >= chainReadyDsp)
                {
                    if (parryPressed && CanParry()) StartParry(now);
                    else if (attackPressed) StartAttackOrRiposte(now);
                }
                break;

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

    private void StartAttackOrRiposte(double now)
    {
        enemyBase riposteTarget = FindTarget(riposteRange, true);
        if (riposteTarget != null) StartRiposte(now, riposteTarget);
        else if (currentWeapon != null && currentWeapon.inputType == weaponInputType.Hold) StartHold(now);
        else StartAttack(now);
    }

    private void StartAttack(double now)
    {
        double durationBeats = currentWeapon != null ? currentWeapon.GetEffectiveBeats(currentWeapon.attackDuration, clock.secPerBeat) : attackBeats;
        SetAttackState(now, durationBeats);

        //ビートに合わせて最寄り拍とのズレを計算する(+=遅, -=早)
        double stepBeats = currentWeapon != null ? currentWeapon.GetEffectiveBeats(currentWeapon.rhythmStep, clock.secPerBeat) : 1.0;
        if (stepBeats <= 0.0) stepBeats = 1.0;

        double beat = BeatOf(now);
        if (beat < lastTapTargetBeat - stepBeats)
            lastTapTargetBeat = double.NegativeInfinity; //曲の再生し直し対策

        double targetBeat = Math.Round(beat / stepBeats) * stepBeats;
        if (targetBeat < lastTapTargetBeat + stepBeats * 0.5)
            targetBeat = Math.Round((lastTapTargetBeat + stepBeats) / stepBeats) * stepBeats; //同じ刻みでの二重判定を防止
        lastTapTargetBeat = targetBeat;

        double offsetMs = (beat - targetBeat) * clock.secPerBeat * 1000.0;
        double abs = Math.Abs(offsetMs);

        string rating = GetTimingRating(abs);
        float mul = EvaluateTimingMultiplier(abs);
        float weaponMul = currentWeapon != null ? currentWeapon.damageMultiplier : 1f;
        float damage = attackDamage * weaponMul * mul;

        string name = currentWeapon != null ? currentWeapon.weaponName : "なし";
        Debug.Log($"TapAttack [{name}] {rating}({offsetMs:+0;-0}ms) x{mul:0.00} = {damage:0.0}");

        ApplyAttackHit(damage);
    }

    private void SetAttackState(double now, double durationBeats)
    {
        double durationSec = durationBeats * clock.secPerBeat;
        double earlyChainSec = Math.Min(goodMs * 0.001, durationSec * 0.5);
        stateEndDsp = now + durationSec;
        chainReadyDsp = stateEndDsp - earlyChainSec;
        SetAction(playerAction.Attack);
    }

    private void StartHold(double now)
    {
        double stepBeats = currentWeapon.GetEffectiveBeats(currentWeapon.rhythmStep, clock.secPerBeat);
        if (stepBeats <= 0.0) stepBeats = 1.0;

        double pressBeat = BeatOf(now);
        holdTargetPressBeat = Math.Round(pressBeat / stepBeats) * stepBeats;
        holdPressOffsetMs = (pressBeat - holdTargetPressBeat) * clock.secPerBeat * 1000.0;
        holdPressMul = EvaluateTimingMultiplier(Math.Abs(holdPressOffsetMs));

        SetAction(playerAction.Holding);
    }

    private void ReleaseHoldAttack(double now)
    {
        if(currentWeapon == null)
        {
            SetAction(playerAction.Free);
            return;
        }

        double holdBeats = currentWeapon.GetEffectiveBeats(currentWeapon.holdLength, clock.secPerBeat);
        double targetReleaseBeat = holdTargetPressBeat + holdBeats;
        double releaseBeat = BeatOf(now);
        double releaseOffsetMs = (releaseBeat - targetReleaseBeat) * clock.secPerBeat * 1000.0;
        float releaseMul = EvaluateTimingMultiplier(Math.Abs(releaseOffsetMs));

        float timingMul = holdPressMul * currentWeapon.pressWeight + releaseMul * currentWeapon.releaseWeight;
        float damage = attackDamage * currentWeapon.damageMultiplier * timingMul;

        double durationBeats = currentWeapon.GetEffectiveBeats(currentWeapon.attackDuration, clock.secPerBeat);
        SetAttackState(now, durationBeats);

        string pressRating = GetTimingRating(Math.Abs(holdPressOffsetMs));
        string releaseRating = GetTimingRating(Math.Abs(releaseOffsetMs));
        Debug.Log($"HoldAttack [{currentWeapon.weaponName}] 押:{pressRating}({holdPressOffsetMs:+0;-0}ms) / 離:{releaseRating}({releaseOffsetMs:+0;-0}ms) x{timingMul:0.00} = {damage:0.0}");

        ApplyAttackHit(damage);
    }

    //攻撃の当たり判定(プロトタイプ中は距離で判定している。将来的には武器モデルのコライダー判定に差し替える)
    private void ApplyAttackHit(float damage)
    {
        enemyBase hitTarget = FindTarget(currentAttackRange, false);
        if (hitTarget != null) hitTarget.TakeDamage(damage);
    }

    private float EvaluateTimingMultiplier(double absOffsetMs)
    {
        float t = Mathf.Clamp01((float)(absOffsetMs / falloffMs));
        return damageCurve.Evaluate(t);
    }

    private string GetTimingRating(double absOffsetMs)
    {
        return absOffsetMs <= justMs ? "JUST" : absOffsetMs <= goodMs ? "GOOD" : "OFF";
    }

    private double BeatOf(double dsp)
    {
        return (dsp - clock.BeatToDspTime(0)) / clock.secPerBeat;
    }

    private bool CanParry()
    {
        return currentWeapon == null || currentWeapon.canParry;
    }

    private bool IsSubChargeWeapon()
    {
        return currentWeapon != null && currentWeapon.inputType == weaponInputType.SubCharge;
    }

    private void OnSubChargeAttackPressed(double now)
    {
        double stepBeats = currentWeapon.GetEffectiveBeats(currentWeapon.rhythmStep, clock.secPerBeat);
        if (stepBeats <= 0.0) stepBeats = 1.0;

        double beat = BeatOf(now);
        double targetBeat = Math.Round(beat / stepBeats) * stepBeats;
        double offsetMs = (beat - targetBeat) * clock.secPerBeat * 1000.0;
        string rating = GetTimingRating(Math.Abs(offsetMs));

        Debug.Log($"SubCharge Tap[{currentWeapon.weaponName}] {rating}({offsetMs:+0;-0}ms) ");
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
        float riposteMul = currentWeapon != null ? currentWeapon.riposteMultiplier : 1f;
        t.TakeRiposte(riposteDamage * riposteMul);
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

        //Debug.Log("HIT!!");
        if (action == playerAction.Holding || action == playerAction.SubCharging)
            SetAction(playerAction.Free);
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
            case playerAction.Free:         playerMoveScript.speedMultiplier = 1f; SetColor(Color.white); break;
            case playerAction.Holding:      playerMoveScript.speedMultiplier = holdSpeedMultiplier; SetColor(new Color(1f, 0.55f, 0f)); break;
            case playerAction.SubCharging:  playerMoveScript.speedMultiplier = subChargeSpeedMultiplier; SetColor(subChargeColor); break;
            case playerAction.Guard:        playerMoveScript.speedMultiplier = guardSpeedMultiplier; SetColor(Color.blue); break;
            case playerAction.Attack:       playerMoveScript.speedMultiplier = 0f; SetColor(Color.red); break;
            case playerAction.Parry:        playerMoveScript.speedMultiplier = 0f; SetColor(Color.cyan); break;
            case playerAction.Recovery:     playerMoveScript.speedMultiplier = 0f; SetColor(Color.gray); break;
            case playerAction.Riposte:      playerMoveScript.speedMultiplier = 0f; SetColor(Color.magenta); break;
            case playerAction.Down:         playerMoveScript.speedMultiplier = 0f; SetColor(Color.black); break;
        }
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.Label(new Rect(10, 10, 300, 40), $"HP: {hp:0} / {maxHp:0}");

        string weaponLabel = currentWeapon != null ? currentWeapon.weaponName : "なし";
        GUI.Label(new Rect(10, 45, 450, 40), $"武器: {weaponLabel} Range: {currentAttackRange:0.0}m");

        if (action == playerAction.SubCharging)
            GUI.Label(new Rect(10, 80, 450, 40), "* SUB CHARGE 中");
    }

    private void SetColor(Color c)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = c;
    }
}