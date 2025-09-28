using UnityEditor;
using UnityEngine;

namespace DarkForest.EditorTools
{
    public class CreateRetroHUD
    {
        [MenuItem("Tools/Create Retro HUD")]
        public static void CreateHUD()
        {
            var hudGO = new GameObject("RetroHUD");
            var hud = hudGO.AddComponent<DarkForest.RetroHUD>();

            var placement = Object.FindObjectOfType<DarkForest.PlacementUI>();
            var game = Object.FindObjectOfType<DarkForest.GameState>();

            if (placement != null) hud.placementUI = placement;
            if (game != null) hud.game = game;

            Selection.activeGameObject = hudGO;
            Debug.Log("RetroHUD created and wired (assign font and tweak colors in Inspector). If references weren't found, assign them manually.");
        }
    }
}
