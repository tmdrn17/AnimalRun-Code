using System;
using System.Collections;
using Animal_Run.Scripts.Data;
using UnityEngine;

public class AnimalSystem : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private Transform runnersParent;
    [SerializeField] private PlayVFX playVFX;

    [Header(" Settings ")] 
    [SerializeField] private float radius;
    [SerializeField] private float angle;
    [SerializeField] private int maxRunners = 150;

    private AnimalDef animalDef;
    private WeaponDef weaponDef;
    private Transform bulletContainer;
    
    public PlayVFX PlayVFX => playVFX;
    
    private void Start()
    {
        playerAnimator = GetComponent<PlayerAnimator>();
        playVFX = GetComponentInChildren<PlayVFX>();
    }

    private void Update()
    {
        if (!GameManager.instance.IsGameState(GameManager.GameState.Game))
            return;

        PlaceRunners();

        if (GetRunnersCount() <= 0)
            GameManager.instance.SetGameState(GameManager.GameState.Gameover);
    }

    private void PlaceRunners()
    {
        // 꺼진 러너는 건너뛰고, 켜진 러너에게만 0번부터 자리를 매겨 빈자리가 생기지 않게 한다.
        int index = 0;

        for (int i = 0; i < runnersParent.childCount; i++)
        {
            Transform runner = runnersParent.GetChild(i);
            if (runner.gameObject.activeSelf == false)
                continue;

            if (Vector3.Distance(runner.localPosition, GetRunnerLocalPosition(index)) > 0.01f)
                runner.localPosition =
                    Vector3.Lerp(runner.localPosition, GetRunnerLocalPosition(index), Time.deltaTime * 10);
            else
                runner.localPosition = GetRunnerLocalPosition(index);
            index++;
        }
    }

    private Vector3 GetRunnerLocalPosition(int index)
    {
        float x = radius * Mathf.Sqrt(index) * Mathf.Cos(Mathf.Deg2Rad * index * angle);
        float z = radius * Mathf.Sqrt(index) * Mathf.Sin(Mathf.Deg2Rad * index * angle);

        return new Vector3(x, 0, z);
    }

    public float GetAnimalGroupRadius()
    {
        return radius * Mathf.Sqrt(GetRunnersCount());
    }

    public int GetRunnersCount()
    {
        int count = 0;
        
        for (int i = 0; i < runnersParent.childCount; i++)
        {
            if(runnersParent.GetChild(i).gameObject.activeSelf)
                count++;
        }
        
        return count;
    }

    /// <summary>
    /// 선택한 동물과 무기를 저장해 두고, 이후 증식하는 러너도 같은 동물·무기로 생성한다.
    /// </summary>
    public void SetRunnerData(AnimalDef animal, WeaponDef weapon, Transform poolContainer)
    {
        animalDef = animal;
        weaponDef = weapon;
        bulletContainer = poolContainer;
    }

    public Runner SpawnRunner()
    {
        for (int i = 0; i < runnersParent.childCount; i++)
        {
            if (runnersParent.GetChild(i).gameObject.activeSelf == false)
            {
                Transform child = runnersParent.GetChild(i);
                Runner disableRunner = child.GetComponent<Runner>();
                
                disableRunner.transform.localPosition = Vector3.zero;
                disableRunner.transform.localRotation = Quaternion.identity;
                disableRunner.gameObject.SetActive(true);
                
                return disableRunner;
            }
        }
        
        Runner runner = Instantiate(animalDef.Prefab, runnersParent);
        runner.transform.localPosition = Vector3.zero;
        runner.transform.localRotation = Quaternion.identity;
        return runner;
    }

    public void EquipWeapon(Runner runner)
    {
        if (runner.weaponPosition.gameObject.activeSelf == false)
        {
            Debug.Log("weapon return");
            runner.weaponPosition.gameObject.SetActive(true);
            return;
        }
        
        Weapon weapon = Instantiate(weaponDef.Prefab, runner.weaponPosition);
        weapon.transform.localPosition = Vector3.zero;
        // 프리팹에 지정된 회전을 그대로 사용한다. (도끼·식칼은 X축 90도로 머리 위에 누워 있다.)
        weapon.transform.localRotation = weaponDef.Prefab.transform.localRotation;
        weapon.Init(weaponDef, bulletContainer);
    }

    public void ApplyBonus(BonusType bonusType, int bonusAmount)
    {
        int currentCount = GetRunnersCount();
        int targetCount = currentCount;

        // 문의 수식 결과가 음수일 수도 있다. (예: +(-3) 은 3명 감소, -(-3) 은 3명 증가)
        switch (bonusType)
        {
            case BonusType.Addition:
                targetCount = currentCount + bonusAmount;
                break;

            case BonusType.Difference:
                targetCount = currentCount - bonusAmount;
                break;

            case BonusType.Product:
                targetCount = currentCount * bonusAmount;
                break;

            case BonusType.Division:
                if (bonusAmount != 0)
                    targetCount = currentCount / bonusAmount;
                break;
        }

        ChangeRunnersCount(targetCount);
    }

    public void ChangeRunnersCount(int targetCount)
    {
        targetCount = Mathf.Clamp(targetCount, 0, maxRunners);
        int currentCount = GetRunnersCount();

        if (targetCount > currentCount)
            AddRunners(targetCount - currentCount);
        else if (targetCount < currentCount)
            RemoveRunners(currentCount - targetCount);
    }

    private void AddRunners(int amount)
    {
        if (animalDef == null || weaponDef == null)
            return;

        for (int i = 0; i < amount; i++)
        {
            Runner runner = SpawnRunner();
            EquipWeapon(runner);
            runner.AddEffectPlay();
        }

        playVFX.PlayParticle(ParticleType.GetRunner, GetAnimalGroupRadius());
        playerAnimator.Run();
    }

    private void RemoveRunners(int amount)
    {
        // 뒤에서부터 켜진 러너만 amount 마리 제거한다. 이미 꺼진 러너는 세지 않는다.
        for (int i = runnersParent.childCount - 1; i >= 0 && amount > 0; i--)
        {
            Transform child = runnersParent.GetChild(i);
            
            if (child.gameObject.activeSelf)
            {
                child.GetComponent<Runner>().Dead();
                amount--;
            }
        }
    }
    

}