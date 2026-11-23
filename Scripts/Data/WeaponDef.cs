using UnityEngine;

namespace Animal_Run.Scripts.Data
{
    public enum WeaponKind
    {
        Throwing,
        Gun,
        Rocket,
        Grenade
    }
        
    public class WeaponDTO
    {
        public int ID;
        public string Name;
        public string PrefabKey;
        public string SpriteKey;
        public WeaponKind Kind;
        public float AttackInterval;
        
        public string BulletPrefabKey;
        public int BulletDamage;
        public float BulletRange;
        public float BulletSpeed;
        public float BulletSplashRadius;
    }
    
    [System.Serializable]
    public class WeaponDef
    {
        public int ID;
        public string Name;
        public Weapon Prefab;
        public Sprite Sprite;
        public WeaponKind Kind;
        public float AttackInterval;
        
        public Bullet BulletPrefab;
        public int BulletDamage;
        public float BulletRange;
        public float BulletSpeed;
        public float BulletSplashRadius;
    }
}