using UnityEngine;

/// <summary>
/// 床に攻撃予告エリア（半透明の円盤＋内側から外枠へ広がるチャージ円＋外枠リング）を描画するコンポーネント。
/// プロシージャルメッシュで描画するため専用プレハブやマテリアルアセットが不要。
/// </summary>
public class AreaTelegraphVisual : MonoBehaviour
{
    [Header("色設定")]
    [SerializeField] private Color baseAreaColor = new Color(1f, 0.12f, 0.1f, 0.25f);
    [SerializeField] private Color chargeAreaColor = new Color(1f, 0.15f, 0.05f, 0.45f);
    [SerializeField] private Color borderColor = new Color(1f, 0.2f, 0.1f, 0.85f);
    [SerializeField] private Color impactFlashColor = new Color(1f, 0.5f, 0.2f, 0.85f);

    private const int Segments = 48;
    private const float RingInnerRadiusRatio = 0.93f;

    private GameObject rootObject;
    private Transform baseTransform;
    private Transform chargeTransform;
    private Transform borderTransform;

    private Material baseMaterial;
    private Material chargeMaterial;
    private Material borderMaterial;

    private Mesh discMesh;
    private Mesh ringMesh;

    private float currentRadius = 1f;
    private bool isFadingOut;
    private float fadeStartTime;
    private float fadeDurationSec = 0.5f;

    /// <summary>
    /// 指定位置・半径・床高さで予告エリアを表示開始する。
    /// </summary>
    public void Show(Vector3 centerWorldPos, float radius, float groundY)
    {
        EnsureVisuals();

        isFadingOut = false;
        chargeTransform.gameObject.SetActive(true);

        currentRadius = Mathf.Max(0.01f, radius);
        rootObject.transform.position = new Vector3(centerWorldPos.x, groundY, centerWorldPos.z);
        rootObject.transform.rotation = Quaternion.identity;

        Vector3 fullScale = new Vector3(currentRadius, 1f, currentRadius);
        baseTransform.localScale = fullScale;
        borderTransform.localScale = fullScale;

        ApplyNormalColors();
        SetProgress(0f);

        rootObject.SetActive(true);
    }

    /// <summary>
    /// 敵と同じ円（ベース円＋外枠リング）を即時表示し、指定秒数かけて徐々にフェードアウトする。
    /// </summary>
    public void ShowAndFadeOut(Vector3 centerWorldPos, float radius, float groundY, float fadeDuration = 0.5f)
    {
        EnsureVisuals();

        currentRadius = Mathf.Max(0.01f, radius);
        rootObject.transform.position = new Vector3(centerWorldPos.x, groundY, centerWorldPos.z);
        rootObject.transform.rotation = Quaternion.identity;

        Vector3 fullScale = new Vector3(currentRadius, 1f, currentRadius);
        baseTransform.localScale = fullScale;
        borderTransform.localScale = fullScale;
        chargeTransform.gameObject.SetActive(false);

        ApplyNormalColors();

        fadeDurationSec = Mathf.Max(0.01f, fadeDuration);
        fadeStartTime = Time.time;
        isFadingOut = true;

        rootObject.SetActive(true);
    }

    /// <summary>
    /// チャージ進捗（0〜1）に応じて内側のチャージ円の大きさを更新する。
    /// </summary>
    public void SetProgress(float normalized01)
    {
        if (chargeTransform == null) return;

        float t = Mathf.Clamp01(normalized01);
        float r = currentRadius * t;
        chargeTransform.localScale = new Vector3(r, 1f, r);
    }

    /// <summary>
    /// 7拍目の着弾時にベース円と外枠を明るいフラッシュ色へ切り替える。
    /// </summary>
    public void ShowImpactFlash()
    {
        if (rootObject == null) return;

        SetProgress(1f);
        if (baseMaterial != null) baseMaterial.color = impactFlashColor;
        if (borderMaterial != null) borderMaterial.color = impactFlashColor;
    }

