using System;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

[DefaultExecutionOrder(10)] //色の上書き順をそろえるため、dummyCombatより後に動かす
public class enemyAttacker : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private playerCombat player;
    [SerializeField] private dummyCombat enemy;

    [Header("パターン")]
    [SerializeField] private List<attackPattern> patterns = new List<attackPattern>(); //順番に繰り返す
    [SerializeField] private int gridBeats = 4; //開始・復帰をそろえる秒数
    [SerializeField] private float attackFlashSeconds = 0.1f;

    //予備動作1つ当たりの実行状態
    private class windupRuntime
    {
        public double dsp;
        public bool shown;
    }

    //攻撃一つ当たりの実行状態
    private class attackRuntime
    {
        public double impactDsp;
        public bool impactShown;
        public bool done;
    }

    private readonly List<windupRuntime> activeWindups = new List<windupRuntime>();
    private readonly List<attackRuntime> activeAttacks = new List<attackRuntime>();
    private int patternIndex;
    private bool patternRunning;
    private double patternEndBeat;
    private double nextStartBeat = double.NaN; //NaN = まだ決めてない
    private bool wasStaggered;
    private double flashEndDsp = -1;
    private double lastWindupShownDsp = -1; //最後に予備動作を出した時刻
    private double lastImpactShownDsp = -1; //最後に攻撃を出した時刻

    private void Update()
    {
        if (!clock.isPlaying || enemy.isDown || patterns.Count == 0) return;
        double now = AudioSettings.dspTime;

        if (double.IsNaN(nextStartBeat))
            nextStartBeat = NextGrid(Math.Max(clock.currentBeat, 0));

        //崩れが終わったら次の小節から再開
        bool staggered = enemy.isStaggered;
        if(wasStaggered && !staggered)
        {
            //時間切れなら崩れの終了時刻から 致命攻撃で終わったなら今から数える
            double endBeat = double.IsNegativeInfinity(enemy.staggerEndDsp)
                ? clock.currentBeat
                : BeatOf(enemy.staggerEndDsp);
            nextStartBeat = NextGrid(endBeat);
        }
        wasStaggered = staggered;
        if (staggered) return;

        //パターンの開始
        if (!patternRunning && now >= clock.BeatToDspTime(nextStartBeat))
            StartPattern(nextStartBeat);

        if (!patternRunning) return;

        ProcessEvents(now);

        //パターンの終了
        if (patternRunning && now >= clock.BeatToDspTime(patternEndBeat) && AllDone())
        {
            patternRunning = false;
            nextStartBeat = patternEndBeat;
            patternIndex = (patternIndex + 1) % patterns.Count;
        }
    }

    private void StartPattern(double startBeat)
    {
        attackPattern p = patterns[patternIndex];
        if(p == null)
        {
            patternIndex = (patternIndex + 1) % patterns.Count; //空のスロットは飛ばす
            return;
        }

        activeWindups.Clear();
        foreach(windupCue w in p.windups)
        {
            activeWindups.Add(new windupRuntime { dsp = clock.BeatToDspTime(startBeat + w.time.ToBeats()) });
        }

        activeAttacks.Clear();
        foreach(attackCue a in p.attacks)
        {
            activeAttacks.Add(new attackRuntime { impactDsp = clock.BeatToDspTime(startBeat + a.impact.ToBeats()) });
        }

        patternEndBeat = startBeat + p.length.ToBeats();
        patternRunning = true;
    }

    private void ProcessEvents(double now)
    {
        //予備動作
        foreach(windupRuntime w in activeWindups)
        {
            if (w.shown || now < w.dsp) continue;
            w.shown = true;
            lastWindupShownDsp = now;
            enemy.ShowWindup();
        }

        //攻撃
        foreach(attackRuntime a in activeAttacks)
        {
            if (a.done) continue;

            if(!a.impactShown && now >= a.impactDsp)
            {
                a.impactShown = true;
                lastImpactShownDsp = now;
                flashEndDsp = now + attackFlashSeconds;
                enemy.ShowAttack();
            }

            //着弾+遅押しの猶予が過ぎたら判定
            if (now >= a.impactDsp + player.parryLateTolerance)
            {
                a.done = true;
                defenceResult result = player.ReceiveAttack(a.impactDsp);
                if(result == defenceResult.Parried)
                {
                    enemy.StartStagger(a.impactDsp);
                    InterruptPattern();
                    wasStaggered = true;
                    return;
                }
            }
        }

        //着弾の色を戻す
        if(flashEndDsp >= 0 && now >= flashEndDsp)
        {
            flashEndDsp = -1;
            if (lastWindupShownDsp <= lastImpactShownDsp) enemy.ShowIdle();
        }
    }

    //パリィされたら残りの予備動作と攻撃をすべてキャンセルして次のパターンへ
    private void InterruptPattern()
    {
        foreach (attackRuntime a in activeAttacks) a.done = true;
        foreach (windupRuntime w in activeWindups) w.shown = true;
        patternRunning = false;
        patternIndex = (patternIndex + 1) % patterns.Count;
        flashEndDsp = -1;
    }

    private bool AllDone()
    {
        foreach (attackRuntime a in activeAttacks)
            if (!a.done) return false;
        foreach (windupRuntime w in activeWindups)
            if (!w.shown) return false;
        return true;
    }

    private double BeatOf(double dsp)
    {
        return (dsp - clock.BeatToDspTime(0)) / clock.secPerBeat;
    }

    //指定の拍以降で、最初の gridBeats の倍数の拍
    private double NextGrid(double beat)
    {
        int grid = Mathf.Max(1, gridBeats);
        return Math.Ceiling(beat / grid - 1e-6) * grid; //ちょうど倍数なら、その拍を返す
    }
}