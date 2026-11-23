using System.Collections;
using UnityEngine;

/// <summary>
/// 빛기둥을 한 번 강하게 점멸시킨 다음, 위에서 아래로 쓸어내리며 사라지게 한다.
/// 이 스크립트는 빛기둥용 Cylinder의 Renderer가 있는 GameObject에 붙인다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class DescendingLightColumnController : MonoBehaviour
{
    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");

    [Header("재생")]
    [SerializeField, Min(0f)] private float flashDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float descendDuration = 0.9f;
    [SerializeField] private bool disableRendererWhenFinished = true;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine playCoroutine;

    public float DescendDuration => descendDuration;

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();

        // 대기 상태에서는 빛기둥이 보이지 않도록 끝난 상태로 초기화한다.
        SetEffect(1f, 0f);
        targetRenderer.enabled = false;
    }

    private void OnEnable()
    {
        Play();
    }

    /// <summary>빛기둥 효과를 처음부터 한 번 재생한다.</summary>
    public void Play()
    {
        if (!isActiveAndEnabled || targetRenderer == null)
        {
            return;
        }

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        targetRenderer.enabled = true;
        playCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        // 1단계: 기둥 전체가 한 번 밝게 빛난 뒤 빠르게 잦아든다.
        SetEffect(0f, 1f);

        if (flashDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                float t = Mathf.Clamp01(elapsed / flashDuration);
                float flash = 1f - Mathf.SmoothStep(0f, 1f, t);
                SetEffect(0f, flash);

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 2단계: 경계 발광이 위에서 아래로 이동하고, 지나간 부분은 사라진다.
        SetEffect(0f, 0f);

        float descendElapsed = 0f;
        while (descendElapsed < descendDuration)
        {
            float t = Mathf.Clamp01(descendElapsed / descendDuration);
            float progress = Mathf.SmoothStep(0f, 1f, t);
            SetEffect(progress, 0f);

            descendElapsed += Time.deltaTime;
            yield return null;
        }

        SetEffect(1f, 0f);
        playCoroutine = null;

        if (disableRendererWhenFinished && targetRenderer != null)
        {
            targetRenderer.enabled = false;
        }
    }

    private void SetEffect(float progress, float flash)
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ProgressId, Mathf.Clamp01(progress));
        propertyBlock.SetFloat(FlashId, Mathf.Clamp01(flash));
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDisable()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }
}
