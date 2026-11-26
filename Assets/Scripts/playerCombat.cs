using UnityEngine;
using System;

public enum playerAction { Free, Holding, SubCharging, Attack, Guard, Parry, Recovery, Riposte, Down }
public enum defenceResult { Hit, Guarded, Parried, Countered }

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

    public double parryLateTolerance //敵が「着弾の何秒後に判定を呼ぶか」を知るため
    {
        get
        {
            double counterLate = (currentWeapon != null && currentWeapon.counterType != counterTrigger.None)
                ? currentWeapon.counterLateMs * 0.001
                : 0.0;
            return Math.Max(parryLate, counterLate);
        }
    }

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
    private double counterPressDsp = double.NegativeInfinity; //ホールド開始時刻 押しカウンター判定用
    private bool holdIsCounter; //押しカウンターが成立しているか判定用
    private bool pendingReleaseCounter; //離しカウンターの着弾待ち中か
    private double counterReleaseDsp = double.NegativeInfinity;　//ホールド終了時刻 離しカウンター判定用
    private double pendingReleaseResolveDsp; //この時刻まで着弾が無ければ通常攻撃とする
    private float pendingReleaseBaseDmg; //居合の基本ダメージ記憶
    private string pendingReleaseLogInfo; //通常確定時のログ文字列
    private bool attackIsCounter; //現在のAttack動作がカウンターか

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

    private void LateUpdate()
    {
        if (!pendingReleaseCounter) return;
        if (action == playerAction.Down)
        {
            pendingReleaseCounter = false;
            return;
        }
        if (AudioSettings.dspTime >= pendingReleaseResolveDsp)
            FlushPendingReleaseCounter();
    }

    private void FlushPendingReleaseCounter()
    {
        if (!pendingReleaseCounter) return;
        pendingReleaseCounter = false;
        Debug.Log(pendingReleaseLogInfo);
        ApplyAttackHit(pendingReleaseBaseDmg);
    }

    private void StartAttackOrRiposte(double now)
    {
        FlushPendingReleaseCounter();
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

    private void SetAttackState(double now, double durationBeats, bool isCounter = false)
    {
        double durationSec = durationBeats * clock.secPerBeat;
        double earlyChainSec = Math.Min(goodMs * GetTimingScale() * 0.001, durationSec * 0.5);
        stateEndDsp = now + durationSec;
        chainReadyDsp = stateEndDsp - earlyChainSec;
        attackIsCounter = isCounter;
        if (action == playerAction.Attack) ApplyView();
        else SetAction(playerAction.Attack);
    }

    private void StartHold(double now)
    {
        double stepBeats = currentWeapon.GetEffectiveBeats(currentWeapon.rhythmStep, clock.secPerBeat);
        if (stepBeats <= 0.0) stepBeats = 1.0;

        double pressBeat = BeatOf(now);
        holdTargetPressBeat = Math.Round(pressBeat / stepBeats) * stepBeats;
        holdPressOffsetMs = (pressBeat - holdTargetPressBeat) * clock.secPerBeat * 1000.0;
        holdPressMul = EvaluateTimingMultiplier(Math.Abs(holdPressOffsetMs));

        counterPressDsp = now;
        holdIsCounter = false;

        SetAction(playerAction.Holding);
    }

    private void ReleaseHoldAttack(double now)
    {
        if(currentWeapon == null)
        {
            holdIsCounter = false;
            SetAction(playerAction.Free);
            return;
        }

        double holdBeats = currentWeapon.GetEffectiveBeats(currentWeapon.holdLength, clock.secPerBeat);
        double targetReleaseBeat = holdTargetPressBeat + holdBeats;
        double releaseBeat = BeatOf(now);
        double releaseOffsetMs = (releaseBeat - targetReleaseBeat) * clock.secPerBeat * 1000.0;
        float releaseMul = EvaluateTimingMultiplier(Math.Abs(releaseOffsetMs));

        bool isPressCounter = holdIsCounter;
        holdIsCounter = false;

        float timingMul = holdPressMul * currentWeapon.pressWeight + releaseMul * currentWeapon.releaseWeight;
        //float counterMul = isCounter ? currentWeapon.counterDamageMultiplier : 1f;
        //float damage = attackDamage * currentWeapon.damageMultiplier * timingMul * counterMul;
        float baseDmg = attackDamage * currentWeapon.damageMultiplier * timingMul;

        string pressRating = GetTimingRating(Math.Abs(holdPressOffsetMs));
        string releaseRating = GetTimingRating(Math.Abs(releaseOffsetMs));
        string timingInfo = $"[{currentWeapon.weaponName}] 押:{pressRating}({holdPressOffsetMs:+0;-0}ms) / 離:{releaseRating}({releaseOffsetMs:+0;-0}ms)";

        double durationBeats = currentWeapon.GetEffectiveBeats(currentWeapon.attackDuration, clock.secPerBeat);

        //離しカウンター武器は早押し猶予分だけ着弾を待ってから確定する
        if (currentWeapon.counterType == counterTrigger.OnRelease)
        {
            FlushPendingReleaseCounter();
            SetAttackState(now, durationBeats, false);

            pendingReleaseCounter = true;
            counterReleaseDsp = now;
            pendingReleaseResolveDsp = now + currentWeapon.counterEarlyMs * 0.001 + parryLateTolerance;
            pendingReleaseBaseDmg = baseDmg;
            pendingReleaseLogInfo = $"HoldAttack {timingInfo} x{timingMul:0.00} = {baseDmg:0.0}";
            return;
        }

        float counterMul = isPressCounter ? currentWeapon.counterDamageMultiplier : 1f;
        float dmg = baseDmg * counterMul;
        SetAttackState(now, durationBeats, isPressCounter);

        string tag = isPressCounter ? "COUNTER HoldAttack" : "HoldAttack";
        Debug.Log($"{tag}{timingInfo} x {timingMul * counterMul:0.00} = {dmg:0.0}");

        ApplyAttackHit(dmg);
    }

    //攻撃の当たり判定(プロトタイプ中は距離で判定している。将来的には武器モデルのコライダー判定に差し替える)
    private void ApplyAttackHit(float damage)
    {
        enemyBase hitTarget = FindTarget(currentAttackRange, false);
        if (hitTarget != null) hitTarget.TakeDamage(damage);
    }

    private float GetTimingScale()
    {
        if (currentWeapon == null || currentWeapon.timingWindowMul <= 0f) return 1f;
        return currentWeapon.timingWindowMul;
    }

    private float EvaluateTimingMultiplier(double absOffsetMs)
    {
        float effectiveFallOff = falloffMs * GetTimingScale();
        float t = Mathf.Clamp01((float)(absOffsetMs / effectiveFallOff));
        return damageCurve.Evaluate(t);
    }

    private string GetTimingRating(double absOffsetMs)
    {
        float scale = GetTimingScale();
        return absOffsetMs <= justMs * scale ? "JUST" : absOffsetMs <= goodMs * scale ? "GOOD" : "OFF";
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
        holdIsCounter = false;
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

        if (action == playerAction.Holding
            && currentWeapon != null
            && currentWeapon.counterType == counterTrigger.OnPress)
        {
            double diffMs = (counterPressDsp - impactDspTime) * 1000.0;
            if(diffMs >= -currentWeapon.counterEarlyMs && diffMs <= currentWeapon.counterLateMs)
            {
                counterPressDsp = double.NegativeInfinity;
                holdIsCounter = true;
                ApplyView();
                float taken = damage * currentWeapon.counterTakenDamageMultiplier;
                Debug.Log($"Counter Block [{currentWeapon.weaponName}] ({diffMs:+0;-0}ms) 被ダメ:[{taken:0.0}]");
                TakeDamage(taken);
                return defenceResult.Countered;
            }
        }

        if (pendingReleaseCounter
            && currentWeapon != null
            && currentWeapon.counterType == counterTrigger.OnRelease)
        {
            double diffMs = (counterReleaseDsp - impactDspTime) * 1000.0;
            if (diffMs >= -currentWeapon.counterEarlyMs && diffMs <= currentWeapon.counterLateMs)
            {
                pendingReleaseCounter = false;
                counterReleaseDsp = double.NegativeInfinity;

                float counterDmg = pendingReleaseBaseDmg * currentWeapon.counterDamageMultiplier;
                float taken = damage * currentWeapon.counterTakenDamageMultiplier;

                attackIsCounter = true;
                ApplyView();

                Debug.Log($"IAI COUNTER [{currentWeapon.weaponName}] (離しズレ:{diffMs:+0;-0}ms) 被ダメ:[{taken:0.0}] 与ダメ:[{counterDmg:0.0}]");
                TakeDamage(taken);
                if (action != playerAction.Down)
                    ApplyAttackHit(counterDmg);

                return defenceResult.Countered;
            }
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
        {
            holdIsCounter = false;
            SetAction(playerAction.Free);
        }
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
            case playerAction.SubCharging:  playerMoveScript.speedMultiplier = subChargeSpeedMultiplier; SetColor(subChargeColor); break;
            case playerAction.Guard:        playerMoveScript.speedMultiplier = guardSpeedMultiplier; SetColor(Color.blue); break;
            case playerAction.Attack:       playerMoveScript.speedMultiplier = 0f; SetColor(attackIsCounter ? new Color(0.65f, 0.2f, 1f) : Color.red); break;
            case playerAction.Holding:      playerMoveScript.speedMultiplier = holdSpeedMultiplier; SetColor(holdIsCounter ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.55f, 0f)); break;
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
        else if (action == playerAction.Holding && holdIsCounter)
            GUI.Label(new Rect(10, 80, 450, 40), "* COUNTER READY");
        else if (action == playerAction.Attack && attackIsCounter)
            GUI.Label(new Rect(10, 80, 450, 40), "* COUNTER ATTACK");
    }

    private void SetColor(Color c)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = c;
    }
}