using System;
using System.Collections.Generic;
using Animal_Run.Scripts.Data;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class SelectManager : MonoBehaviour
{
    [Header(" Elements ")]
    [SerializeField] private SelectUI selectUI;
    [FormerlySerializedAs("crowdSystem")]
    [SerializeField] private AnimalSystem animalSystem;

    [Header(" Positions ")]
    [SerializeField] private Transform runnerPosition;
    [SerializeField] private Transform poolContainer;

    private DataManager dataManager;

    private readonly List<AnimalDef> animalDefs = new();
    private readonly List<WeaponDef> weaponDefs = new();
    private readonly List<AugmentDef> augmentDefs = new();

    // 고른 무기에서 등장할 수 있는 증강 후보. 무기는 한 판에 한 번만 고르므로 그때 한 번 채워 두고 레벨업마다 재사용한다.
    private readonly List<AugmentDef> availableAugments = new();
    private Func<int, AugmentDef> getAvailableAugment;

    private AnimalDef selectedAnimal;
    private WeaponDef selectedWeapon;
    private Runner firstRunner;

    // 한 번에 여러 레벨이 오르면 그 횟수만큼 연달아 증강을 고른다.
    private int pendingAugments;

    private void Awake()
    {
        AugmentStats.Reset();

        // 레벨업마다 람다를 새로 만들지 않도록 한 번만 만들어 둔다.
        getAvailableAugment = index => availableAugments[index];

        if (animalSystem == null)
            animalSystem = runnerPosition.GetComponentInParent<AnimalSystem>();
    }

    private void Start()
    {
        dataManager = DataManager.instance;
        DataManager.onLevelUp += LevelUpCallback;

        PickRandomUnique(animalDefs, selectUI.AnimalButtonCount, dataManager.animalTable.Count, dataManager.animalTable.Get);
        
        for (int i = 0; i < animalDefs.Count; i++)
            selectUI.SetAnimalButton(i, animalDefs[i]);

        PickRandomUnique(weaponDefs, selectUI.WeaponButtonCount, dataManager.weaponTable.Count, dataManager.weaponTable.Get);

        for (int i = 0; i < weaponDefs.Count; i++)
            selectUI.SetWeaponButton(i, weaponDefs[i]);
    }
    
    private static void PickRandomUnique<T>(List<T> result, int pickCount, int poolCount, Func<int, T> getById)
    {
        result.Clear();
        pickCount = Mathf.Min(pickCount, poolCount);   // 후보가 버튼보다 적어도 무한 루프에 빠지지 않게

        while (true)
        {
            if(result.Count >= pickCount) break;
            T candidate = getById(Random.Range(0, poolCount));
            if (result.Contains(candidate)) continue;
            result.Add(candidate);
        }
    }

    private void OnDestroy()
    {
        DataManager.onLevelUp -= LevelUpCallback;

        // 증강 선택 중에 씬을 벗어나도 시간이 멈춘 채로 남지 않게 한다.
        if (pendingAugments > 0)
            Time.timeScale = 1.0f;
    }

    public void SelectRunner(int index)
    {
        selectedAnimal = animalDefs[index];
        animalSystem.SetRunnerData(selectedAnimal, null, poolContainer);

        // 무기 선택 화면에서 고른 동물을 미리 보여준다.
        firstRunner = animalSystem.SpawnRunner();

        selectUI.ShowWeaponPanel();
    }

    public void SelectWeapon(int index)
    {
        selectedWeapon = weaponDefs[index];

        availableAugments.Clear();
        foreach (AugmentDef augment in dataManager.augmentTable.All)
        {
            if (augment.IsAvailableFor(selectedWeapon))
                availableAugments.Add(augment);
        }

        animalSystem.SetRunnerData(selectedAnimal, selectedWeapon, poolContainer);
        animalSystem.EquipWeapon(firstRunner);

        selectUI.CloseAll();

        GameManager.instance.SetGameState(GameManager.GameState.Game);
    }

    private void LevelUpCallback()
    {
        animalSystem.PlayVFX.PlayParticle(ParticleType.LevelUp, animalSystem.GetAnimalGroupRadius());
        
        pendingAugments++;

        if (pendingAugments == 1)
            ShowAugments();
    }

    private void ShowAugments()
    {
        Time.timeScale = 0.0f;
        PlayerController.instance.StopMoving();

        PickRandomUnique(augmentDefs, selectUI.AugmentButtonCount, availableAugments.Count, getAvailableAugment);

        for (int i = 0; i < augmentDefs.Count; i++)
        {
            selectUI.SetAugmentButton(i, augmentDefs[i], augmentDefs[i].Sprite);
        }

        selectUI.ShowAugmentPanel();
    }

    public void SelectAugment(int index)
    {
        if (pendingAugments <= 0)
            return;

        ApplyAugment(augmentDefs[index]);
        pendingAugments--;

        if (pendingAugments > 0)
        {
            ShowAugments();
            return;
        }

        selectUI.CloseAugmentPanel();
        Time.timeScale = 1.0f;
        PlayerController.instance.StartMoving();
    }

    private void ApplyAugment(AugmentDef augment)
    {
        int runnersCount = animalSystem.GetRunnersCount();

        switch (augment.Type)
        {
            case AugmentType.AddRunners:
                animalSystem.ChangeRunnersCount(runnersCount + (int)augment.Value);
                break;

            case AugmentType.MultiplyRunners:
                int runnersToAdd = Mathf.Max(1, Mathf.RoundToInt(runnersCount * augment.Value));
                animalSystem.ChangeRunnersCount(runnersCount + runnersToAdd);
                break;

            default:
                AugmentStats.Apply(augment);
                break;
        }
    }
}
