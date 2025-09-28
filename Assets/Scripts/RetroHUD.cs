using UnityEngine;

namespace DarkForest
{
    // Simple retro 50s-futuristic HUD using IMGUI. Add this to a GameObject and assign PlacementUI and GameState.
    public class RetroHUD : MonoBehaviour
    {
        // Singleton-like accessor for convenience (there may be only one HUD)
        public static RetroHUD Instance { get; private set; }
        public PlacementUI placementUI;
        public GameState game;

        // Styling parameters (tweak in Inspector)
        public Font retroFont;
        public Color panelColor = new Color(0.02f, 0.06f, 0.12f, 0.9f);
        public Color accentColor = new Color(0.9f, 0.6f, 0.2f, 1f);
        public Color textColor = new Color(0.95f, 0.95f, 0.9f, 1f);

        GUIStyle panelStyle;
        GUIStyle titleStyle;
        GUIStyle buttonStyle;
        GUIStyle smallLabelStyle;
        // Card sizing used by shop and hand layouts
        public float cardW = 120f;
        public float cardH = 80f;
    bool hasDrawnOnce = false;
    Vector2 shopScrollPos = Vector2.zero;
    Vector2 handScrollPos = Vector2.zero;
        // Simple transient modal fields (private; use ShowTransient to display)
        string transientTitle = null;
        string transientMessage = null;
        float transientTimer = 0f;

        // Public helper to show an informational transient modal
        public void ShowTransient(string title, string message, float seconds = 5f)
        {
            transientTitle = title;
            transientMessage = message;
            transientTimer = Mathf.Max(0f, seconds);
        }

        void Update()
        {
            if (transientTimer > 0f)
            {
                transientTimer -= Time.deltaTime;
                if (transientTimer <= 0f)
                {
                    transientTimer = 0f;
                    transientTitle = null;
                    transientMessage = null;
                }
            }
        }

