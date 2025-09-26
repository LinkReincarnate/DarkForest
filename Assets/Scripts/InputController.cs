
using UnityEngine;
namespace DarkForest
{
    public class InputController : MonoBehaviour
    {
        public GameState game;
        public BoardRenderer rendererView;
        public int targetLayer = 0;
        public int defenderIndex = 1;

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    var local = rendererView.transform.InverseTransformPoint(hit.point);
                    int x = Mathf.FloorToInt(local.x / rendererView.cellSize + 0.5f);
                    int y = Mathf.FloorToInt(local.y / rendererView.cellSize + 0.5f);
                    var atk = game.players[0];
                    var def = game.players[Mathf.Clamp(defenderIndex,0,game.players.Count-1)];
                    var outcome = game.LaunchProbe(atk, def, x,y,targetLayer);
                    rendererView.RenderCentral(atk, def);
                    Debug.Log($"Probe ({x},{y},{targetLayer}) -> {(outcome.isHit?"HIT": outcome.isNear? "NEAR":"MISS")}");
                }
            }
        }
    }
}
