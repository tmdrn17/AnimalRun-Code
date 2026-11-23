using Animal_Run.Scripts.Data;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ButtonElement
{
    public Image buttonImage;     // 동물 · 무기 그림
    public Text name;
    public Text description;      // 효과. 숫자는 리치 텍스트로 크게 강조한다.

    [Header(" Card Style ")]
    public Image frame;           // 카드 테두리. 화면(동물 · 무기 · 증강)마다 색이 다르다.
    public Image badge;           // 왼쪽 위 작은 배지. 종류를 색으로 구분한다.
    public Image tagBackground;   // 아래쪽 알약 모양 태그 바탕
    public Text tag;              // 크기 · 무기 계열 · 증강 종류
}

// 선택 카드 색 규칙 (Docs/UI_디자인_프롬프트_선택카드.md 의 사양표)
public static class SelectCardStyle
{
    public static readonly Color AnimalFrame = Hex(0x3185F5);
    public static readonly Color WeaponFrame = Hex(0xE74747);
    public static readonly Color AugmentFrame = Hex(0xEAB83E);

    public static readonly Color AnimalBadge = Hex(0x1F5FBF);
    public static readonly Color WeaponBadge = Hex(0xB83232);
    public static readonly Color WeaponAugment = Hex(0xF58A28);   // 무기 강화 = 주황
    public static readonly Color RunnerAugment = Hex(0x55A94C);   // 무리 강화 = 초록

    public static readonly Color PictureSlot = Hex(0xFFF6E6);
    public static readonly Color NameText = Hex(0x342B32);
    public static readonly Color PanelDim = new Color32(0x10, 0x16, 0x2B, 0xCC);
    public static readonly Color TitleRibbon = Hex(0x2864C8);

    public const string EmphasisHex = "#F57B24";
    public const int EmphasisSize = 76;

    public static Color Hex(int rgb) =>
        new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

    public static string Emphasize(string value) =>
        $"<size={EmphasisSize}><b><color={EmphasisHex}>{value}</color></b></size>";

    public static string KindLabel(AnimalKind kind) => kind switch
    {
        AnimalKind.Small => "소형",
        AnimalKind.Large => "대형",
        _ => "중형"
    };

    public static string KindLabel(WeaponKind kind) => kind switch
    {
        WeaponKind.Throwing => "투척",
        WeaponKind.Gun => "총",
        WeaponKind.Rocket => "로켓",
        _ => "투척폭발"
    };
}

public class SelectUI : MonoBehaviour
{
    [Header(" Panels ")]
    [SerializeField] private GameObject animalSelectPanel;
    [SerializeField] private GameObject weaponSelectPanel;
    [SerializeField] private GameObject augmentSelectPanel;

    [Header(" Buttons ")]
    [SerializeField] private ButtonElement[] animalButtons;
    [SerializeField] private ButtonElement[] weaponButtons;
    [SerializeField] private ButtonElement[] augmentButtons;

    public int AnimalButtonCount => animalButtons.Length;
    public int WeaponButtonCount => weaponButtons.Length;
    public int AugmentButtonCount => augmentButtons.Length;

    // 동물 카드: 그림 → 이름 → 크기 태그 (효과 숫자 없음)
    public void SetAnimalButton(int index, AnimalDef animal)
    {
        ButtonElement button = animalButtons[index];
        SetCommon(button, animal.Sprite, animal.Name, string.Empty);
        SetStyle(button, SelectCardStyle.AnimalFrame, SelectCardStyle.AnimalBadge,
            SelectCardStyle.KindLabel(animal.Kind), SelectCardStyle.AnimalFrame);
    }

    // 무기 카드: 배지 → 그림 → 이름 → 공격력(강조) + 보조 수치
    public void SetWeaponButton(int index, WeaponDef weapon)
    {
        ButtonElement button = weaponButtons[index];
        string description =
            $"공격력 {SelectCardStyle.Emphasize(weapon.BulletDamage.ToString())}\n" +
            $"<size=28>간격 {weapon.AttackInterval}초 · 사거리 {weapon.BulletRange}</size>";

        SetCommon(button, weapon.Sprite, weapon.Name, description);
        SetStyle(button, SelectCardStyle.WeaponFrame, SelectCardStyle.WeaponBadge,
            SelectCardStyle.KindLabel(weapon.Kind), SelectCardStyle.WeaponFrame);
    }

    // 증강 카드: 테두리는 금색으로 같고, 무기 강화 / 무리 강화는 배지 · 태그 색으로만 구분한다.
    public void SetAugmentButton(int index, AugmentDef augment, Sprite sprite)
    {
        ButtonElement button = augmentButtons[index];
        Color typeColor = augment.IsRunnerAugment ? SelectCardStyle.RunnerAugment : SelectCardStyle.WeaponAugment;
        string typeLabel = augment.IsRunnerAugment ? "무리 강화" : "무기 강화";

        SetCommon(button, sprite, augment.Name, EmphasizeValue(augment.Description));
        SetStyle(button, SelectCardStyle.AugmentFrame, typeColor, typeLabel, typeColor);
    }

    // "공격 속도 +15%" → "공격 속도" 한 줄 + 큰 "+15%"
    private static string EmphasizeValue(string description)
    {
        int split = description.LastIndexOf(' ');
        if (split <= 0)
            return SelectCardStyle.Emphasize(description);

        return $"{description.Substring(0, split)}\n{SelectCardStyle.Emphasize(description.Substring(split + 1))}";
    }

    private static void SetCommon(ButtonElement button, Sprite sprite, string nameText, string descText)
    {
        button.buttonImage.sprite = sprite;
        button.name.text = nameText;
        button.description.text = descText;
    }

    // 스타일 칸은 씬에서 아직 연결 전이어도 동작하도록 비어 있으면 건너뛴다.
    private static void SetStyle(ButtonElement button, Color frameColor, Color badgeColor, string tagText, Color tagColor)
    {
        if (button.frame != null) button.frame.color = frameColor;
        if (button.badge != null) button.badge.color = badgeColor;
        if (button.tagBackground != null) button.tagBackground.color = tagColor;
        if (button.tag != null) button.tag.text = tagText;
    }

    public void ShowWeaponPanel()
    {
        animalSelectPanel.SetActive(false);
        weaponSelectPanel.SetActive(true);
    }

    public void ShowAugmentPanel()
    {
        augmentSelectPanel.SetActive(true);
    }

    public void CloseAugmentPanel()
    {
        augmentSelectPanel.SetActive(false);
    }

    public void CloseAll()
    {
        animalSelectPanel.SetActive(false);
        weaponSelectPanel.SetActive(false);
        augmentSelectPanel.SetActive(false);
    }
}
