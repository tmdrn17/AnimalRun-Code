using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class ExplosionEffectPool
{
    private static readonly Dictionary<GameObject, ObjectPool<ExplosionEffect>> pools = new();
    private static Transform container;

    public static void Spawn(GameObject prefab, Vector3 position, float radius)
    {
        if (prefab == null)
            return;

        // 씬이 바뀌면 컨테이너와 그 밑의 이펙트가 함께 파괴되므로 풀도 새로 만든다.
        if (container == null)
        {
            pools.Clear();
            container = new GameObject("Explosion Effects").transform;
        }

        if (pools.TryGetValue(prefab, out ObjectPool<ExplosionEffect> pool) == false)
        {
            pool = CreatePool(prefab);
            pools.Add(prefab, pool);
        }

        pool.Get().Play(position, radius);
    }

    private static ObjectPool<ExplosionEffect> CreatePool(GameObject prefab)
    {
        ObjectPool<ExplosionEffect> pool = null;

        pool = new ObjectPool<ExplosionEffect>(
            createFunc: () =>
            {
                GameObject instance = Object.Instantiate(prefab, container);

                // 이펙트 프리팹에 ExplosionEffect가 없으면 자동으로 붙인다.
                if (instance.TryGetComponent(out ExplosionEffect effect) == false)
                    effect = instance.AddComponent<ExplosionEffect>();

                effect.SetPool(pool);
                return effect;
            },
            actionOnGet: effect => effect.gameObject.SetActive(true),
            actionOnRelease: effect => effect.gameObject.SetActive(false),
            actionOnDestroy: effect =>
            {
                if (effect != null)
                    Object.Destroy(effect.gameObject);
            },
            collectionCheck: true,
            defaultCapacity: 5,
            maxSize: 30
        );

        return pool;
    }
}
