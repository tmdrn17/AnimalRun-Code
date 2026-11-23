using System.Collections;
using UnityEngine;

/// <summary>
/// 적이 총알에 맞았을 때의 피드백(색 번쩍임, 크기 펀치, 피격 이펙트)을 재생한다.
/// Enemy.OnDamage에서 Play()를 호출한다.
/// </summary>
public class EnemyHitFeedback : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // Synty 셰이더(Generic_Basic 등)의 발광 속성
    private static readonly int EnableEmissionId = Shader.PropertyToID("_Enable_Emission");
    private static readonly int EmissionColorId = Shader.PropertyToID("_Emission_Color");
    private static readonly int EmissionMapId = Shader.PropertyToID("_Emission_Map");

    [Header(" Flash ")]
    [SerializeField] private bool useFlash = true;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;

    [Header(" Punch Scale ")]
    [SerializeField] private bool usePunchScale = true;
    [SerializeField] private float punchAmount = 0.15f;
    [SerializeField, Min(0.01f)] private float punchDuration = 0.15f;

    [Header(" Hit Effect ")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private Vector3 hitEffectOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float hitEffectScale = 1f;

    private Renderer[] renderers;
    private Color[] originColors;
    private int[] colorIds;
    private bool[] useEmission;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 originScale;

    private Coroutine flashRoutine;
    private Coroutine punchRoutine;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        originScale = transform.localScale;

        renderers = GetComponentsInChildren<Renderer>();
        originColors = new Color[renderers.Length];
        colorIds = new int[renderers.Length];
        useEmission = new bool[renderers.Length];

        // 렌더러마다 원래 색을 기억해 둔다. URP Lit은 _BaseColor, 구형 셰이더는 _Color를 쓴다.
        for (int i = 0; i < renderers.Length; i++)
        {
            Material material = renderers[i].sharedMaterial;
            colorIds[i] = -1;
            originColors[i] = Color.white;

            if (material == null)
                continue;

            // Synty 셰이더는 _BaseColor 가 텍스처에 곱해지는데 보통 흰색이라, 색을 바꿔도 번쩍임이 보이지 않는다.
            // 그래서 발광(Emission)을 켜서 번쩍이게 한다.
            if (material.HasProperty(EnableEmissionId) && material.HasProperty(EmissionColorId) && material.HasProperty(EmissionMapId))
            {
                useEmission[i] = true;
                continue;
            }

            if (material.HasProperty(BaseColorId))
                colorIds[i] = BaseColorId;
            else if (material.HasProperty(ColorId))
                colorIds[i] = ColorId;

            if (colorIds[i] != -1)
                originColors[i] = material.GetColor(colorIds[i]);
        }
    }

    private void OnDisable()
    {
        flashRoutine = null;
        punchRoutine = null;
        ResetFeedback();
    }

    public void Play()
    {
        if (useFlash)
            Restart(ref flashRoutine, FlashRoutine());

        if (usePunchScale)
            Restart(ref punchRoutine, PunchRoutine());

        if (hitEffectPrefab != null)
            ExplosionEffectPool.Spawn(hitEffectPrefab, transform.position + hitEffectOffset, hitEffectScale);
    }

    private void Restart(ref Coroutine routine, IEnumerator next)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(next);
    }

    private IEnumerator FlashRoutine()
    {
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            SetFlash(1f - elapsed / flashDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearFlash();
        flashRoutine = null;
    }

    private IEnumerator PunchRoutine()
    {
        float elapsed = 0f;

        while (elapsed < punchDuration)
        {
            // 0 → 1 → 0으로 한 번 부풀었다 돌아온다.
            float wave = Mathf.Sin(elapsed / punchDuration * Mathf.PI);
            transform.localScale = originScale * (1f + punchAmount * wave);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = originScale;
        punchRoutine = null;
    }

    // amount 1이면 flashColor, 0이면 원래 색.
    private void SetFlash(float amount)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            if (useEmission[i])
            {
                // 원래 발광 텍스처는 빛나는 부분만 밝아서, 번쩍이는 동안은 흰 텍스처로 바꿔 몸 전체가 빛나게 한다.
                renderers[i].GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(EnableEmissionId, 1f);
                propertyBlock.SetTexture(EmissionMapId, Texture2D.whiteTexture);
                propertyBlock.SetColor(EmissionColorId, flashColor * amount);
                renderers[i].SetPropertyBlock(propertyBlock);
                continue;
            }

            if (colorIds[i] == -1)
                continue;

            renderers[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(colorIds[i], Color.Lerp(originColors[i], flashColor, amount));
            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    // 프로퍼티 블록을 비워 원래 머티리얼(SRP Batcher 포함) 상태로 되돌린다.
    private void ClearFlash()
    {
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer != null)
                targetRenderer.SetPropertyBlock(null);
        }
    }

    private void ResetFeedback()
    {
        if (renderers != null)
            ClearFlash();

        transform.localScale = originScale;
    }
}
