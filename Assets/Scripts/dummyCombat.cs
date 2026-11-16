using JetBrains.Annotations;
using System.Data;
using UnityEngine;

public class dummyCombat : MonoBehaviour
{
    [SerializeField] private beatClock clock;
    [SerializeField] private playerCombat player;
    [SerializeField] private Renderer view;

    [Header("ステータス")]
    [SerializeField] private float maxHp = 3f;
    [SerializeField] private double staggerBeats = 2.0;

    [Header("色")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color windupColor = Color.yellow;
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private Color staggerColor = Color.green;
    [SerializeField] private Color hitColor = Color.magenta;
    [SerializeField] private Color downColor = Color.black;

    public bool isStaggered { get; private set; }

    private double impactDsp = -1;
    private bool attackShown;
    private double staggerEndDsp = double.NegativeInfinity;
    private float hp;

    private void Start()
    {
        hp = maxHp;
    }

    private void OnEnable()
    {
        clock.OnBeat += HandleBeat;
    }
    private void OnDisable()
    {
        clock.OnBeat -= HandleBeat;
    }

    private void HandleBeat(int beat)
    {
        double beatDsp = clock.BeatToDspTime(beat);
        if (beatDsp < staggerEndDsp - 0.001) return;

        if(beat % 4 == 2)
        {
            impactDsp = clock.BeatToDspTime(beat + 1);
            attackShown = false;
            view.material.color = windupColor;
        }
        else if(beat % 4 == 0)
        {
            view.material.color = idleColor;
        }
    }

    void Update()
    { 
        double now = AudioSettings.dspTime;
        if(isStaggered)
        {
            if (now < staggerEndDsp) return;
            isStaggered = false;
            if (impactDsp < 0) view.material.color = idleColor;
            //if(now >= staggerEndDsp)
            //{
            //    isStaggered = false;
            //    view.material.color = idleColor;
            //}
            //return;
        }
        if (impactDsp < 0) return;

        if (!attackShown && now >= impactDsp)
        {
            view.material.color = attackColor;
            attackShown = true;
        }

        if(now >= impactDsp + player.parryLateTolerance)
        {
            defenceResult result = player.ReceiveAttack(impactDsp);
            if (result == defenceResult.Parried) StartStagger(impactDsp);
            impactDsp = -1;
        }
    }
    
    private void StartStagger(double fromDsp)
    {
        isStaggered = true;
        staggerEndDsp = fromDsp + staggerBeats * clock.secPerBeat;
        view.material.color = staggerColor;
    }

    public void TakeDamage(float damage)
    {
        hp -= damage;
        Debug.Log($"Hit. Enemy HP {Mathf.Max(hp, 0)}/{maxHp}");
        CheckDown();
    }

    ///<summary>致命攻撃を受ける</summary>
    public void TakeRiposte(float damage)
    {
        isStaggered = false;
        staggerEndDsp = double.NegativeInfinity;
        hp -= damage;
        Debug.Log($"Riposte hit! Enemy HP {Mathf.Max(hp, 0)}/{maxHp}");

        if (CheckDown()) return;
        view.material.color = hitColor;
    }

    private bool CheckDown()
    {
        if (hp > 0) return false;
        view.material.color = downColor;
        Debug.Log("ENEMY DOWN");
        enabled = false;
        return true;
    }
}
