
using UnityEngine;
namespace DarkForest
{
    [CreateAssetMenu(menuName="DarkForest/BodyCatalog")]
    public class BodyCatalog : ScriptableObject
    {
        public BodyDefinition[] entries;
        public BodyDefinition Get(BodyType type)
        {
            foreach (var e in entries) if (e.type == type) return e;
            Debug.LogError("Body not found: " + type);
            return null;
        }
    }
}
