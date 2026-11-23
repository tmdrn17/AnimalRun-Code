using System.Collections.Generic;
using System.IO;
using FileIO;
using UnityEngine;

namespace Animal_Run.Scripts.Data
{
    public class AnimalTable
    {
        private const string TableName = "Table/Animal Run/Animals.tsv";
        
        private Dictionary<int, AnimalDef> animalDefs = new();

        public int Count => animalDefs.Count;

        public IReadOnlyCollection<AnimalDef> All => animalDefs.Values;
        public AnimalDef Get(int id) => animalDefs[id];
        
        public void Load()
        {
            animalDefs.Clear();

            string path = Path.Combine(Application.streamingAssetsPath, TableName);
            List<AnimalDTO> rows = TSVReader.ReadTable<AnimalDTO>(path);
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                AnimalDTO dto = rows[i];
                Runner prefab = Resources.Load<Runner>(dto.PrefabKey);
                if(prefab == null) 
                    Debug.LogWarning($"[UnitTable] {dto.PrefabKey} 경로에 파일 없음");
                Sprite sprite = Resources.Load<Sprite>(dto.SpriteKey);
                if(sprite == null) 
                    Debug.LogWarning($"[UnitTable] {dto.SpriteKey} 경로에 파일 없음");

                AnimalDef def = new AnimalDef();
                def.ID = dto.ID;
                def.Name = dto.Name;
                def.Prefab = prefab;
                def.Sprite = sprite;
                def.Kind = dto.Kind;
                
                animalDefs.Add(dto.ID, def);
            }
        }
    }
}