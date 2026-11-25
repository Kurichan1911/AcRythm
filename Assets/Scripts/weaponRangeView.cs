using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(LineRenderer))]
public class weaponRangeView : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private playerCombat combat;

    [Header("表示設定")]
    [SerializeField] private bool showRange = true;
    [SerializeField, Min(8)] private int segments = 48; //円の滑らかさ
    [SerializeField] private float lineWidth = 0.05f; //線の太さ
    [SerializeField] private float yOffset = -0.95f;　//足元の少し上に表示する
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.5f); //敵が範囲外にいるときの色
    [SerializeField] private Color inRangeColor = new Color(0.2f, 1f, 0.4f, 0.9f); //敵が射程内にいるときの色

    private LineRenderer line;
    private Material lineMaterial;
    private float lastRange = -1f;
    private float lastYOffset = float.NaN;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        Shader shader = Shader.Find("Sprites/Default");
        if(shader != null)
        {
            lineMaterial = new Material(shader);
            line.material = lineMaterial;
        }
    }

    private void OnDestroy()
    {
        Destroy(lineMaterial);
    }

    private void Update()
    {
        if(combat == null || !showRange)
        {
            line.enabled = false;
            return;
        }

        float range = combat.currentAttackRange;
        if(range <= 0f)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        int count = Mathf.Max(8, segments);
        if (!Mathf.Approximately(range, lastRange)
            || !Mathf.Approximately(yOffset, lastYOffset)
            || line.positionCount != count)
        {
            lastRange = range;
            lastYOffset = yOffset;
            RebuildCircle(range, count);
        }

        Color c = HasEnemyInRange(range) ? inRangeColor : normalColor;
        line.startColor = c;
        line.endColor = c;
    }

    private void RebuildCircle(float radius, int count)
    {
        line.positionCount = count;

        float step = Mathf.PI * 2f / count;
        for(int i = 0; i < count; i++)
        {
            float rad = step * i;
            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;
            line.SetPosition(i, new Vector3(x, yOffset, z));
        }
    }

    private bool HasEnemyInRange(float range)
    {
        foreach(enemyBase e in enemyBase.all)
        {
            if (Vector3.Distance(transform.position, e.transform.position) <= range)
                return true;
        }
        return false;
    }
}
