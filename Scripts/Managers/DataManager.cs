using System;
using Animal_Run.Scripts.Data;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class DataManager : MonoBehaviour
{
    public static DataManager instance;
    public static Action onLevelUp;

    [Header(" Exp Texts ")]
    [SerializeField] private Text[] expTexts;

    [Header(" Exp Settings ")]
    [SerializeField] private int baseRequiredExp = 5;
    [SerializeField] private int requiredExpIncrease = 3;

    public WeaponTable weaponTable;
    public AnimalTable animalTable;
    public AugmentTable augmentTable;

    // 경험치는 한 판 동안만 유지된다. (코인 1개 = 경험치 1)
    private int exp;
    private int level = 1;
    private int requiredExp;

    private void Awake()
    {
        if (instance != null)
            Destroy(gameObject);
        else
            instance = this;

        requiredExp = baseRequiredExp;

        animalTable = new AnimalTable();
        weaponTable = new WeaponTable();
        augmentTable = new AugmentTable();

        animalTable.Load();
        weaponTable.Load();
        augmentTable.Load();
    }

    private void Start()
    {
        UpdateExpTexts();
    }

    private void UpdateExpTexts()
    {
        foreach(Text expText in expTexts)
        {
            expText.text = $"Lv.{level}  {exp}/{requiredExp}";
        }
    }

    public void AddExp(int amount)
    {
        exp += amount;

        while (exp >= requiredExp)
        {
            exp -= requiredExp;
            level++;
            requiredExp += requiredExpIncrease;

            onLevelUp?.Invoke();
        }

        UpdateExpTexts();
    }

    public int GetLevel()
    {
        return level;
    }
}