    /// <summary>
    /// 予告エリアを非表示にする。
    /// </summary>
    public void Hide()
    {
        isFadingOut = false;
        if (rootObject != null)
        {
            rootObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (!isFadingOut || rootObject == null || !rootObject.activeSelf) return;

        float t = Mathf.Clamp01((Time.time - fadeStartTime) / fadeDurationSec);
        float alphaMul = 1f - t;

        if (baseMaterial != null)
            baseMaterial.color = new Color(baseAreaColor.r, baseAreaColor.g, baseAreaColor.b, baseAreaColor.a * alphaMul);
        if (borderMaterial != null)
            borderMaterial.color = new Color(borderColor.r, borderColor.g, borderColor.b, borderColor.a * alphaMul);

        if (t >= 1f)
        {
            isFadingOut = false;
            Hide();
        }
    }

    private void EnsureVisuals()
    {
        if (rootObject != null) return;

        // 親オブジェクト（敵ブロック）のスケールや回転に影響されないようワールド空間のルートとして作成
        rootObject = new GameObject($"{gameObject.name}_AreaTelegraph");
        rootObject.SetActive(false);

        discMesh = CreateDiscMesh(Segments);
        ringMesh = CreateRingMesh(Segments, RingInnerRadiusRatio, 1f);

        baseMaterial = CreateTransparentMaterial(baseAreaColor);
        chargeMaterial = CreateTransparentMaterial(chargeAreaColor);
        borderMaterial = CreateTransparentMaterial(borderColor);

        baseTransform = CreateLayerObject("BaseFill", 0.02f, discMesh, baseMaterial).transform;
        chargeTransform = CreateLayerObject("ChargeFill", 0.03f, discMesh, chargeMaterial).transform;
        borderTransform = CreateLayerObject("BorderRing", 0.04f, ringMesh, borderMaterial).transform;
    }

    private GameObject CreateLayerObject(string layerName, float yOffset, Mesh mesh, Material material)
    {
        GameObject obj = new GameObject(layerName);
        obj.transform.SetParent(rootObject.transform, false);
        obj.transform.localPosition = new Vector3(0f, yOffset, 0f);
        obj.transform.localRotation = Quaternion.identity;

        MeshFilter filter = obj.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return obj;
    }

    private void ApplyNormalColors()
    {
        if (baseMaterial != null) baseMaterial.color = baseAreaColor;
        if (chargeMaterial != null) chargeMaterial.color = chargeAreaColor;
        if (borderMaterial != null) borderMaterial.color = borderColor;
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    private static Mesh CreateDiscMesh(int segments)
    {
        Mesh mesh = new Mesh { name = "AreaTelegraphDisc" };

        Vector3[] vertices = new Vector3[segments + 1];
        Vector3[] normals = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        normals[0] = Vector3.up;

        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            normals[i + 1] = Vector3.up;

            int curr = i + 1;
            int next = (i + 1) % segments + 1;
            int triIndex = i * 3;
            triangles[triIndex] = 0;
            triangles[triIndex + 1] = next;
            triangles[triIndex + 2] = curr;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateRingMesh(int segments, float innerRadius, float outerRadius)
    {
        Mesh mesh = new Mesh { name = "AreaTelegraphRing" };

        Vector3[] vertices = new Vector3[segments * 2];
        Vector3[] normals = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];

        float step = Mathf.PI * 2f / segments;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * step;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            int innerIdx = i * 2;
            int outerIdx = i * 2 + 1;

            vertices[innerIdx] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
            vertices[outerIdx] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
            normals[innerIdx] = Vector3.up;
            normals[outerIdx] = Vector3.up;

            int nextInner = ((i + 1) % segments) * 2;
            int nextOuter = nextInner + 1;
            int tri = i * 6;

            // 左手系で上(+Y)から見て表向きになるよう巻き順を指定
            triangles[tri] = innerIdx;
            triangles[tri + 1] = nextOuter;
            triangles[tri + 2] = outerIdx;

            triangles[tri + 3] = innerIdx;
            triangles[tri + 4] = nextInner;
            triangles[tri + 5] = nextOuter;
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void OnDestroy()
    {
        if (rootObject != null) Destroy(rootObject);
        if (discMesh != null) Destroy(discMesh);
        if (ringMesh != null) Destroy(ringMesh);
        if (baseMaterial != null) Destroy(baseMaterial);
        if (chargeMaterial != null) Destroy(chargeMaterial);
        if (borderMaterial != null) Destroy(borderMaterial);
    }
}
