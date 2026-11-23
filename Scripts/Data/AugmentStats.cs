namespace Animal_Run.Scripts.Data
{
    /// <summary>
    /// 이번 판에 획득한 증강 효과의 누적값. 씬을 다시 불러오면 SelectManager가 초기화한다.
    /// </summary>
    public static class AugmentStats
    {
        public static float DamageMultiplier { get; private set; } = 1f;
        public static float AttackSpeedMultiplier { get; private set; } = 1f;
        public static float RangeMultiplier { get; private set; } = 1f;
        public static float BulletSpeedMultiplier { get; private set; } = 1f;
        public static float BonusSplashRadius { get; private set; }

        public static void Reset()
        {
            DamageMultiplier = 1f;
            AttackSpeedMultiplier = 1f;
            RangeMultiplier = 1f;
            BulletSpeedMultiplier = 1f;
            BonusSplashRadius = 0f;
        }

        public static void Apply(AugmentDef augment)
        {
            switch (augment.Type)
            {
                case AugmentType.Damage:
                    DamageMultiplier += augment.Value;
                    break;

                case AugmentType.AttackSpeed:
                    AttackSpeedMultiplier += augment.Value;
                    break;

                case AugmentType.Range:
                    RangeMultiplier += augment.Value;
                    break;

                case AugmentType.BulletSpeed:
                    BulletSpeedMultiplier += augment.Value;
                    break;

                case AugmentType.Splash:
                    BonusSplashRadius += augment.Value;
                    break;
            }
        }
    }
}
