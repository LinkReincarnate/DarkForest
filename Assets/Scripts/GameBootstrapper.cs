using UnityEngine;

namespace DarkForest
{
    public class GameBootstrapper : MonoBehaviour
    {
        public GameState game;
        public BoardRenderer enemyBoardView;
        public BoardRenderer myBoardView;

        void Start()
        {
            if (!game) return;

            // Ensure the game's BodyCatalog is available. Prefer any catalog assigned
            // in the editor; however if the assigned catalog appears to contain null
            // prototype references (common when asset references weren't serialized),
            // replace it with a runtime-built catalog so definitions contain usable
            // power prototypes used at placement/colonization.
            if (game.catalog == null)
            {
                game.catalog = BodyFactory.BuildCatalog();
            }
            else
            {
                // Quick heuristic: if any entry with declared placement/colonization
                // powers only contains null references, rebuild the catalog to avoid
                // runtime failures.
                bool anyBad = false;
                try
                {
                    foreach (var entry in game.catalog.entries ?? new BodyDefinition[0])
                    {
                        if (entry == null) continue;
                        if ((entry.colonizationPowers != null && entry.colonizationPowers.Length > 0) ||
                            (entry.placementPowers != null && entry.placementPowers.Length > 0))
                        {
                            // If all declared powers are null, mark as bad
                            int total = 0, valid = 0;
                            if (entry.colonizationPowers != null) { total += entry.colonizationPowers.Length; foreach (var p in entry.colonizationPowers) if (p != null) valid++; }
                            if (entry.placementPowers != null) { total += entry.placementPowers.Length; foreach (var p in entry.placementPowers) if (p != null) valid++; }
                            if (total > 0 && valid == 0) { anyBad = true; break; }
                        }
                    }
                }
                catch { anyBad = false; }

                if (anyBad)
                {
                    Debug.Log("[GameBootstrapper] Inspector-assigned BodyCatalog appears to have missing prototype references; replacing with runtime-built catalog.");
                    game.catalog = BodyFactory.BuildCatalog();
                }
            }

            game.NewGame(game.playerCount, game.config.layers);
            RefreshRenderers();
        }

        public void RefreshRenderers()
        {
            if (!game) return;

            var active = game.Current;
            var defender = game.GetOpponent(active);
            bool isPlacement = game.phase == GameState.GamePhase.Placement;
            bool allowPlacement = !game.gameOver && !game.HasPendingProbe && isPlacement;
            bool allowProbe = !game.gameOver && !game.HasPendingProbe && !isPlacement;

            if (enemyBoardView)
            {
                enemyBoardView.revealOccupants = false;
                if (active != null && defender != null)
                {
                    enemyBoardView.RenderCentral(active, defender, allowProbe);
                }
                else
                {
                    enemyBoardView.Clear();
                }
            }

            if (myBoardView)
            {
                myBoardView.revealOccupants = true;
                if (active != null)
                {
                    myBoardView.RenderHidden(active, allowPlacement);
                }
                else
                {
                    myBoardView.Clear();
                }
            }
        }
    }
}
