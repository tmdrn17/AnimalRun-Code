using UnityEngine;

namespace Animal_Run.Scripts.Data
{
    public enum AnimalKind
    {
        Small,
        Medium,
        Large
    }
        
    public class AnimalDTO
    {
        public int ID;
        public string Name;
        public string PrefabKey;
        public string SpriteKey;
        public AnimalKind Kind;
    }
    
    [System.Serializable]
    public class AnimalDef
    {
        public int ID;
        public string Name;
        public Runner Prefab;
        public Sprite Sprite;
        public AnimalKind Kind;
    }
}