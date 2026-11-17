using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class attackStep
{
    public beatTime impact; //着弾の位置(パターン開始からカウント)
    public beatTime windup; //予備動作の長さ 着弾の前にこの長さ分の予備動作をする(0だと予備動作なし→追撃とかに使う)
}

[CreateAssetMenu(menuName = "Rhythm/Attack Pattern", fileName = "newAttackPattern")]
public class attackPattern : ScriptableObject
{
    public beatTime length; //次のパターンが始まるまでのパターン全体の長さ
    public List<attackStep> steps = new List<attackStep>();

    private void OnValidate()
    {
        if (!length.IsValid)
            Debug.LogWarning($"{name}: length の分割数は 480 を割り切れる数にしてください。", this);

        foreach(attackStep s in steps)
        {
            if (!s.impact.IsValid || !s.windup.IsValid)
                Debug.LogWarning($"{name}: 分割数は 480 を割り切れる数にしてください。", this);
            if (s.windup.ToTicks() > s.impact.ToTicks())
                Debug.LogWarning($"{name}: 予備動作がパターン開始より前から始まります。予備動作の長さを短くしてください。", this);
            if (s.impact.ToTicks() > length.ToTicks())
                Debug.LogWarning($"{name}: 着弾の length より後になっています。", this);
        }
    }
}
