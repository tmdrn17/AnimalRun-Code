using System.Collections.Generic;
using System.IO;
using FileIO;
using UnityEngine;

namespace Animal_Run.Scripts.Data
{
    public class AugmentTable
    {
        private const string TableName = "Table/Animal Run/Augments.tsv";

        private Dictionary<int, AugmentDef> augmentDefs = new();

        public int Count => augmentDefs.Count;

        public IReadOnlyCollection<AugmentDef> All => augmentDefs.Values;
        public AugmentDef Get(int id) => augmentDefs[id];

        public void Load()
        {
            augmentDefs.Clear();

            string path = Path.Combine(Application.streamingAssetsPath, TableName);
            List<AugmentDTO> rows = TSVReader.ReadTable<AugmentDTO>(path);
            if (rows == null)
                return;

            for (int i = 0; i < rows.Count; i++)
            {
                AugmentDTO dto = rows[i];
                Sprite sprite = Resources.Load<Sprite>(dto.SpriteKey);
                if(sprite == null)
                    Debug.LogWarning($"[Table] {dto.SpriteKey} 경로에 파일 없음");

                AugmentDef def = new AugmentDef();
                def.ID = dto.ID;
                def.Name = dto.Name;
                def.Sprite = sprite;
                def.Description = dto.Description;
                def.Type = dto.Type;
                def.Value = dto.Value;

                augmentDefs.Add(dto.ID, def);
            }
        }
    }
}
