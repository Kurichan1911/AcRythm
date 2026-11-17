using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10)] //dummyConbatよりも後に動かす
public class enemyAttacker : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private playerCombat player;
    [SerializeField] private dummyCombat enemy;

    [Header("パターン")]
    [SerializeField] private List<attackPattern> patterns = new List<attackPattern>(); //順番に繰り返す
    [SerializeField] private int gridBeats = 4; //開始・復帰を数える秒数
    [SerializeField] private float attackFlashSeconds = 0.1f; //着弾の色を見せる長さ

    //パターン内の1回の攻撃の実行状態
    private class stepRuntime
    {
        public double windupDsp;
        public double impactDsp;
        public bool windupShown;
        public bool impactShown;
        public bool done;
    }

    private readonly List<stepRuntime> active = new List<stepRuntime>();
    private int patternIndex;
    private bool patternRunning;
    private double patternEndBeat;
    private double nextStartBeat = double.NaN; //NaN=まだ決めてない
    private bool wasStaggered;
    private double flashEndDsp = -1;

    private void Update()
    {
        if (!clock.isPlaying || enemy.isDown || patterns.Count == 0) return;
        double now = AudioSettings.dspTime;

        if (double.IsNaN(nextStartBeat))
            nextStartBeat = NextGrid(Math.Max(clock.currentBeat, 0));

        //崩れが終わったら、次の小節の頭から再開
        bool staggered = enemy.isStaggered;
        if(wasStaggered && !staggered)
        {
            //崩れの時間切れなら崩れの終了時刻から 致命攻撃で終わったなら現在時刻から数える
            double endBeat = double.IsNegativeInfinity(enemy.staggerEndDsp)
                ? clock.currentBeat
                : BeatOf(enemy.staggerEndDsp);
            nextStartBeat = NextGrid(endBeat);
        }
        wasStaggered = staggered;
        if (staggered) return; //崩れている間は予備動作や攻撃はしない

        //パターンの開始
        if (!patternRunning && now >= clock.BeatToDspTime(nextStartBeat))
            StartPattern(nextStartBeat);

        if (!patternRunning) return;

        ProcessSteps(now);

        //パターンの終了
        if(patternRunning && now >= clock.BeatToDspTime(patternEndBeat) && AllDone())
        {
            patternRunning = false;
            nextStartBeat = patternEndBeat;
            patternIndex = (patternIndex + 1) % patterns.Count;
        }
    }

    private void StartPattern(double startBeat)
    {
        attackPattern p = patterns[patternIndex];
        if (p == null)
        {
            patternIndex = (patternIndex + 1) % patterns.Count; //空のスロットは飛ばす
            return;
        }

        active.Clear();
        foreach(attackStep s in p.steps)
        {
            double impactBeat = startBeat + s.impact.ToBeats();
            double windupBeat = Math.Max(impactBeat - s.windup.ToBeats(), startBeat);
            active.Add(new stepRuntime
            {
                windupDsp = clock.BeatToDspTime(windupBeat),
                impactDsp = clock.BeatToDspTime(impactBeat)
            });
        }

        patternEndBeat = startBeat + p.length.ToBeats();
        patternRunning = true;
    }

    private void ProcessSteps(double now)
    {
        foreach(stepRuntime s in active)
        {
            if (s.done) continue;

            if(!s.windupShown && now >= s.windupDsp)
            {
                enemy.ShowWindup();
                s.windupShown = true;
            }

            if(!s.impactShown && now >= s.impactDsp)
            {
                enemy.ShowAttack();
                s.impactShown = true;
                flashEndDsp = now + attackFlashSeconds;
            }

            //着弾・遅押しの猶予が来たら判定を呼ぶ
            if(now >= s.impactDsp + player.parryLateTolerance)
            {
                s.done = true;
                defenceResult result = player.ReceiveAttack(s.impactDsp);
                if(result == defenceResult.Parried)
                {
                    enemy.StartStagger(s.impactDsp);
                    InterruptPattern();
                    wasStaggered = true;
                    return;
                }
            }
        }

        //着弾の色を戻す(予備動作中なら戻さない)
        if(flashEndDsp >= 0 && now >= flashEndDsp)
        {
            flashEndDsp = -1;
            if (!AnyWindupActive(now)) enemy.ShowIdle();
        }
    }

    //パリィされたら、残りの攻撃をすべてキャンセルして次のパターンへ
    private void InterruptPattern()
    {
        foreach (stepRuntime s in active) s.done = true;
        patternRunning = false;
        patternIndex = (patternIndex + 1) % patterns.Count;
        flashEndDsp = -1;
    }

    private bool AllDone()
    {
        foreach (stepRuntime s in active)
            if (!s.done) return false;
        return true;
    }

    private bool AnyWindupActive(double now)
    {
        foreach (stepRuntime s in active)
            if (!s.done && s.windupShown && now < s.impactDsp) return true;
        return false;
    }

    private double BeatOf(double dsp)
    {
        return (dsp - clock.BeatToDspTime(0)) / clock.secPerBeat;
    }

    private double NextGrid(double beat)
    {
        int grid = Mathf.Max(1, gridBeats);
        return Math.Ceiling(beat / grid - 1e-6) * grid; //ちょうど倍数ならその数を返す
    }
}
