using System;
using System.Collections;
using UnityEngine;
using Animal_Run.Scripts.Data;
using UnityEngine.Pool;

public class Weapon : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private Transform firePosition;

    private Bullet bulletPrefab;
    private int bulletDamage;
    private float bulletRange;
    private float bulletSpeed;
    private float bulletSplashRadius;
    private float attackInterval;
    private WeaponKind weaponKind;

    private  ObjectPool<Bullet> bulletPool;
    private Transform bulletContainer;
    
    private bool isInitialized;

    public void Init(WeaponDef myDef, Transform poolContainer)
    {
        bulletPrefab = myDef.BulletPrefab;
        bulletDamage = myDef.BulletDamage;
        bulletRange = myDef.BulletRange;
        bulletSpeed = myDef.BulletSpeed;
        bulletSplashRadius = myDef.BulletSplashRadius;
        attackInterval = myDef.AttackInterval;
        weaponKind = myDef.Kind;
        bulletContainer = poolContainer;
        
        InitializePool();
        isInitialized = true;
        StartCoroutine(FireRoutine());
    }

    private void OnEnable()
    {
        if(isInitialized)
            StartCoroutine(FireRoutine());
    }

    private void OnDestroy()
    {
        // 러너가 죽으면 풀에 대기 중인 총알도 함께 정리한다.
        bulletPool?.Clear();
    }

    private IEnumerator FireRoutine()
    {
        while (true)
        {
            if (GameManager.instance.IsGameState(GameManager.GameState.Game))
                Fire();

            // 공격 속도 증강이 즉시 반영되도록 매번 대기 시간을 다시 계산한다.
            yield return new WaitForSeconds(attackInterval / AugmentStats.AttackSpeedMultiplier);
        }
    }

    private void InitializePool()
    {
        bulletPool = new ObjectPool<Bullet>(
            createFunc: () => {
                // 1. 풀에 여유가 없을 때 새로 생성
                Bullet bullet = Instantiate(bulletPrefab, bulletContainer);
                // 총알에게 자신이 속할 풀의 정보를 넘겨주어, 스스로 풀에 반납할 수 있게 합니다.
                bullet.SetPool(bulletPool);
                return bullet;
            },
            actionOnGet: (bullet) => {
                // 2. 풀에서 꺼낼 때
                bullet.gameObject.SetActive(true);
            },
            actionOnRelease: (bullet) => {
                // 3. 풀에 반납할 때
                bullet.gameObject.SetActive(false);
            },
            actionOnDestroy: (bullet) => {
                // 4. 풀이 터지거나 파괴될 때
                if (bullet != null)
                    Destroy(bullet.gameObject);
            },
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: 50
        );
    }

    public void Fire()
    {
        Vector3 spawnPos = firePosition.position;
        Quaternion spawnRot = Quaternion.identity;

        Bullet bulletObj = bulletPool.Get();

        bulletObj.transform.position = spawnPos;
        bulletObj.transform.rotation = spawnRot;

        float finalBulletSpeed = bulletSpeed * AugmentStats.BulletSpeedMultiplier + PlayerController.instance.moveSpeed;
        int finalDamage = Mathf.RoundToInt(bulletDamage * AugmentStats.DamageMultiplier);
        float finalRange = bulletRange * AugmentStats.RangeMultiplier;
        float finalSplashRadius = bulletSplashRadius + AugmentStats.BonusSplashRadius;

        bulletObj.Init(finalDamage, finalRange, finalBulletSpeed, finalSplashRadius, weaponKind);
    }
}
