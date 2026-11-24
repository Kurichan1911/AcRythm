using System;
using UnityEngine;

/// <summary>
/// InputAction 版のプレイヤー戦闘スクリプト（playerCombat の比較用）。
/// ・リズムに乗った攻撃ボタン入力（JUST/GOOD）およびパリィ成功でチャージ加算
/// ・リズム外し・ビート見逃し・パリィ失敗時にチャージ1以上あればチャージ消費で固定エリアの回転切りを発動（チャージ0の初発失敗は不発）
/// ・回転切り発動の瞬間、床に敵と同じ円を表示し 0.5 秒かけてフェードアウト
/// ・発動前に被弾した場合はチャージをリセット
/// </summary>
public class PlayerCombat_IA : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private beatClock clock;
    [SerializeField] private PlayerMove_IA playerMoveScript;
    [SerializeField] private Renderer bodyRenderer; // 状態を色で可視化するためのレンダラー

    [Header("攻撃")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackDamage = 1f;
    [SerializeField] private double attackBeats = 1.0; // 攻撃動作の長さ
    [SerializeField] private double justMs = 40; // JUST判定のズレ（ミリ秒）
    [SerializeField] private double goodMs = 90; // GOOD判定のズレ（ミリ秒）
    [SerializeField] private float falloffMs = 200f; // このms以上ズレたら最小倍率
    [SerializeField] private AnimationCurve damageCurve = AnimationCurve.Linear(0f, 2f, 1f, 0.5f); // X: 拍の頭からのズレ(0=ぴったり 1=falloffMs以上) Y: ダメージ倍率

    [Header("チャージ")]
    [SerializeField] private int maxCharge = 10; // 0以下なら上限なし
    [SerializeField] private int chargePerRhythmAttack = 1;
    [SerializeField] private int chargePerParry = 1;
    [SerializeField] private double chargeBeats = 0.5; // 連続拍でチャージしやすいよう半拍硬直
    [SerializeField] private float chargeSpeedMultiplier = 1f;
    [SerializeField] private bool resetChargeOnGuard = false;
    [SerializeField] private bool triggerSpinOnZeroChargeParryFail = false;

    [Header("回転切り（失敗時発動）")]
    [SerializeField] private float spinSlashRadius = 3.5f; // チャージ量によらず固定のエリア半径
    [SerializeField] private float spinBaseDamage = 10f; // チャージ0のときの基礎ダメージ
    [SerializeField] private float spinDamagePerCharge = 15f; // チャージ1あたりの追加ダメージ
    [SerializeField] private double spinSlashBeats = 1.0;
    [SerializeField] private float spinVisualDuration = 0.35f;
    [SerializeField] private float spinAreaFadeDuration = 0.5f;
    [SerializeField] private float groundY = 0f;
    [SerializeField] private AreaTelegraphVisual spinAreaVisual;

    [Header("致命攻撃")]
    [SerializeField] private float riposteRange = 3f;
    [SerializeField] private double riposteBeats = 1.0;
    [SerializeField] private float riposteDamage = 2f;

    [Header("パリィ")]
    [SerializeField] private double parryWindow = 0.12; // 入力後に判定が続く長さ（秒）
    [SerializeField] private double parryLate = 0.06; // 着弾より遅く入力した場合の猶予（秒）
    [SerializeField] private double parryRecovery = 0.4; // 判定後の硬直（秒）

    /// <summary>敵が「着弾の何秒後に判定を呼ぶか」を知るための猶予時間</summary>
    public double parryLateTolerance => parryLate;

    [Header("防御")]
    [SerializeField] private float guardSpeedMultiplier = 0.4f;

    [Header("体力")]
    [SerializeField] private float maxHp = 200f;
    [SerializeField] private float guardDamageMultiplier = 0.3f;
    public float hp { get; private set; }
    public int chargeCount { get; private set; }

    public playerAction action { get; private set; } = playerAction.Free;

    private double stateEndDsp; // 攻撃・硬直が終わる dspTime
    private double parryStartDsp = double.NegativeInfinity; // パリィ判定開始 dspTime
    private double parryEndDsp; // パリィ判定終了 dspTime

    private long lastChargedBeatIndex = long.MinValue; // 同じ拍での二重チャージ防止
    private bool hasExpectedChargeBeat; // チャージ継続のために次に入力を待つべき拍があるか
    private long nextExpectedChargeBeat; // 次に入力すべき拍番号
    private bool isChargingMotion;
    private bool isSpinSlashing;
    private float spinVisualElapsed = -1f;
    private Quaternion bodyInitialLocalRotation = Quaternion.identity;

    // ------ InputAction 経由の入力 ------
    private bool attackPressed => inputReader != null && inputReader.AttackPressedThisFrame;
    private bool parryPressed => inputReader != null && inputReader.ParryPressedThisFrame;
    private bool guardHeld => inputReader != null && inputReader.IsGuardHeld;

    private void Awake()
    {
        if (spinAreaVisual == null)
        {
            spinAreaVisual = gameObject.AddComponent<AreaTelegraphVisual>();
        }

        if (bodyRenderer != null)
        {
            bodyInitialLocalRotation = bodyRenderer.transform.localRotation;
        }
    }

    private void Start()
    {
        hp = maxHp;
        ApplyView();
    }

    private void Update()
    {
        UpdateSpinVisual();

        double now = AudioSettings.dspTime;
        if (action == playerAction.Down) return; // 倒れたら何もできない

        switch (action)
        {
            case playerAction.Free:
            case playerAction.Guard:
                if (parryPressed) StartParry(now);
                else if (attackPressed)
                {
                    enemyBase riposteTarget = FindTarget(riposteRange, true);
                    if (riposteTarget != null) StartRiposte(now, riposteTarget);
                    else HandleRhythmAttackInput(now);
                }
                else if (HasMissedExpectedBeat(now))
                {
                    StartSpinSlash(now, "ビート見逃し");
                }
                else SetAction(guardHeld ? playerAction.Guard : playerAction.Free);
                break;

            case playerAction.Attack:
            case playerAction.Riposte:
                if (now >= stateEndDsp)
                {
                    isChargingMotion = false;
                    isSpinSlashing = false;
                    SetAction(playerAction.Free);
                }
                break;

            case playerAction.Parry:
                if (now > parryEndDsp)
                {
                    if (chargeCount > 0 || triggerSpinOnZeroChargeParryFail)
                    {
                        StartSpinSlash(now, "パリィ失敗");
                    }
                    else
                    {
                        SetAction(playerAction.Recovery);
                    }
                }
                break;

            case playerAction.Recovery:
                if (now >= stateEndDsp) SetAction(playerAction.Free);
                break;
        }
    }

    /// <summary>
    /// 攻撃ボタン入力をリズム判定し、成功時はチャージのみ加算、失敗時は回転切りを発動する。
    /// </summary>
    private void HandleRhythmAttackInput(double now)
    {
        double beat = clock.currentBeat;
        long nearestBeat = (long)Math.Round(beat);
        double offsetMs = (beat - nearestBeat) * clock.secPerBeat * 1000.0;
        double abs = Math.Abs(offsetMs);

        bool isOnBeat = abs <= goodMs && nearestBeat != lastChargedBeatIndex;
        if (isOnBeat)
        {
            string rating = abs <= justMs ? "JUST" : "GOOD";
            lastChargedBeatIndex = nearestBeat;
            AddCharge(chargePerRhythmAttack);
            ScheduleNextChargeBeat(nearestBeat);

            stateEndDsp = now + chargeBeats * clock.secPerBeat;
            isChargingMotion = true;
            isSpinSlashing = false;
            SetAction(playerAction.Attack);

            Debug.Log($"[IA] Charge {rating}({offsetMs:+0;-0}ms) -> Charge: {chargeCount}");
        }
        else
        {
            if (chargeCount > 0)
            {
                StartSpinSlash(now, $"攻撃リズム外し({offsetMs:+0;-0}ms)");
            }
            else
            {
                Debug.Log($"[IA] Charge MISS({offsetMs:+0;-0}ms) チャージ0のため不発");
            }
        }
    }

    /// <summary>
    /// 失敗時に発動する回転切り。チャージ量に応じたダメージを固定エリア内の全生存敵（状態不問）へ与える。
    /// </summary>
    private void StartSpinSlash(double now, string reason)
    {
        int consumedCharge = chargeCount;
        chargeCount = 0;
        hasExpectedChargeBeat = false;

        float damage = spinBaseDamage + spinDamagePerCharge * consumedCharge;
        stateEndDsp = now + spinSlashBeats * clock.secPerBeat;
        isChargingMotion = false;
        isSpinSlashing = true;
        spinVisualElapsed = 0f;
        SetAction(playerAction.Attack);

        if (spinAreaVisual != null)
        {
            spinAreaVisual.ShowAndFadeOut(transform.position, spinSlashRadius, groundY, spinAreaFadeDuration);
        }

        // 撃破によって enemyBase.all から削除されても安全に走査するため逆順ループで判定
        int hitCount = 0;
        float sqrRadius = spinSlashRadius * spinSlashRadius;
        Vector3 center = transform.position;
        for (int i = enemyBase.all.Count - 1; i >= 0; i--)
        {
            enemyBase e = enemyBase.all[i];
            if (e == null || e.isDown) continue;

            float dx = e.transform.position.x - center.x;
            float dz = e.transform.position.z - center.z;
            if (dx * dx + dz * dz <= sqrRadius)
            {
                e.TakeDamage(damage);
                hitCount++;
            }
        }

        Debug.Log($"[IA] SPIN SLASH! ({reason}) Charge:{consumedCharge} -> Damage:{damage:0.0} (Radius:{spinSlashRadius:0.0} / Hit:{hitCount}体)");
    }

    private void StartParry(double now)
    {
        parryStartDsp = now;
        parryEndDsp = now + parryWindow;
        stateEndDsp = parryEndDsp + parryRecovery;
        isChargingMotion = false;
        isSpinSlashing = false;
        SetAction(playerAction.Parry);
    }

    // 範囲内で最も近い敵を探す（requireStaggered なら崩れている敵だけが対象）
    private enemyBase FindTarget(float range, bool requireStaggered)
    {
        enemyBase best = null;
        float bestDist = range;
        foreach (enemyBase e in enemyBase.all)
        {
            if (requireStaggered && !e.isStaggered) continue;
            float d = Vector3.Distance(transform.position, e.transform.position);
            if (d <= bestDist)
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
        isChargingMotion = false;
        isSpinSlashing = false;
        if (chargeCount > 0 && clock != null)
        {
            double endBeat = clock.currentBeat + riposteBeats;
            ScheduleNextChargeBeat((long)Math.Floor(endBeat));
        }
        SetAction(playerAction.Riposte);
        Debug.Log("[IA] RIPOSTE!");
        t.TakeRiposte(riposteDamage);
    }

    /// <summary>
    /// 敵が攻撃の着弾時に呼ぶメソッド。引数は着弾時の dspTime。
    /// </summary>
    public defenceResult ReceiveAttack(double impactDspTime, float damage)
    {
        if (action == playerAction.Down) return defenceResult.Hit; // 倒れた後は何もしない

        if (impactDspTime >= parryStartDsp - parryLate && impactDspTime <= parryStartDsp + parryWindow)
        {
            double ms = (impactDspTime - parryStartDsp) * 1000.0;
            parryStartDsp = double.NegativeInfinity;
            AddCharge(chargePerParry);
            if (clock != null)
            {
                long parryBeat = (long)Math.Round(clock.currentBeat);
                ScheduleNextChargeBeat(parryBeat);
            }
            Debug.Log($"[IA] Parry! (入力から着弾まで{ms:0}ms) -> Charge: {chargeCount}");
            SetAction(playerAction.Free);
            return defenceResult.Parried;
        }

        if (action == playerAction.Guard)
        {
            float taken = damage * guardDamageMultiplier;
            if (resetChargeOnGuard) ResetCharge("ガード");
            Debug.Log("[IA] GUARD");
            TakeDamage(taken);
            return defenceResult.Guarded;
        }

        ResetCharge("被弾");
        Debug.Log("[IA] HIT!!");
        TakeDamage(damage);
        return defenceResult.Hit;
    }

    private void ScheduleNextChargeBeat(long baseBeat)
    {
        nextExpectedChargeBeat = baseBeat + 1;
        hasExpectedChargeBeat = true;
    }

    private bool HasMissedExpectedBeat(double now)
    {
        if (!hasExpectedChargeBeat || chargeCount <= 0 || clock == null || !clock.isPlaying) return false;
        double deadlineDsp = clock.BeatToDspTime(nextExpectedChargeBeat) + goodMs / 1000.0;
        return now > deadlineDsp;
    }

    private void AddCharge(int amount)
    {
        if (amount <= 0) return;

        if (maxCharge > 0)
            chargeCount = Mathf.Min(chargeCount + amount, maxCharge);
        else
            chargeCount += amount;
    }

    private void ResetCharge(string reason)
    {
        hasExpectedChargeBeat = false;
        if (chargeCount <= 0) return;
        chargeCount = 0;
        Debug.Log($"[IA] Charge Reset ({reason})");
    }

    private void TakeDamage(float damage)
    {
        hp = Mathf.Max(hp - damage, 0f);
        if (hp <= 0f)
        {
            Debug.Log("[IA] PLAYER DOWN");
            chargeCount = 0;
            hasExpectedChargeBeat = false;
            isChargingMotion = false;
            isSpinSlashing = false;
            spinVisualElapsed = -1f;
            if (bodyRenderer != null)
            {
                bodyRenderer.transform.localRotation = bodyInitialLocalRotation;
            }
            SetAction(playerAction.Down);
        }
    }

    private void UpdateSpinVisual()
    {
        if (spinVisualElapsed < 0f || bodyRenderer == null) return;

        spinVisualElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, spinVisualDuration);
        float t = Mathf.Clamp01(spinVisualElapsed / duration);

        bodyRenderer.transform.localRotation = bodyInitialLocalRotation * Quaternion.Euler(0f, 360f * t, 0f);

        if (t >= 1f)
        {
            spinVisualElapsed = -1f;
            bodyRenderer.transform.localRotation = bodyInitialLocalRotation;
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
        if (playerMoveScript == null) return;

        switch (action)
        {
            case playerAction.Free:     playerMoveScript.speedMultiplier = 1f; SetColor(Color.white); break;
            case playerAction.Guard:    playerMoveScript.speedMultiplier = guardSpeedMultiplier; SetColor(Color.blue); break;
            case playerAction.Attack:
                if (isSpinSlashing)
                {
                    playerMoveScript.speedMultiplier = 0f;
                    SetColor(new Color(1f, 0.35f, 0f));
                }
                else if (isChargingMotion)
                {
                    playerMoveScript.speedMultiplier = chargeSpeedMultiplier;
                    SetColor(Color.yellow);
                }
                else
                {
                    playerMoveScript.speedMultiplier = 0f;
                    SetColor(Color.red);
                }
                break;
            case playerAction.Parry:    playerMoveScript.speedMultiplier = 0f; SetColor(Color.cyan); break;
            case playerAction.Recovery: playerMoveScript.speedMultiplier = 0f; SetColor(Color.gray); break;
            case playerAction.Riposte:  playerMoveScript.speedMultiplier = 0f; SetColor(Color.magenta); break;
            case playerAction.Down:     playerMoveScript.speedMultiplier = 0f; SetColor(Color.black); break;
        }
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.Label(new Rect(10, 10, 340, 40), $"[IA] HP: {hp:0} / {maxHp:0}");
        string maxLabel = maxCharge > 0 ? $" / {maxCharge}" : "";
        GUI.Label(new Rect(10, 44, 340, 40), $"[IA] Charge: {chargeCount}{maxLabel}");
    }

    private void SetColor(Color c)
    {
        if (bodyRenderer != null) bodyRenderer.material.color = c;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.9f);
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, groundY, transform.position.z), spinSlashRadius);
    }
}
