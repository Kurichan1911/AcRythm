using UnityEngine;

public enum weaponInputType
{
    Tap,        //短剣などタップ系武器
    Hold,       //直剣、太刀などホールド系武器
    SubCharge   //大剣や大槌など複ボタン系武器
}

public enum counterTrigger
{
    None,       //カウンター判定なし
    OnPress,    //押す瞬間にカウンター判定
    OnRelease   //離す瞬間にカウンター判定
}

[CreateAssetMenu(menuName = "Rhythm/Weapon Data", fileName = "newWeaponData")]
public class weaponData : ScriptableObject
{
    [Header("基本")]
    public string weaponName = "直剣";
    public weaponInputType inputType = weaponInputType.Hold;
    public bool canParry = true; //パリィ可否(大剣と太刀は false )

    [Header("攻撃性能")]
    public float damageMultiplier = 1.0f; //通常攻撃力の倍率
    public float attackRange = 2.0f; //攻撃距離
    public beatTime attackDuration = new beatTime { beat = 1, subdivision = 1, subIndex = 0 }; //攻撃動作の長さ
    public float riposteMultiplier = 1.0f; //致命攻撃の倍率

    [Header("リズム／ホールド")]
    public beatTime rhythmStep = new beatTime { beat = 1, subdivision = 1, subIndex = 0 }; //判定の基準になる刻み(4分=拍, 8分=subdivision:2 subIndex=1)
    public beatTime holdLength = new beatTime { beat = 1, subdivision = 1, subIndex = 0 }; //ホールド系の長押しの長さ(直剣1拍)
    [Range(0f, 1f)] public float pressWeight = 0.5f;    //ホールド判定の押す側の重み(離す側はholdLengthから自動計算)
    public bool allowStepDoubling = true;               //高BPMで最短入力間隔を下回ったら2倍の長さで判定するかどうか
    public float minIntervalMs = 150f;                  //最短入力間隔の目安(ms)

    [Header("カウンター(調整前)")]
    public counterTrigger counterType = counterTrigger.None;
    public float counterEarlyMs = 40f;      //着弾よりも速く押した場合の猶予(ms)
    public float counterLateMs = 50f;       //着弾よりも遅く押した場合の猶予(ms)
    public float counterDamageMultiplier = 2.0f; //カウンター成功時の攻撃倍率
    [Range(0f, 1f)] public float counterTakenDamageMultiplier = 0.15f; //カウンター成功時の被ダメージ倍率

    public float releaseWeight => 1f - pressWeight;


    ///<summary>
    ///最短入力間隔ルールを適用した実行の値を返す。
    ///高BPMで、minIntervalMs未満になる場合は2倍ずつ伸ばす。
    /// </summary>
    public double GetEffectiveBeats(beatTime baseLength, double secPerBeat)
    {
        double beats = baseLength.ToBeats();
        if (!allowStepDoubling || beats <= 0.0 || minIntervalMs <= 0f || secPerBeat <= 0.0)
            return beats;

        while (beats * secPerBeat * 1000.0 < minIntervalMs)
            beats *= 2.0;

        return beats;
    }

    private void OnValidate()
    {
        if (!attackDuration.IsValid)
            Debug.LogWarning($"{name}: attackDuration の分割数は{beatTime.ticksPerBeat}を割り切れる数にしてください。", this);
        if (!rhythmStep.IsValid)
            Debug.LogWarning($"{name}: rhythmStep の分割数は{beatTime.ticksPerBeat}を割り切れる数にしてください。", this);
        if (inputType == weaponInputType.Hold && !holdLength.IsValid)
            Debug.LogWarning($"{name}: holdLength の分割数は{beatTime.ticksPerBeat}を割り切れる数にしてください。", this);
    }
}