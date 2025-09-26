using System.Collections.Generic;
using UnityEngine;

namespace DarkForest
{
    [CreateAssetMenu(menuName = "DarkForest/CardArtCatalog", fileName = "CardArtCatalog")]
    public class CardArtCatalog : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public BodyType bodyType;
            public Sprite art;
        }

        [SerializeField]
        private Entry[] entries;

        private Dictionary<BodyType, Sprite> lookup;

        void OnEnable()
        {
            BuildLookup();
        }

        void BuildLookup()
        {
            lookup = new Dictionary<BodyType, Sprite>();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                lookup[entry.bodyType] = entry.art;
            }
        }

        public Sprite GetSprite(BodyType type)
        {
            if (lookup == null)
            {
                BuildLookup();
            }

            return (lookup != null && lookup.TryGetValue(type, out var sprite)) ? sprite : null;
        }
    }
}
