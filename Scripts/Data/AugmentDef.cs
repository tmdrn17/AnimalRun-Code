using UnityEngine;

namespace Animal_Run.Scripts.Data
{
    public enum AugmentType
    {
        Damage,
        AttackSpeed,
        Range,
        BulletSpeed,
        Splash,
        AddRunners,
        MultiplyRunners
    }

    public class AugmentDTO
    {
        public int ID;
        public string Name;
        public string SpriteKey;
        public string Description;
        public AugmentType Type;
        public float Value;
    }

    [System.Serializable]
    public class AugmentDef
    {
        public int ID;
        public string Name;
        public Sprite Sprite;
        public string Description;
        public AugmentType Type;
        public float Value;

        // 러너 수를 바꾸는 증강인지. 카드의 배지 · 태그 색(무리 강화 / 무기 강화)을 나누는 데 쓴다.
        public bool IsRunnerAugment => Type == AugmentType.AddRunners || Type == AugmentType.MultiplyRunners;

        /// <summary>
        /// 선택한 무기에서 의미가 있는 증강인지. 폭발 범위 증강은 폭발하는 무기(Rocket, Grenade)에만 등장한다.
        /// </summary>
        public bool IsAvailableFor(WeaponDef weapon)
        {
            if (Type == AugmentType.Splash)
                return weapon.Kind == WeaponKind.Rocket || weapon.Kind == WeaponKind.Grenade;

            return true;
        }
    }
}
