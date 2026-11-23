using UnityEngine;
using UnityEngine.Pool;

public class ExplosionEffect : MonoBehaviour
{
    private const float DefaultLifeTime = 3f;

    private IObjectPool<ExplosionEffect> myPool;
    private ParticleSystem[] particles;
    private float lifeTime;

    private void Awake()
    {
        particles = GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particle in particles)
        {
            // 하위 파티클까지 폭발 반경에 맞춰 커지도록 계층 스케일을 따르게 한다.
            ParticleSystem.MainModule main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            lifeTime = Mathf.Max(lifeTime, main.duration + main.startLifetime.constantMax);
        }

        if (lifeTime <= 0f)
            lifeTime = DefaultLifeTime;
    }

    public void SetPool(IObjectPool<ExplosionEffect> pool)
    {
        myPool = pool;
    }

    // position에서 폭발 반경 크기로 이펙트를 처음부터 다시 재생한다.
    public void Play(Vector3 position, float radius)
    {
        transform.SetPositionAndRotation(position, Quaternion.identity);
        transform.localScale = Vector3.one * Mathf.Max(0.1f, radius);

        foreach (ParticleSystem particle in particles)
        {
            particle.Clear(false);
            particle.Play(false);
        }

        // 재생 중에 다시 꺼내 쓰는 일은 없지만, 혹시 모를 중복 반납을 막는다.
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), lifeTime);
    }

    private void ReturnToPool()
    {
        myPool.Release(this);
    }
}
