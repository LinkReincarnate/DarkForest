
using UnityEngine;
namespace DarkForest
{
    [CreateAssetMenu(menuName = "DarkForest/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public int width = 15;
        public int height = 15;
        public int layers = 1;

        public int startingCurrencySingle = 20;
        public int startingCurrencyTwo = 32;
        public int startingCurrencyThree = 40;

        public bool powersLockOnPartialColonization = true;
        public bool enableQuadrantsOnCentralBoard = true;
        public bool enableNearMissReporting = true;

        public int GetStartingCurrency(int players, int depth)
        {
            if (depth == 1) return startingCurrencySingle;
            if (depth == 2) return startingCurrencyTwo;
            return startingCurrencyThree;
        }
    }
}
