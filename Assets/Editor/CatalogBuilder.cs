#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DarkForest
{
    public static class CatalogBuilder
    {
        private const string CatalogAssetPath = "Assets/Configs/BodyCatalog.asset";

        [MenuItem("DarkForest/Build Catalog Asset")]
        public static void BuildCatalog()
        {
            EnsureConfigFolder();

            var runtimeCatalog = BodyFactory.BuildCatalog();
            var assetCatalog = ScriptableObject.CreateInstance<BodyCatalog>();
            assetCatalog.entries = new BodyDefinition[runtimeCatalog.entries.Length];

            for (int i = 0; i < runtimeCatalog.entries.Length; i++)
            {
                var clone = ScriptableObject.CreateInstance<RuntimeBodyDefinition>();
                runtimeCatalog.entries[i].CopyTo(clone);
                clone.name = runtimeCatalog.entries[i].name;
                assetCatalog.entries[i] = clone;
            }

            AssetDatabase.CreateAsset(assetCatalog, CatalogAssetPath);
            foreach (var entry in assetCatalog.entries)
            {
                AssetDatabase.AddObjectToAsset(entry, assetCatalog);
                if (entry is RuntimeBodyDefinition runtimeDef)
                {
                    AddPowerSubassets(runtimeDef, entry);
                }
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = assetCatalog;
            Debug.Log($"DarkForest catalog rebuilt at {CatalogAssetPath}");
        }

        static void AddPowerSubassets(RuntimeBodyDefinition def, Object parent)
        {
            if (def.placementPowers != null)
            {
                foreach (var power in def.placementPowers)
                {
                    if (power) AssetDatabase.AddObjectToAsset(power, parent);
                }
            }
            if (def.colonizationPowers != null)
            {
                foreach (var power in def.colonizationPowers)
                {
                    if (power) AssetDatabase.AddObjectToAsset(power, parent);
                }
            }
        }

        private static void EnsureConfigFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Configs"))
            {
                AssetDatabase.CreateFolder("Assets", "Configs");
            }
        }
    }

    internal static class BodyDefinitionExtensions
    {
        public static void CopyTo(this BodyDefinition source, BodyDefinition target)
        {
            target.type = source.type;
            target.displayName = source.displayName;
            target.populationCapacity = source.populationCapacity;
            target.price = source.price;
            target.nearPattern = source.nearPattern;
            target.rulesSummary = source.rulesSummary;
            target.shape = source.shape != null ? source.shape.Clone() : new BodyShape();
            target.placementPowers = ClonePowers(source.placementPowers);
            target.colonizationPowers = ClonePowers(source.colonizationPowers);
        }

        static Power[] ClonePowers(Power[] powers)
        {
            if (powers == null || powers.Length == 0) return null;
            var result = new Power[powers.Length];
            for (int i = 0; i < powers.Length; i++)
            {
                if (!powers[i]) continue;
                var clone = ScriptableObject.Instantiate(powers[i]);
                clone.name = powers[i].name;
                result[i] = clone;
            }
            return result;
        }
    }
}
#endif