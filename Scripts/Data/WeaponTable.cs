using System.Collections.Generic;
using System.IO;
using FileIO;
using UnityEngine;

namespace Animal_Run.Scripts.Data
{
    public class WeaponTable
    {
        private const string TableName = "Table/Animal Run/Weapons.tsv";
        
        private Dictionary<int, WeaponDef> WeaponDefs = new();

        public int Count => WeaponDefs.Count;

        public IReadOnlyCollection<WeaponDef> All => WeaponDefs.Values;
        public WeaponDef Get(int id) => WeaponDefs[id];
        
        public void Load()
        {
            WeaponDefs.Clear();

            string path = Path.Combine(Application.streamingAssetsPath, TableName);
            List<WeaponDTO> rows = TSVReader.ReadTable<WeaponDTO>(path);
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                WeaponDTO dto = rows[i];
                Weapon weaponPrefab = Resources.Load<Weapon>(dto.PrefabKey);
                if(weaponPrefab == null) 
                    Debug.LogWarning($"[Table] {dto.PrefabKey} 경로에 파일 없음");
                Bullet bulletPrefab = Resources.Load<Bullet>(dto.BulletPrefabKey);
                if(bulletPrefab == null) 
                    Debug.LogWarning($"[Table] {dto.BulletPrefabKey} 경로에 파일 없음");
                Sprite sprite = Resources.Load<Sprite>(dto.SpriteKey);
                if(sprite == null) 
                    Debug.LogWarning($"[Table] {dto.SpriteKey} 경로에 파일 없음");

                WeaponDef def = new WeaponDef();
                def.ID = dto.ID;
                def.Name = dto.Name;
                def.Prefab = weaponPrefab;
                def.Sprite = sprite;
                def.Kind = dto.Kind;
                def.AttackInterval = dto.AttackInterval;
                
                def.BulletPrefab = bulletPrefab;
                def.BulletDamage = dto.BulletDamage;
                def.BulletRange = dto.BulletRange;
                def.BulletSpeed = dto.BulletSpeed;
                def.BulletSplashRadius = dto.BulletSplashRadius;
                
                WeaponDefs.Add(dto.ID, def);
            }
        }
    }
}