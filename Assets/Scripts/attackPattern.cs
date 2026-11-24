using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class windupCue
{
    public beatTime time; //予備動作を出す位置(パターン開始からカウントした位置)
    //以下に予備動作のアニメーションや音などを追加
}

[Serializable]
public class attackCue
{
    public beatTime impact; //着弾の位置　パリィ判定の基準
    //以下にパリィ不可攻撃やダメージ量追加
    public float damage = 10f;
}

[CreateAssetMenu(menuName = "Rhythm/Attack Pattern", fileName = "newAttackPattern")]
public class attackPattern : ScriptableObject
{
    public beatTime length; //パターン全体の長さ　(パターン開始から次のパターンが始まるまで)
    public List<windupCue> windups = new List<windupCue>();
    public List<attackCue> attacks = new List<attackCue>();

    public void OnValidate()
    {
        int len = length.ToTicks();
        if (!length.IsValid)
            Debug.LogWarning($"{name}: lengthの分割数は 480 を割り切れる数にしてください。", this);

        foreach (windupCue w in windups)
            CheckTime(w.time, len, "予備動作");
        foreach (attackCue a in attacks)
            CheckTime(a.impact, len, "攻撃");
    }

    private void CheckTime(beatTime t, int lengthTicks, string label)
    {
        if (!t.IsValid)
            Debug.LogWarning($"{name}: {label}の分割数は 480 を割り切れる数にしてください。", this);
        if (t.ToTicks() < 0 || t.ToTicks() > lengthTicks)
            Debug.LogWarning($"{name}: {label}の位置がパターンの範囲(0 ~ length)の外です。", this);
    }
}