        void Awake()
        {
            Instance = this;
            BuildStyles();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BuildStyles()
        {
            // Create styles without accessing GUI.skin (avoids errors when called from OnValidate)
            panelStyle = new GUIStyle();
            panelStyle.normal.background = MakeTex(2, 2, panelColor);
            panelStyle.border = new RectOffset(6, 6, 6, 6);
            panelStyle.padding = new RectOffset(12, 12, 12, 12);

            titleStyle = new GUIStyle();
            titleStyle.alignment = TextAnchor.UpperLeft;
            titleStyle.normal.textColor = accentColor;
            titleStyle.fontSize = 22;
            if (retroFont) titleStyle.font = retroFont;

            smallLabelStyle = new GUIStyle();
            smallLabelStyle.normal.textColor = textColor;
            smallLabelStyle.fontSize = 12;
            if (retroFont) smallLabelStyle.font = retroFont;

            buttonStyle = new GUIStyle();
            buttonStyle.normal.background = MakeTex(2, 2, accentColor * 0.9f);
            buttonStyle.hover.background = MakeTex(2, 2, accentColor * 1.05f);
            buttonStyle.normal.textColor = Color.black;
            buttonStyle.fontSize = 14;
            buttonStyle.padding = new RectOffset(8, 8, 6, 6);
            if (retroFont) buttonStyle.font = retroFont;
        }

        void OnValidate()
        {
            BuildStyles();
        }

        // Create and wire a RetroHUD in the scene if none exists. Safe to call at runtime.
        public static void EnsureExists(PlacementUI placementUI = null, GameState game = null)
        {
            var existing = Object.FindObjectOfType<RetroHUD>();
            if (existing != null) return;

            var hudGO = new GameObject("RetroHUD");
            var hud = hudGO.AddComponent<RetroHUD>();
            hud.placementUI = placementUI ?? Object.FindObjectOfType<PlacementUI>();
            hud.game = game ?? Object.FindObjectOfType<GameState>();
            Debug.Log("RetroHUD: Auto-created RetroHUD GameObject and wired references.");
        }

    void OnGUI()
        {
            // Draw on top
            GUI.depth = -1000;

            // One-time confirmation log so it's easy to see whether OnGUI ran
            if (!hasDrawnOnce)
            {
                hasDrawnOnce = true;
                Debug.Log("RetroHUD: OnGUI running and drawing HUD.");
            }

            // Auto-find references if they weren't assigned in the Inspector
            if (placementUI == null)
            {
                placementUI = FindObjectOfType<PlacementUI>();
            }
            if (game == null)
            {
                game = FindObjectOfType<GameState>();
            }

            if (game == null || placementUI == null)
            {
                if (game == null)
                {
                    Debug.LogWarning("RetroHUD: GameState not found in scene. Please add or assign a GameState.");
                }
                if (placementUI == null)
                {
                    Debug.LogWarning("RetroHUD: PlacementUI not found in scene. Please add or assign a PlacementUI.");
                }
                return;
            }

            if (panelStyle == null || titleStyle == null || buttonStyle == null || smallLabelStyle == null)
            {
                BuildStyles();
            }

            // Top-left status panel
            const int statusW = 300;
            const int statusH = 120;
            Rect statusRect = new Rect(10, 10, statusW, statusH);
            GUI.Box(statusRect, "", panelStyle);
            GUILayout.BeginArea(statusRect);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("ASTROCOMM", titleStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            var active = game.Current;
            int probesPerTurn = game.GetProbesPerTurn(active);
            int probesRemaining = active != null ? Mathf.Max(0, probesPerTurn - placementUI.ProbesTakenThisTurn) : 0;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Player: {(active != null ? active.team.ToString() : "-")}", smallLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Currency: {(active != null ? active.currency.ToString() : "-")}", smallLabelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Probes: {probesRemaining}/{probesPerTurn}", smallLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("End Turn", buttonStyle))
            {
                placementUI.EndTurnFromHUD();
            }
            // If in placement phase, show placement controls
            if (game != null && game.phase == GameState.GamePhase.Placement)
            {
                if (GUILayout.Button("Finish Placement", buttonStyle))
                {
                    placementUI.FinishPlacementFromHUD();
                }
            }
            else
            {
                if (placementUI != null)
                {
                    if (placementUI.IsInPlacementMode)
                    {
                        if (GUILayout.Button("Exit Placement Mode", buttonStyle))
                        {
                            placementUI.ExitPlacementFromHUD();
                        }
                    }
                    else
                    {
                        // If not in placement mode but the current player can afford a body,
                        // allow re-entering placement mode from the HUD.
                        if (placementUI.CurrentPlayerCanAffordAny() && GUILayout.Button("Enter Placement Mode", buttonStyle))
                        {
                            placementUI.EnterPlacementModeFromHUD();
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Pending defender decision modal (when a pending probe exists)
            bool pendingModalVisible = (game != null && game.HasPendingProbe && placementUI != null && game.pendingProbe != null);
            if (pendingModalVisible)
            {
                var pending = game.pendingProbe;
                if (pending != null)
                {
                    // Centered modal
                    int modalW = 420;
                    int modalH = 160;
                    Rect modalRect = new Rect((Screen.width - modalW) / 2, (Screen.height - modalH) / 2, modalW, modalH);
                    GUI.Box(modalRect, "", panelStyle);
                    GUILayout.BeginArea(modalRect);
                    GUILayout.BeginVertical();
                    GUILayout.Label("DEFENDER DECISION", titleStyle);
                    GUILayout.Space(6);
                    GUILayout.Label($"Targeted Cell: ({pending.x}, {pending.y}, {pending.z})", smallLabelStyle);
                    GUILayout.Space(4);
                    GUILayout.Label("Spend a False Report token to convert this hit to a miss?", smallLabelStyle);
                    GUILayout.Space(8);
                    GUILayout.BeginHorizontal();
                    bool hasToken = pending.defender != null && pending.defender.falseReportTokens > 0;
                    GUI.enabled = hasToken;
                    if (GUILayout.Button("Spend Token", buttonStyle, GUILayout.Width(140)))
                    {
                        placementUI.ResolvePendingProbeFromHUD(true);
                    }
                    GUI.enabled = true;
                    if (GUILayout.Button("Allow Hit", buttonStyle, GUILayout.Width(140)))
                    {
                        placementUI.ResolvePendingProbeFromHUD(false);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                }
            }

            // Right-side Shop panel
            int shopW = 300;
            int shopH = Mathf.Clamp(Screen.height - 40, 200, Screen.height - 40);
            Rect shopRect = new Rect(Screen.width - shopW - 10, 10, shopW, shopH);
            GUI.Box(shopRect, "", panelStyle);
            GUILayout.BeginArea(shopRect);
            GUILayout.BeginVertical();
            GUILayout.Label("STORE", titleStyle);
            GUILayout.Space(6);
            var player = game.Current;
            if (placementUI != null && placementUI.shop != null && placementUI.shop.Length > 0)
            {
                    int count = placementUI.shop.Length;
                    int cols = 2;
                    int rows = Mathf.CeilToInt(count / (float)cols);

                    shopScrollPos = GUILayout.BeginScrollView(shopScrollPos);
                    for (int r = 0; r < rows; r++)
                    {
                        GUILayout.BeginHorizontal();
                        for (int c = 0; c < cols; c++)
                        {
                            int i = r * cols + c;
                            if (i >= count) break;
                            var def = placementUI.shop[i];
                            if (def == null) continue;

                            bool affordable = player != null && player.currency >= def.price;
                            GUI.enabled = affordable && !game.gameOver;

                            // Try to get a card sprite from the game's cardArtCatalog using the body type
                            Texture img = null;
                            if (game != null && game.cardArtCatalog != null)
                            {
                                var spr = game.cardArtCatalog.GetSprite(def.type);
                                if (spr != null) img = spr.texture;
                            }

                            GUILayout.BeginVertical(GUILayout.Width(cardW));
                            if (img != null)
                            {
                                var content = new GUIContent(img, def.displayName + " - $" + def.price);
                                if (GUILayout.Button(content, GUILayout.Width(cardW), GUILayout.Height(cardH)))
                                {
                                    placementUI.SelectShopIndex(i);
                                }
                            }
                            else
                            {
                                if (GUILayout.Button(def.displayName, GUILayout.Width(cardW), GUILayout.Height(40)))
                                {
                                    placementUI.SelectShopIndex(i);
                                }
                            }

                            GUILayout.Label($"${def.price}" + (placementUI.selectedIndex == i ? "   (selected)" : ""), smallLabelStyle);
                            GUILayout.EndVertical();
                            GUILayout.Space(8f);
                            GUI.enabled = true;
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label("No bodies available", smallLabelStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Transient modal (informational) — drawn last so it appears on top
            bool transientVisible = transientTimer > 0f && !string.IsNullOrEmpty(transientTitle);
            if (transientVisible)
            {
                int modalW = 380;
                int modalH = 120;
                Rect modalRect = new Rect((Screen.width - modalW) / 2, (Screen.height - modalH) / 2, modalW, modalH);
                GUI.Box(modalRect, "", panelStyle);
                GUILayout.BeginArea(modalRect);
                GUILayout.BeginVertical();
                GUILayout.Label(transientTitle, titleStyle);
                GUILayout.Space(6);
                GUILayout.Label(transientMessage ?? string.Empty, smallLabelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("OK", buttonStyle, GUILayout.Width(120)))
                {
                    transientTimer = 0f;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            // Game Over modal
            if (game != null && game.gameOver)
            {
                int modalW = 420;
                int modalH = 180;
                Rect modalRect = new Rect((Screen.width - modalW) / 2, (Screen.height - modalH) / 2, modalW, modalH);
                GUI.Box(modalRect, "", panelStyle);
                GUILayout.BeginArea(modalRect);
                GUILayout.BeginVertical();
                GUILayout.Label("GAME OVER", titleStyle);
                GUILayout.Space(8);
                string winnerName = game.winner != null ? game.winner.team.ToString() : "No one";
                GUILayout.Label($"Winner: {winnerName}", smallLabelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (placementUI != null && GUILayout.Button("Restart Game", buttonStyle, GUILayout.Width(140)))
                {
                    placementUI.RestartGameFromHUD();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }

            // Bottom Hand panel
            int handH = 160;
            Rect handRect = new Rect(10, Screen.height - handH - 10, Screen.width - shopW - 40, handH);
            GUI.Box(handRect, "", panelStyle);
            GUILayout.BeginArea(handRect);
            GUILayout.BeginVertical();
            GUILayout.Label("HAND", titleStyle);
            GUILayout.Space(6);
            if (player != null && player.hand != null && player.hand.Count > 0)
            {
                int count = player.hand.Count;

                // Horizontal scroll row: lay out cards left-to-right
                GUILayout.BeginHorizontal();
                handScrollPos = GUILayout.BeginScrollView(handScrollPos, GUILayout.Height(cardH + 40f));
                GUILayout.BeginHorizontal();
                for (int i = 0; i < count; i++)
                {
                    var card = player.hand[i];
                    if (card == null) continue;

                    GUILayout.BeginVertical(GUILayout.Width(cardW + 20f));
                    if (card.art != null)
                    {
                        var content = new GUIContent(card.art.texture, card.description);
                        if (GUILayout.Button(content, GUILayout.Width(cardW), GUILayout.Height(cardH)))
                        {
                            placementUI.StartCard(card);
                        }

                    }
                    else
                    {
                        if (GUILayout.Button(card.title, GUILayout.Width(cardW), GUILayout.Height(cardH * 0.5f)))
                        {
                            placementUI.StartCard(card);
                        }
                    }
                    GUILayout.Label(card.title, smallLabelStyle, GUILayout.Width(cardW));
                    GUILayout.EndVertical();
                    GUILayout.Space(6f);
                }
                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("No cards available", smallLabelStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // Public-facing property: any modal is visible (transient info or pending defender decision)
        // Note: this is computed dynamically and exposed for other scripts to query.
        public bool IsModalActive => (transientTimer > 0f && !string.IsNullOrEmpty(transientTitle)) || (game != null && game.HasPendingProbe && placementUI != null && game.pendingProbe != null);

        public static bool AnyModalOpen => Instance != null && Instance.IsModalActive;



        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
