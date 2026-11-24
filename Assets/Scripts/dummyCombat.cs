using UnityEngine;

public class dummyCombat : enemyBase
{
    [SerializeField] private Renderer view;

    [Header("色")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color windupColor = Color.yellow;
    [SerializeField] private Color attackColor = Color.red;
    [SerializeField] private Color staggerColor = Color.green;
    [SerializeField] private Color hitColor = Color.magenta;
    [SerializeField] private Color downColor = Color.black;

    public override void ShowIdle() { view.material.color = idleColor; }
    public override void ShowWindup() { view.material.color = windupColor; }
    public override void ShowAttack() { view.material.color = attackColor; }
    protected override void ShowStagger() { view.material.color = staggerColor; }
    protected override void ShowHit() { view.material.color = hitColor; }
    protected override void ShowDown() { view.material.color = downColor; }
}