using System;
using UnityEngine;

/// <summary>
/// 拍の位置または長さ。「整数」+「拍の分割」で表現。
/// beat, subdivision, subIndexで表す。
/// 例) 3拍目の頭: beat = 3, subdivision = 1, subIndex = 0
///     2拍目の3連符の2つ目: beat = 2, subdivision = 3, subIndex = 1
/// 内部では整数をtickで扱うことにより、3連符などの複雑なリズムでも誤差なし
/// </summary>

[Serializable]
public struct beatTime
{
    public const int ticksPerBeat = 480;

    public int beat;                    //何拍目か
    [Min(1)] public int subdivision;    //1拍を何分割するか(1=そのまま 2=裏拍 3=3連符 ...)
    public int subIndex;                //分割した何番目か(0スタート)

    public int ToTicks()
    {
        int div = Mathf.Max(1, subdivision);
        return beat * ticksPerBeat + subIndex * (ticksPerBeat / div);
    }

    public double ToBeats()
    {
        return ToTicks() / (double)ticksPerBeat;
    }

    public bool IsValid => ticksPerBeat % Mathf.Max(1, subdivision) == 0;
}
