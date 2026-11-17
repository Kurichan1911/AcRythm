using UnityEngine;

public class dummyCombat : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private Renderer view;

    [Header("ステータス")]
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private double staggerBeats = 2.0;

    [Header("色")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color windupColor = Color.yellow;
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private Color staggerColor = Color.green;
    [SerializeField] private Color hitColor = Color.magenta;
    [SerializeField] private Color downColor = Color.black;

    public double staggerEndDsp { get; private set; } = double.NegativeInfinity;
    public bool isStaggered => AudioSettings.dspTime < staggerEndDsp; //時刻で判定
    public bool isDown { get; private set; }

    private float hp;
    private bool staggerShown; //崩れの色を出している間だけtrue

    private void Start()
    {
        hp = maxHp;
    }

    private void Update()
    {
        if(staggerShown && !isStaggered)
        {
            staggerShown = false;
            ShowIdle();
        }
    }

    //見た目の窓口 後でアニメーションに置き換える
    public void ShowIdle()      { view.material.color = idleColor;  }
    public void ShowWindup()    { view.material.color = windupColor; }
    public void ShowAttack()    { view.material.color = attackColor; }

    public void StartStagger(double fromDsp)
    {
        staggerEndDsp = fromDsp + staggerBeats * clock.secPerBeat;
        staggerShown = true;
        view.material.color = staggerColor;
    }

    public void TakeDamage(float damage)
    {
        hp -= damage;
        Debug.Log($"Hit. Enemy HP {Mathf.Max(hp, 0)}/{maxHp}");
        CheckDown();
    }

    ///<summary>致命攻撃を受ける(当てたら崩れは終わり)</summary>>
    public void TakeRiposte(float damage)
    {
        staggerEndDsp = double.NegativeInfinity;
        staggerShown = false;
        hp -= damage;
        Debug.Log($"Riposte hit! Enemy HP {Mathf.Max(hp, 0)}/{maxHp}");

        if (CheckDown()) return;
        view.material.color = hitColor;
    }

    private bool CheckDown()
    {
        if (hp > 0) return false;
        staggerEndDsp = double.NegativeInfinity;
        view.material.color = downColor;
        Debug.Log("ENEMY DOWNED");
        isDown = true;
        enabled = false;
        return true;
    }
}