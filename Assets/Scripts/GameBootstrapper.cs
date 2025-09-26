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
