using System;
using UnityEngine;

/// <summary>
/// プレイヤーが射程内に入ったら次の拍を待って攻撃シーケンスを開始する敵攻撃スクリプト。
/// ・1拍目：床に攻撃予告エリアを表示
/// ・3、4、5拍目：敵本体を黄色く発光
/// ・7拍目：予告エリア内へ攻撃判定を実行（パリィ・ガード・ダウン仕様は既存と同一）
/// </summary>
[DefaultExecutionOrder(10)] // 色の上書き順をそろえるため、dummyCombat より後に動かす
public class AreaTelegraphEnemy_IA : MonoBehaviour
{
    public enum AreaAnchorMode
    {
        EnemyCenter,
        PlayerPositionAtTelegraph
    }

    private enum SequenceState
    {
        Idle,
        WaitingForBeat1,
        Active
    }

    [Header("参照")]
    [SerializeField] private beatClock clock;
    [SerializeField] private PlayerCombat_IA player;
    [SerializeField] private enemyBase enemy;
    [SerializeField] private AreaTelegraphVisual telegraphVisual;

    [Header("射程・エリア設定")]
    [SerializeField] private float triggerRange = 4.5f;
    [SerializeField] private float attackAreaRadius = 4.5f;
    [SerializeField] private AreaAnchorMode anchorMode = AreaAnchorMode.EnemyCenter;
    [SerializeField] private float groundY = 0f;

    [Header("攻撃・タイミング設定")]
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private double windupFlashBeats = 0.45;
    [SerializeField] private float attackFlashSeconds = 0.15f;
    [SerializeField] private double cooldownBeats = 1.0;
    [SerializeField] private bool debugLog = true;

    private SequenceState state = SequenceState.Idle;

    private double beat1Dsp;
    private readonly double[] windupDsps = new double[3];
    private readonly bool[] windupTriggered = new bool[3];
    private double windupOffDsp = double.PositiveInfinity;
    private double impactDsp;
    private double sequenceEndBeat;
    private double cooldownEndDsp = double.NegativeInfinity;

    private Vector3 areaCenter;
    private bool impactShown;
    private bool damageResolved;
    private bool playerInAreaAtImpact;
    private double attackFlashEndDsp = -1;

    private void Awake()
    {
        if (telegraphVisual == null)
        {
            telegraphVisual = gameObject.AddComponent<AreaTelegraphVisual>();
        }
    }

    private void OnDisable()
    {
        if (telegraphVisual != null)
        {
            telegraphVisual.Hide();
        }
    }

    private void Update()
    {
        if (clock == null || player == null || enemy == null) return;

        // 撃破されたら即座に予告エリアを消して終了
        if (enemy.isDown)
        {
            if (state != SequenceState.Idle || attackFlashEndDsp >= 0)
            {
                CancelSequence(restoreIdleColor: false);
            }
            return;
        }

        // 崩れ中は新規開始せず、進行中だった場合も中断する
        if (enemy.isStaggered)
        {
            if (state != SequenceState.Idle || attackFlashEndDsp >= 0)
            {
                CancelSequence(restoreIdleColor: false);
            }
            return;
        }

        if (!clock.isPlaying) return;

        double now = AudioSettings.dspTime;

        switch (state)
        {
            case SequenceState.Idle:
                UpdateIdle(now);
                break;

            case SequenceState.WaitingForBeat1:
                if (now >= beat1Dsp)
                {
                    BeginBeat1();
                }
                break;

            case SequenceState.Active:
                UpdateActive(now);
                break;
        }
    }

    private void UpdateIdle(double now)
    {
        if (now < cooldownEndDsp) return;
        if (!IsWithinHorizontalDistance(transform.position, player.transform.position, triggerRange)) return;

        // 次の拍を1拍目として予約する
        double currentBeat = clock.currentBeat;
        double beat1 = currentBeat < 0.0 ? 0.0 : Math.Floor(currentBeat) + 1.0;

        beat1Dsp = clock.BeatToDspTime(beat1);
        windupDsps[0] = clock.BeatToDspTime(beat1 + 2.0); // 3拍目
        windupDsps[1] = clock.BeatToDspTime(beat1 + 3.0); // 4拍目
        windupDsps[2] = clock.BeatToDspTime(beat1 + 4.0); // 5拍目
        impactDsp = clock.BeatToDspTime(beat1 + 6.0);     // 7拍目
        sequenceEndBeat = beat1 + 6.0 + cooldownBeats;

        for (int i = 0; i < windupTriggered.Length; i++)
        {
            windupTriggered[i] = false;
        }

        windupOffDsp = double.PositiveInfinity;
        impactShown = false;
        damageResolved = false;
        playerInAreaAtImpact = false;
        attackFlashEndDsp = -1;

        state = SequenceState.WaitingForBeat1;
    }

