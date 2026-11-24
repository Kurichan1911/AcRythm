using System.Collections.Generic;
using UnityEngine;

///<summary>
///すべての敵に共通する部分。(HP, 崩れ, 致命攻撃, 撃破)
///見た目(色やアニメーション)は、派生クラスが実装するのでここでは触れない
/// </summary>
public abstract class enemyBase : MonoBehaviour
{
    //シーン内のうごいている敵の一覧
    public static readonly List<enemyBase> all = new List<enemyBase>();

    [SerializeField] private beatClock clock;

    [Header("ステータス")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private double staggerBeats = 2.0;

    public double staggerEndDsp { get; private set; } = double.NegativeInfinity;
    public bool isStaggered => AudioSettings.dspTime < staggerEndDsp;
    public bool isDown { get; private set; }

    private float hp;
    private bool staggerShown; //崩れの見た目の間 true

    public abstract void ShowIdle();
    public abstract void ShowWindup();
    public abstract void ShowAttack();
    protected abstract void ShowStagger();
    protected abstract void ShowHit();
    protected abstract void ShowDown();

    protected virtual void Start()
    {
        hp = maxHp;
    }

    protected virtual void OnEnable()
    {
        all.Add(this);
    }

    protected virtual void OnDisable()
    {
        all.Remove(this); //撃破されると一覧から外れる
    }

    protected virtual void Update()
    {
        //崩れが時間切れで終わったら見た目を戻す
        if(staggerShown && !isStaggered)
        {
            staggerShown = false;
            ShowIdle();
        }
    }

    public void StartStagger(double fromDsp)
    {
        staggerEndDsp = fromDsp + staggerBeats * clock.secPerBeat;
        staggerShown = true;
        ShowStagger();
    }

    public void TakeDamage(float damage)
    {
        hp -= damage;
        Debug.Log($"HIT. Enemy HP: {Mathf.Max(hp, 0)} / {maxHp}");
        CheckDown();
    }

    ///<summary>致命攻撃を受ける</summary>
    public void TakeRiposte(float damage)
    {
        staggerEndDsp = double.NegativeInfinity;
        staggerShown = false;
        hp -= damage;
        Debug.Log($"RIPOSTE HIT! Enemy HP{Mathf.Max(hp, 0)} / {maxHp}");

        if (CheckDown()) return;
        ShowHit();
    }

    private bool CheckDown()
    {
        if (hp > 0) return false;
        staggerEndDsp = double.NegativeInfinity;
        ShowDown();
        Debug.Log("Enemy Downed");
        isDown = true;
        enabled = false;
        return true;
    }
}
