using Animal_Run.Scripts.Data;
using UnityEngine;
using UnityEngine.Pool;

public class Bullet : MonoBehaviour
{
    [Header(" Settings ")]
    [SerializeField] private float speed;
    [SerializeField] private float hitRadius = 0.6f;
    [SerializeField] private float arcHeightPerRange = 0.35f;
    [SerializeField] private float spinSpeed = 720f;
    
    [SerializeField] private GameObject explosionPrefab;

    private IObjectPool<Bullet> myPool;

    private float range;
    private int damage;
    private float splashRadius;
    private bool isArc;
    private bool isSpinning;
    private bool isExplosive;

    private Vector3 startPosition;
    private Vector3 lastPosition;
    private Vector3 direction;
    private float groundY;
    private float traveledDistance;

    private void Update()
    {
        Move();

        if (isSpinning)
            transform.Rotate(Vector3.right, spinSpeed * Time.deltaTime, Space.Self);

        if (isArc == false)
        {
            Enemy enemy = FindEnemyOnPath(out Vector3 hitPoint);
            if (enemy != null)
            {
                EnemyHit(enemy, hitPoint);
                myPool.Release(this);
                return;
            }
        }

        lastPosition = transform.position;

        if (traveledDistance < range)
            return;

        if (isExplosive)
            Explode(transform.position);

        myPool.Release(this);
    }

    // 사정거리 방향으로 이동한다. 포물선 총알은 높이를 더해 사정거리 끝 지면까지 곡선을 그린다.
    private void Move()
    {
        traveledDistance = Mathf.Min(traveledDistance + speed * Time.deltaTime, range);
        Vector3 position = startPosition + direction * traveledDistance;

        if (isArc)
        {
            float progress = traveledDistance / range;
            position.y = Mathf.Lerp(startPosition.y, groundY, progress)
                         + 4f * range * arcHeightPerRange * progress * (1f - progress);
        }

        transform.position = position;
    }

    // 이번 프레임 이동 경로 근처의 적 중 경로 시작점에 가장 가까운 적을 찾는다.
    private Enemy FindEnemyOnPath(out Vector3 hitPoint)
    {
        Enemy closestEnemy = null;
        float closestSqrDistance = float.MaxValue;
        float sqrHitRadius = hitRadius * hitRadius;
        hitPoint = transform.position;

        foreach (Enemy enemy in ListManager.Enemies)
        {
            if (enemy.IsDead)
                continue;

            Vector3 pointOnPath = ClosestPointOnPath(enemy.transform.position);
            Vector3 offset = enemy.transform.position - pointOnPath;
            offset.y = 0f;
            if (offset.sqrMagnitude > sqrHitRadius)
                continue;

            float sqrDistanceFromStart = (pointOnPath - lastPosition).sqrMagnitude;
            if (sqrDistanceFromStart >= closestSqrDistance)
                continue;

            closestSqrDistance = sqrDistanceFromStart;
            closestEnemy = enemy;
            hitPoint = pointOnPath;
        }

        return closestEnemy;
    }

    // 이번 프레임 이동 경로(lastPosition → 현재 위치) 위에서 target과 가장 가까운 점을 구한다.
    private Vector3 ClosestPointOnPath(Vector3 target)
    {
        Vector3 path = transform.position - lastPosition;
        if (path.sqrMagnitude == 0f)
            return lastPosition;

        float t = Mathf.Clamp01(Vector3.Dot(target - lastPosition, path) / path.sqrMagnitude);
        return lastPosition + path * t;
    }

    private void EnemyHit(Enemy enemy, Vector3 hitPoint)
    {
        if (isExplosive)
            Explode(hitPoint);
        else
            enemy.OnDamage(damage);
    }

    // center에서 수평 거리 splashRadius 안의 모든 적에게 데미지를 준다.
    private void Explode(Vector3 center)
    {
        ExplosionEffectPool.Spawn(explosionPrefab, center, splashRadius);

        float sqrSplashRadius = splashRadius * splashRadius;

        // 데미지로 적이 죽으면 목록에서 빠질 수 있으므로 뒤에서부터 순회한다.
        for (int i = ListManager.Enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = ListManager.Enemies[i];

            Vector3 offset = enemy.transform.position - center;
            offset.y = 0f;
            if (offset.sqrMagnitude <= sqrSplashRadius)
                enemy.OnDamage(damage);
        }
    }

    public void SetPool(IObjectPool<Bullet> pool)
    {
        myPool = pool;
    }

    public void Init(int damage, float range, float speed, float splashRadius, WeaponKind kind)
    {
        this.damage = damage;
        this.range = range;
        this.speed = speed;
        this.splashRadius = splashRadius;

        isArc = kind == WeaponKind.Grenade;
        isSpinning = isArc || kind == WeaponKind.Throwing;
        isExplosive = kind == WeaponKind.Rocket || kind == WeaponKind.Grenade;

        direction = transform.forward;
        direction.y = 0;
        direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;

        startPosition = transform.position;
        lastPosition = startPosition;
        groundY = PlayerController.instance.transform.position.y;
        traveledDistance = 0f;
    }
}