    private void BeginBeat1()
    {
        Vector3 anchorPos = anchorMode == AreaAnchorMode.PlayerPositionAtTelegraph
            ? player.transform.position
            : transform.position;

        areaCenter = new Vector3(anchorPos.x, groundY, anchorPos.z);
        telegraphVisual.Show(areaCenter, attackAreaRadius, groundY);
        state = SequenceState.Active;

        if (debugLog) Debug.Log("[AreaEnemy] 1拍目: 攻撃予告エリア表示");
    }

    private void UpdateActive(double now)
    {
        // 1拍目〜7拍目までのチャージ進捗を床の予告エリアに反映
        if (!impactShown)
        {
            double duration = impactDsp - beat1Dsp;
            float progress = duration > 0.0 ? Mathf.Clamp01((float)((now - beat1Dsp) / duration)) : 1f;
            telegraphVisual.SetProgress(progress);
        }

        // 3、4、5拍目に黄色く点滅させる
        for (int i = 0; i < windupDsps.Length; i++)
        {
            if (!windupTriggered[i] && now >= windupDsps[i])
            {
                windupTriggered[i] = true;
                enemy.ShowWindup();
                windupOffDsp = windupDsps[i] + windupFlashBeats * clock.secPerBeat;
                if (debugLog) Debug.Log($"[AreaEnemy] {i + 3}拍目: 黄色発光");
            }
        }

        if (!impactShown && now >= windupOffDsp)
        {
            windupOffDsp = double.PositiveInfinity;
            enemy.ShowIdle();
        }

        // 7拍目に攻撃を発動
        if (!impactShown && now >= impactDsp)
        {
            impactShown = true;
            windupOffDsp = double.PositiveInfinity;
            playerInAreaAtImpact = IsPointInAttackArea(player.transform.position);

            enemy.ShowAttack();
            telegraphVisual.ShowImpactFlash();
            attackFlashEndDsp = now + attackFlashSeconds;

            if (debugLog) Debug.Log("[AreaEnemy] 7拍目: エリア攻撃発動");
        }

        // 着弾＋パリィ遅押し猶予後に成否を判定
        if (impactShown && !damageResolved && now >= impactDsp + player.parryLateTolerance)
        {
            damageResolved = true;

            if (playerInAreaAtImpact)
            {
                defenceResult result = player.ReceiveAttack(impactDsp, attackDamage);
                if (result == defenceResult.Parried)
                {
                    enemy.StartStagger(impactDsp);
                    CancelSequence(restoreIdleColor: false);
                    return;
                }
            }
            else
            {
                if (debugLog) Debug.Log("[AreaEnemy] プレイヤーはエリア外のため回避");
            }
        }

        // 着弾フラッシュが終わったら見た目を戻す
        if (attackFlashEndDsp >= 0 && now >= attackFlashEndDsp)
        {
            attackFlashEndDsp = -1;
            telegraphVisual.Hide();
            if (!enemy.isStaggered && !enemy.isDown)
            {
                enemy.ShowIdle();
            }
        }

        // 攻撃判定とフラッシュが両方終わったら待機状態へ戻る
        if (damageResolved && attackFlashEndDsp < 0)
        {
            cooldownEndDsp = clock.BeatToDspTime(sequenceEndBeat);
            state = SequenceState.Idle;
        }
    }

    private void CancelSequence(bool restoreIdleColor)
    {
        state = SequenceState.Idle;
        attackFlashEndDsp = -1;
        windupOffDsp = double.PositiveInfinity;

        if (telegraphVisual != null)
        {
            telegraphVisual.Hide();
        }

        if (restoreIdleColor && enemy != null && !enemy.isDown && !enemy.isStaggered)
        {
            enemy.ShowIdle();
        }
    }

    private bool IsPointInAttackArea(Vector3 worldPos)
    {
        return IsWithinHorizontalDistance(areaCenter, worldPos, attackAreaRadius);
    }

    private static bool IsWithinHorizontalDistance(Vector3 a, Vector3 b, float range)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz <= range * range;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(new Vector3(transform.position.x, groundY, transform.position.z), triggerRange);

        Gizmos.color = Color.red;
        Vector3 previewCenter = state == SequenceState.Active
            ? areaCenter
            : new Vector3(transform.position.x, groundY, transform.position.z);
        Gizmos.DrawWireSphere(previewCenter, attackAreaRadius);
    }
}
