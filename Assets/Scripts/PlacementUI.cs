using System.Linq;
using UnityEngine;

namespace DarkForest
{
    public class PlacementUI : MonoBehaviour
    {
        public GameState game;
        public GameConfig config;
        public BodyCatalog catalog;
        public BoardRenderer probeView;
        public BoardRenderer hiddenView;

        public int layerForPlacement = 0;
        public int selectedIndex = 0;
        public BodyDefinition[] shop;

        private Vector2 shopScroll;
        private Vector2 handScroll;
        private bool awaitingTurnAdvance;
        private bool placingDuringTurn;
        private bool awaitingDefenderDecision;
        private GameBootstrapper bootstrapper;
        private PlayerCard pendingCard;
        private PlayerState cachedTurnPlayer;
        private int probesTakenThisTurn;

        void Start()
        {
            bootstrapper = FindObjectOfType<GameBootstrapper>();
            if (!catalog)
            {
                catalog = BodyFactory.BuildCatalog();
            }

            shop = catalog && catalog.entries != null ? catalog.entries.ToArray() : new BodyDefinition[0];
            EnsureRuntimeUpgrades();
            RefreshViews();
        }

        void EnsureRuntimeUpgrades()
        {
            if (shop == null) return;

            foreach (var def in shop)
            {
                UpgradeAlienArtifact(def);
                UpgradeAsteroidBelt(def);
                UpgradeStar(def);
            }
        }

        void UpgradeAlienArtifact(BodyDefinition def)
        {
            if (def == null || def.type != BodyType.AlienArtifact) return;

            bool hasAfterimagePlacement = def.placementPowers != null && def.placementPowers.Any(p => p is EraseProbePower);
            bool hasAfterimageColonization = def.colonizationPowers != null && def.colonizationPowers.Any(p => p is EraseProbePower);

            if (!hasAfterimagePlacement)
            {
                var power = ScriptableObject.CreateInstance<EraseProbePower>();
                power.name = "Afterimage";
                power.powerName = "Afterimage";
                power.text = "Erase intel from one of your probed cells.";
                power.timing = PowerTiming.DefensiveFaceDown;
                def.placementPowers = new Power[] { power };
            }
            else
            {
                foreach (var power in def.placementPowers)
                {
                    if (power is EraseProbePower)
                    {
                        power.name = "Afterimage";
                        power.powerName = "Afterimage";
                        power.text = "Erase intel from one of your probed cells.";
                        power.timing = PowerTiming.DefensiveFaceDown;
                    }
                }
            }

            if (!hasAfterimageColonization)
            {
                var power = ScriptableObject.CreateInstance<EraseProbePower>();
                power.name = "Afterimage";
                power.powerName = "Afterimage";
                power.text = "Erase intel from one of your probed cells.";
                power.timing = PowerTiming.DefensiveFaceDown;
                def.colonizationPowers = new Power[] { power };
            }
            else
            {
                foreach (var power in def.colonizationPowers)
                {
                    if (power is EraseProbePower)
                    {
                        power.name = "Afterimage";
                        power.powerName = "Afterimage";
                        power.text = "Erase intel from one of your probed cells.";
                        power.timing = PowerTiming.DefensiveFaceDown;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(def.rulesSummary) || def.rulesSummary.Contains("Experimental"))
            {
                def.rulesSummary = "Experimental tech. Cloaks your intel when threatened.";
            }
        }

        void UpgradeAsteroidBelt(BodyDefinition def)
        {
            if (def == null || def.type != BodyType.AsteroidBelt) return;

            bool hasSalvage = def.colonizationPowers != null && def.colonizationPowers.Any(p => p is GainCurrencyPower);

            if (!hasSalvage)
            {
                var power = ScriptableObject.CreateInstance<GainCurrencyPower>();
                power.name = "Salvage Bounty";
                power.powerName = "Salvage Bounty";
                power.text = "Gain 5 currency when this belt is fully colonized.";
                power.timing = PowerTiming.PassiveUpkeep;
                power.amount = 5;
                def.colonizationPowers = new Power[] { power };
            }
            else
            {
                foreach (var power in def.colonizationPowers)
                {
                    if (power is GainCurrencyPower gain)
                    {
                        power.name = "Salvage Bounty";
                        power.powerName = "Salvage Bounty";
                        power.text = "Gain 5 currency when this belt is fully colonized.";
                        gain.amount = 5;
                        power.timing = PowerTiming.PassiveUpkeep;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(def.rulesSummary) || def.rulesSummary.Contains("No special power"))
            {
                def.rulesSummary = "Sturdy debris field that pays out on survival.";
            }
        }

        void UpgradeStar(BodyDefinition def)
        {
            if (def == null || def.type != BodyType.Star) return;

            def.placementPowers = EnsureSolarDividend(def.placementPowers);
            def.colonizationPowers = EnsureSolarDividend(def.colonizationPowers);

            if (string.IsNullOrWhiteSpace(def.rulesSummary) || def.rulesSummary.Contains("every other turn"))
            {
                def.rulesSummary = "Generates currency each turn while active.";
            }
        }

        Power[] EnsureSolarDividend(Power[] powers)
        {
            bool hasDividend = false;

            if (powers != null)
            {
                for (int i = 0; i < powers.Length; i++)
                {
                    if (powers[i] is GainCurrencyPower gain)
                    {
                        ConfigureSolarDividend(gain);
                        hasDividend = true;
                    }
                }
            }

            if (hasDividend) return powers;

            var power = ScriptableObject.CreateInstance<GainCurrencyPower>();
            ConfigureSolarDividend(power);
            return AppendPower(powers, power);
        }

        void ConfigureSolarDividend(GainCurrencyPower power)
        {
            if (!power) return;

            power.name = "Solar Dividend";
            power.powerName = "Solar Dividend";
            power.text = "Gain 1 currency each turn while active.";
            power.timing = PowerTiming.PassiveUpkeep;
            power.amount = 1;
        }

        Power[] AppendPower(Power[] existing, Power addition)
        {
            if (addition == null) return existing;

            if (existing == null || existing.Length == 0)
            {
                return new[] { addition };
            }

            var result = new Power[existing.Length + 1];
            for (int i = 0; i < existing.Length; i++)
            {
                result[i] = existing[i];
            }

            result[existing.Length] = addition;
            return result;
        }

        void OnGUI()
        {
            if (!game || game.players.Count == 0) return;

            var active = game.Current;
            GUILayout.BeginArea(new Rect(10, 10, 380, 820), GUI.skin.box);
            GUILayout.Label($"Phase: {game.phase}");
            GUILayout.Label($"Current Player: {active?.team}");

            if (game.gameOver)
            {
                DrawGameOverUI();
                GUILayout.EndArea();
                return;
            }

            DrawLayerSelector();

            if (active != null)
            {
                GUILayout.Label($"Currency: {active.currency}");
                GUILayout.Label($"False Report Tokens: {active.falseReportTokens}");
            }

            GUILayout.Space(6f);
            DrawShop(active);
            GUILayout.Space(8f);

            if (game.phase == GameState.GamePhase.Placement)
            {
                DrawPlacementControls(active);
            }
            else
            {
                DrawProbeControls(active);
            }

            GUILayout.Space(8f);
            DrawHand(active);

            if (pendingCard != null && pendingCard.grantedPower != null && pendingCard.grantedPower.power != null && pendingCard.grantedPower.power.RequiresTargetCell)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"Select a target for {pendingCard.title} (Layer {layerForPlacement})");
            }

            DrawPendingProbeDecision();

            GUILayout.EndArea();
        }

        void DrawGameOverUI()
        {
            GUILayout.Space(12f);
            string winnerName = game.winner != null ? game.winner.team.ToString() : "No one";
            GUILayout.Label("Game Over!");
            GUILayout.Label($"Winner: {winnerName}");
            GUILayout.Space(10f);
            if (GUILayout.Button("Restart Game"))
            {
                RestartGame();
            }
        }

        void DrawPlacementControls(PlayerState active)
        {
            GUILayout.Label("Click your board to place the selected body.");
            if (active != null && active.placementReady)
            {
                GUILayout.Label("Ready. Waiting for other players.");
            }
            else if (!awaitingDefenderDecision && GUILayout.Button("Finished Placement"))
            {
                awaitingTurnAdvance = false;
                placingDuringTurn = false;
                pendingCard = null;
                game.CompletePlacementForCurrentPlayer();
                RefreshViews();
            }

            if (!awaitingDefenderDecision && GUILayout.Button("Pass Device To Next Player"))
            {
                awaitingTurnAdvance = false;
                placingDuringTurn = false;
                pendingCard = null;
                game.AdvanceToNextPlacementPlayer();
                RefreshViews();
            }
        }

        void DrawProbeControls(PlayerState active)
        {
            GUILayout.Label("Click the opponent board to launch a probe.");
            RenderPlacementToggle(active);

            int probesPerTurn = GetProbesPerTurn(active);
            int probesRemaining = active != null ? Mathf.Max(0, probesPerTurn - probesTakenThisTurn) : 0;
            GUILayout.Label($"Probes remaining this turn: {probesRemaining} / {probesPerTurn}");

            bool canManualPass = active != null && !awaitingDefenderDecision && !game.gameOver;
            GUI.enabled = canManualPass;
            if (GUILayout.Button(probesRemaining > 0 ? "End Turn (pass remaining probes)" : "End Turn"))
            {
                if (canManualPass)
                {
                    probesTakenThisTurn = Mathf.Max(probesTakenThisTurn, probesPerTurn);
                    if (!TryAutoAdvanceAfterProbes())
                    {
                        RefreshViews();
                    }
                }
            }
            GUI.enabled = true;
        }

        void DrawLayerSelector()
        {
            if (!game || game.depth <= 1) return;

            int maxLayer = Mathf.Max(0, game.depth - 1);
            layerForPlacement = Mathf.Clamp(layerForPlacement, 0, maxLayer);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Layer: {layerForPlacement}");

            GUI.enabled = !awaitingDefenderDecision && layerForPlacement > 0;
            if (GUILayout.Button("Prev", GUILayout.Width(70f)))
            {
                SetLayer(layerForPlacement - 1);
            }
            GUI.enabled = !awaitingDefenderDecision && layerForPlacement < maxLayer;
            if (GUILayout.Button("Next", GUILayout.Width(70f)))
            {
                SetLayer(layerForPlacement + 1);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        void RenderPlacementToggle(PlayerState active)
        {
            bool canPlace = !awaitingTurnAdvance && !awaitingDefenderDecision && active != null && HasAffordableBody(active) && !game.gameOver && pendingCard == null;
            if (!canPlace && placingDuringTurn)
            {
                placingDuringTurn = false;
                RefreshViews();
            }

            GUI.enabled = canPlace;
            if (canPlace)
            {
                bool next = GUILayout.Toggle(placingDuringTurn, placingDuringTurn ? "Exit Placement Mode" : "Enter Placement Mode", "Button");
                if (next != placingDuringTurn)
                {
                    placingDuringTurn = next;
                    RefreshViews();
                }
            }
            else
            {
                GUILayout.Button("Placement Mode (need currency or pending action)");
            }
            GUI.enabled = true;
        }

        void DrawShop(PlayerState active)
        {
            if (shop == null || shop.Length == 0)
            {
                GUILayout.Label("No bodies available");
                return;
            }

            EnsureSelectedBodyUnlocked(active);

            shopScroll = GUILayout.BeginScrollView(shopScroll, GUILayout.Height(200f));
            for (int i = 0; i < shop.Length; i++)
            {
                var body = shop[i];
                if (body == null) continue;

                bool affordable = active != null && active.currency >= body.price;
                bool unlocked = IsBodyUnlockedForPurchase(active, body);
                string label = FormatShopEntry(body);
                if (!unlocked)
                {
                    label += " (requires a Star in play)";
                }

                GUI.enabled = affordable && unlocked && !awaitingDefenderDecision;
                bool isSelected = unlocked && selectedIndex == i;
                bool clicked = GUILayout.Toggle(isSelected, label, "Button");
                if (clicked && unlocked)
                {
                    selectedIndex = i;
                }
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
        }

        void DrawHand(PlayerState active)
        {
            GUILayout.Label("Hand");
            if (active == null || active.hand.Count == 0)
            {
                GUILayout.Label("No cards available");
                return;
            }

            handScroll = GUILayout.BeginScrollView(handScroll, GUILayout.Height(240f));
            GUILayout.BeginHorizontal();

            foreach (var card in active.hand.ToList())
            {
                if (card == null) continue;

                bool canUse = !awaitingTurnAdvance && !placingDuringTurn && pendingCard == null && !awaitingDefenderDecision && card.IsAvailable;

                GUI.enabled = canUse;
                GUILayout.BeginVertical(GUILayout.Width(140f));
                if (card.art)
                {
                    var content = new GUIContent(card.art.texture, card.description);
                    if (GUILayout.Button(content, GUILayout.Width(140f), GUILayout.Height(200f)))
                    {
                        StartCard(card);
                    }
                }
                else
                {
                    if (GUILayout.Button(card.title, GUILayout.Width(140f), GUILayout.Height(60f)))
                    {
                        StartCard(card);
                    }
                }
                GUI.enabled = true;

                GUILayout.Label(card.title, GUILayout.Width(140f));
                if (!string.IsNullOrWhiteSpace(card.description))
                {
                    GUILayout.Label(card.description, GUILayout.Width(140f));
                }

                if (card.IsReusable && card.grantedPower != null && card.grantedPower.consumed)
                {
                    GUILayout.Label("(Waiting next turn)", GUILayout.Width(140f));
                }

                GUILayout.EndVertical();
                GUILayout.Space(6f);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        void DrawPendingProbeDecision()
        {
            if (!awaitingDefenderDecision || !game.HasPendingProbe) return;

            var pending = game.pendingProbe;
            GUILayout.Space(10f);
            GUILayout.Label("Defender Decision Required");
            GUILayout.Label($"Targeted Cell: ({pending.x}, {pending.y}, {pending.z})");
            GUILayout.Label("Spend a False Report token to convert this hit to a miss?");

            GUILayout.BeginHorizontal();
            bool hasToken = pending.defender.falseReportTokens > 0;
            GUI.enabled = hasToken;
            if (GUILayout.Button("Spend Token"))
            {
                ResolvePendingProbe(true);
            }
            GUI.enabled = true;
            if (GUILayout.Button("Allow Hit"))
            {
                ResolvePendingProbe(false);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Hand the device to the defender for this choice.");
        }

        string FormatShopEntry(BodyDefinition body)
        {
            if (body == null) return string.Empty;

            string summary = !string.IsNullOrWhiteSpace(body.rulesSummary) ? body.rulesSummary : string.Empty;
            string placement = DescribePowers(body.placementPowers, "On place");
            string colonization = DescribePowers(body.colonizationPowers, "On colonize");

            string extras = string.Join("  |  ", new[] { summary, placement, colonization }.Where(s => !string.IsNullOrEmpty(s)));

            return string.IsNullOrEmpty(extras)
                ? $"{body.displayName}  (cap {body.populationCapacity})  ${body.price}"
                : $"{body.displayName}  (cap {body.populationCapacity})  ${body.price} - {extras}";
        }

        string DescribePowers(Power[] powers, string prefix)
        {
            if (powers == null || powers.Length == 0) return string.Empty;

            var parts = powers
                .Where(p => p != null)
                .Select(p =>
                {
                    string name = !string.IsNullOrWhiteSpace(p.powerName) ? p.powerName : p.name;
                    if (string.IsNullOrWhiteSpace(name)) return null;

                    string description = !string.IsNullOrWhiteSpace(p.text) ? p.text.Trim() : string.Empty;
                    return string.IsNullOrEmpty(description) ? name : $"{name}: {description}";
                })
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .ToList();

            if (parts.Count == 0) return string.Empty;

            string joined = string.Join("; ", parts);
            return string.IsNullOrEmpty(prefix) ? joined : $"{prefix}: {joined}";
        }

        void Update()
        {
            if (!game || game.players.Count == 0 || !Camera.main || game.gameOver)
            {
                return;
            }

            UpdateBoardHover();

            if (awaitingDefenderDecision)
            {
                hiddenView?.ClearPlacementPreview();
                probeView?.ClearPlacementPreview();
                return;
            }

            if (pendingCard != null)
            {
                var power = pendingCard.grantedPower?.power;
                if (power == null)
                {
                    pendingCard = null;
                    RefreshViews();
                    return;
                }

                if (!power.RequiresTargetCell)
                {
                    ExecutePendingCard(Vector3Int.zero);
                    return;
                }

                UpdatePlacementPreviewForCard(power);

                if (Input.GetMouseButtonDown(0))
                {
                    HandleCardClick(power);
                }
                return;
            }

            bool isPlacementPhase = game.phase == GameState.GamePhase.Placement;
            if (isPlacementPhase)
            {
                UpdatePlacementPreviewCurrentBody();
            }
            else
            {
                hiddenView?.ClearPlacementPreview();
            }

            if (awaitingTurnAdvance && !isPlacementPhase)
            {
                var active = game.Current;
                int probesPerTurn = GetProbesPerTurn(active);
                if (active == null || probesTakenThisTurn >= probesPerTurn)
                {
                    return;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                var activeView = (isPlacementPhase || placingDuringTurn) ? hiddenView : probeView;
                if (TryGetBoardCoordinates(activeView, out int ox, out int oy))
                {
                    if (isPlacementPhase || placingDuringTurn)
                    {
                        TryPlaceAt(ox, oy, layerForPlacement);
                    }
                    else
                    {
                        LaunchProbe(ox, oy, layerForPlacement);
                    }
                }
            }
        }

        void UpdateBoardHover()
        {
            if (!game)
            {
                return;
            }

            BoardRenderer hoverView = null;
            var power = pendingCard?.grantedPower?.power;
            if (awaitingDefenderDecision)
            {
                hoverView = null;
            }
            else if (power != null && power.RequiresTargetCell)
            {
                hoverView = power.timing == PowerTiming.OffensiveFaceUp ? probeView : hiddenView;
            }
            else
            {
                bool isPlacementPhase = game.phase == GameState.GamePhase.Placement;
                hoverView = (isPlacementPhase || placingDuringTurn) ? hiddenView : probeView;
            }

            if (hoverView != null && TryGetBoardCoordinates(hoverView, out int hx, out int hy))
            {
                hoverView.SetHover(hx, hy);
            }
            else
            {
                hoverView?.SetHover(null, null);
            }

            if (hoverView != hiddenView && hiddenView != null)
            {
                hiddenView.SetHover(null, null);
            }
            if (hoverView != probeView && probeView != null)
            {
                probeView.SetHover(null, null);
            }
        }

        void UpdatePlacementPreviewCurrentBody()
        {
            if (hiddenView == null)
            {
                return;
            }

            if (TryGetBoardCoordinates(hiddenView, out int x, out int y))
            {
                var body = GetSelectedBody();
                if (body != null)
                {
                    hiddenView.ShowPlacementPreview(body, x, y);
                }
                else
                {
                    hiddenView.ClearPlacementPreview();
                }
            }
            else
            {
                hiddenView.ClearPlacementPreview();
            }
        }

        void UpdatePlacementPreviewForCard(Power power)
        {
            hiddenView?.ClearPlacementPreview();
            probeView?.ClearPlacementPreview();

            if (power == null)
            {
                return;
            }

            if (power is ScanRowPower)
            {
                var targetView = power.timing == PowerTiming.OffensiveFaceUp ? probeView : hiddenView;
                if (TryGetBoardCoordinates(targetView, out int x, out int y))
                {
                    targetView.ShowRowPreview(y);
                }
            }
            else if (power is ScanColumnPower columnPower)
            {
                var targetView = power.timing == PowerTiming.OffensiveFaceUp ? probeView : hiddenView;
                if (TryGetBoardCoordinates(targetView, out int x, out int y))
                {
                    if (columnPower.halfRange)
                    {
                        int span = Mathf.Max(3, game.config.height / 2);
                        int startY = Mathf.Max(0, y - span / 2);
                        int endY = Mathf.Min(game.config.height - 1, y + span / 2);
                        targetView.ShowHalfColumnPreview(x, startY, endY);
                    }
                    else
                    {
                        targetView.ShowColumnPreview(x);
                    }
                }
            }
        }

        BodyDefinition GetSelectedBody()
        {
            if (shop == null || shop.Length == 0) return null;

            var active = game != null ? game.Current : null;
            EnsureSelectedBodyUnlocked(active);

            selectedIndex = Mathf.Clamp(selectedIndex, 0, shop.Length - 1);
            var body = shop[selectedIndex];
            return IsBodyUnlockedForPurchase(active, body) ? body : null;
        }

        int GetProbesPerTurn(PlayerState player)
        {
            return game != null && player != null ? game.GetProbesPerTurn(player) : 1;
        }

        bool TryGetBoardCoordinates(BoardRenderer view, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (!view) return false;

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return false;

            var hitTransform = hit.collider ? hit.collider.transform : hit.transform;
            if (hitTransform == null) return false;
            if (hitTransform != view.transform && !hitTransform.IsChildOf(view.transform)) return false;

            var local = view.transform.InverseTransformPoint(hit.point);
            int ox = Mathf.FloorToInt(local.x / view.cellSize + 0.5f);
            int oy = Mathf.FloorToInt(local.y / view.cellSize + 0.5f);
            x = ox;
            y = oy;
            return true;
        }

        void HandleCardClick(Power power)
        {
            if (pendingCard == null || power == null) return;

            BoardRenderer targetView = power.timing == PowerTiming.OffensiveFaceUp ? probeView : hiddenView;
            if (!TryGetBoardCoordinates(targetView, out int x, out int y)) return;

            ExecutePendingCard(new Vector3Int(x, y, layerForPlacement));
        }

        void StartCard(PlayerCard card)
        {
            hiddenView?.ClearPlacementPreview();
            probeView?.ClearPlacementPreview();

            if (card == null || card.grantedPower == null || card.grantedPower.power == null) return;
            if (!card.IsAvailable) return;

            pendingCard = card;
            if (!card.grantedPower.power.RequiresTargetCell)
            {
                ExecutePendingCard(Vector3Int.zero);
            }
            else
            {
                RefreshViews();
            }
        }

        void ExecutePendingCard(Vector3Int probe)
        {
            hiddenView?.ClearPlacementPreview();
            probeView?.ClearPlacementPreview();

            if (pendingCard == null || pendingCard.grantedPower == null || pendingCard.grantedPower.power == null)
            {
                pendingCard = null;
                RefreshViews();
                return;
            }

            var card = pendingCard;
            pendingCard = null;

            var owner = game.Current;
            var opponent = game.GetOpponent(owner);
            var power = card.grantedPower.power;

            var context = new PowerContext
            {
                game = game,
                self = owner,
                target = power.timing == PowerTiming.OffensiveFaceUp ? opponent : owner,
                sourceBody = card.grantedPower.sourceBody,
                probe = probe,
                rng = game.rng
            };

            bool success = power.TryPlay(context);
            if (success)
            {
                if (card.IsReusable)
                {
                    card.grantedPower.consumed = true;
                }
                else
                {
                    owner.hand.Remove(card);
                    owner.readyPowers.Remove(card.grantedPower);
                }

                Debug.Log($"{owner.team} played {card.title}.");
            }
            else
            {
                Debug.LogWarning($"{owner.team} failed to play {card.title}.");
            }

            RefreshViews();
        }

        void TryPlaceAt(int ox, int oy, int oz)
        {
            var me = game.Current;
            if (me == null) return;
            if (shop == null || shop.Length == 0) return;

            var def = GetSelectedBody();
            if (def == null)
            {
                Debug.LogWarning("No valid body is selected for placement.");
                return;
            }

            if (me.currency < def.price)
            {
                Debug.Log("Not enough currency");
                return;
            }

            if (!ValidatePlacementPrerequisites(me, def, ox, oy, oz))
            {
                return;
            }

            if (me.hiddenBoard.CanPlace(def.shape, ox, oy, oz))
            {
                var inst = me.hiddenBoard.Place(def, me.team, ox, oy, oz);
                me.bodies.Add(inst);
                me.currency -= def.price;
                Debug.Log($"{me.team} placed {def.displayName} for ${def.price}.");

                game.HandlePlacement(me, inst);

                if (!HasAffordableBody(me))
                {
                    placingDuringTurn = false;
                }
                RefreshViews();
            }
            else
            {
                hiddenView?.ShowPlacementPreview(def, ox, oy);
                Debug.Log("Cannot place here: blocked, out of bounds, or probed.");
            }
        }

        void LaunchProbe(int x, int y, int z)
        {
            var attacker = game.Current;
            var defender = game.GetOpponent(attacker);
            if (attacker == null || defender == null) return;

            var outcome = game.LaunchProbe(attacker, defender, x, y, z);

            bool counted = outcome.isHit || outcome.isMiss || outcome.isNear || outcome.wasFalseReport || outcome.isPending;
            if (counted)
            {
                probesTakenThisTurn++;
                int maxProbes = GetProbesPerTurn(attacker);
                if (maxProbes > 0 && probesTakenThisTurn > maxProbes)
                {
                    probesTakenThisTurn = maxProbes;
                }
            }

            if (outcome.isPending || game.HasPendingProbe)
            {
                awaitingDefenderDecision = true;
                awaitingTurnAdvance = false;
                placingDuringTurn = false;
                Debug.Log($"{attacker.team} probes ({x},{y},{z}) -> awaiting defender decision");
                RefreshViews();
                return;
            }

            if (!counted)
            {
                Debug.LogWarning($"{attacker.team} probe at ({x},{y},{z}) was invalid.");
                awaitingTurnAdvance = false;
                RefreshViews();
                return;
            }

            string resultText = outcome.wasFalseReport
                ? "FALSE REPORT"
                : outcome.isHit ? "HIT" : outcome.isNear ? "NEAR" : outcome.isMiss ? "MISS" : "INVALID";

            Debug.Log($"{attacker.team} probes ({x},{y},{z}) -> {resultText}");

            placingDuringTurn = false;
            if (TryAutoAdvanceAfterProbes())
            {
                return;
            }

            awaitingTurnAdvance = false;
            RefreshViews();
        }

        void ResolvePendingProbe(bool spendToken)
        {
            var outcome = game.ResolvePendingProbe(spendToken);

            string resultText = outcome.wasFalseReport
                ? "FALSE REPORT"
                : outcome.isHit ? "HIT" : outcome.isNear ? "NEAR" : outcome.isMiss ? "MISS" : "INVALID";

            Debug.Log($"Defender resolves probe -> {resultText}");

            awaitingDefenderDecision = false;
            placingDuringTurn = false;

            if (TryAutoAdvanceAfterProbes())
            {
                return;
            }

            awaitingTurnAdvance = false;
            RefreshViews();
        }

        void RefreshViews()
        {
            if (!game) return;

            int maxLayer = Mathf.Max(0, game.depth - 1);
            layerForPlacement = Mathf.Clamp(layerForPlacement, 0, maxLayer);

            if (hiddenView)
            {
                hiddenView.layerToShow = layerForPlacement;
            }
            if (probeView)
            {
                probeView.layerToShow = layerForPlacement;
            }

            bool isPlacementPhase = game.phase == GameState.GamePhase.Placement;
            if (isPlacementPhase)
            {
                placingDuringTurn = false;
                awaitingTurnAdvance = false;
                probesTakenThisTurn = 0;
            }

            var active = game.Current;
            if (cachedTurnPlayer != active)
            {
                cachedTurnPlayer = active;
                probesTakenThisTurn = 0;
                if (!awaitingDefenderDecision)
                {
                    awaitingTurnAdvance = false;
                }
            }
            var defender = game.GetOpponent(active);

            int probesPerTurn = GetProbesPerTurn(active);
            bool hasProbeAvailable = active != null && probesTakenThisTurn < probesPerTurn;

            bool allowPlacementClicks = (isPlacementPhase || placingDuringTurn || (pendingCard != null && pendingCard.grantedPower != null && pendingCard.grantedPower.power != null && pendingCard.grantedPower.power.timing == PowerTiming.DefensiveFaceDown))
                                        && active != null && !game.gameOver && !awaitingDefenderDecision;
            bool allowProbeClicks = ((!isPlacementPhase && !placingDuringTurn && active != null && defender != null && !game.gameOver && !awaitingDefenderDecision && hasProbeAvailable)
                                    || (pendingCard != null && pendingCard.grantedPower != null && pendingCard.grantedPower.power != null && pendingCard.grantedPower.power.timing == PowerTiming.OffensiveFaceUp));

            if (hiddenView)
            {
                if (active != null)
                {
                    hiddenView.revealOccupants = true;
                    hiddenView.RenderHidden(active, allowPlacementClicks);
                }
                else
                {
                    hiddenView.Clear();
                }
            }

            if (probeView)
            {
                if (active != null && defender != null)
                {
                    probeView.revealOccupants = false;
                    probeView.RenderCentral(active, defender, allowProbeClicks);
                }
                else
                {
                    probeView.Clear();
                }
            }
        }

        void RestartGame()
        {
            pendingCard = null;
            placingDuringTurn = false;
            awaitingTurnAdvance = false;
            awaitingDefenderDecision = false;
            cachedTurnPlayer = null;
            probesTakenThisTurn = 0;
            layerForPlacement = 0;
            game.RestartCurrentConfig();
            shop = catalog && catalog.entries != null ? catalog.entries.ToArray() : new BodyDefinition[0];
            EnsureRuntimeUpgrades();
            RefreshViews();
            bootstrapper?.RefreshRenderers();
        }

        void SetLayer(int newLayer)
        {
            if (!game)
            {
                layerForPlacement = newLayer;
                return;
            }

            layerForPlacement = Mathf.Clamp(newLayer, 0, Mathf.Max(0, game.depth - 1));
            RefreshViews();
        }

        void EnsureSelectedBodyUnlocked(PlayerState player)
        {
            if (shop == null || shop.Length == 0)
            {
                selectedIndex = 0;
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, shop.Length - 1);
            var current = shop[selectedIndex];
            if (current == null || !IsBodyUnlockedForPurchase(player, current))
            {
                selectedIndex = FindFirstUnlockedBodyIndex(player);
            }
        }

        int FindFirstUnlockedBodyIndex(PlayerState player)
        {
            if (shop == null || shop.Length == 0) return 0;

            for (int i = 0; i < shop.Length; i++)
            {
                var candidate = shop[i];
                if (candidate != null && IsBodyUnlockedForPurchase(player, candidate))
                {
                    return i;
                }
            }

            for (int i = 0; i < shop.Length; i++)
            {
                if (shop[i] != null)
                {
                    return i;
                }
            }

            return Mathf.Clamp(selectedIndex, 0, shop.Length - 1);
        }

        bool IsBodyUnlockedForPurchase(PlayerState player, BodyDefinition body)
        {
            if (body == null) return false;
            if (body.type == BodyType.DysonSphere)
            {
                return PlayerHasBodyOfType(player, BodyType.Star);
            }
            return true;
        }

        bool PlayerHasBodyOfType(PlayerState player, BodyType type)
        {
            return player != null && player.bodies.Any(b => b != null && b.Definition != null && b.Definition.type == type);
        }

        bool DysonPlacementEncirclesStar(PlayerState player, int ox, int oy, int oz)
        {
            if (player == null) return false;

            foreach (var body in player.bodies)
            {
                if (body?.Definition == null || body.Definition.type != BodyType.Star) continue;
                if (body.OccupiedCells == null || body.OccupiedCells.Count == 0) continue;

                bool sameLayer = body.OccupiedCells.All(c => c != null && c.z == oz);
                if (!sameLayer) continue;

                bool inside = true;
                foreach (var cell in body.OccupiedCells)
                {
                    int dx = cell.x - ox;
                    int dy = cell.y - oy;
                    if (Mathf.Abs(dx) > 2 || Mathf.Abs(dy) > 2)
                    {
                        inside = false;
                        break;
                    }
                }

                if (inside)
                {
                    return true;
                }
            }

            return false;
        }

        bool ValidatePlacementPrerequisites(PlayerState player, BodyDefinition def, int ox, int oy, int oz)
        {
            if (player == null || def == null) return false;

            if (def.type == BodyType.DysonSphere)
            {
                if (!PlayerHasBodyOfType(player, BodyType.Star))
                {
                    Debug.Log("Dyson Sphere requires a Star already in play.");
                    return false;
                }

                if (!DysonPlacementEncirclesStar(player, ox, oy, oz))
                {
                    hiddenView?.ShowPlacementPreview(def, ox, oy);
                    Debug.Log("Dyson Sphere must be placed to encircle one of your Stars.");
                    return false;
                }
            }

            return true;
        }

        bool TryAutoAdvanceAfterProbes()
        {
            if (!game || game.gameOver) return false;
            if (game.phase != GameState.GamePhase.Probing) return false;
            if (awaitingDefenderDecision || game.HasPendingProbe) return false;

            var active = game.Current;
            if (active == null) return false;

            int probesPerTurn = Mathf.Max(0, GetProbesPerTurn(active));
            if (probesPerTurn > 0 && probesTakenThisTurn < probesPerTurn)
            {
                awaitingTurnAdvance = false;
                return false;
            }

            AdvanceToNextProbePlayer();
            return true;
        }

        void AdvanceToNextProbePlayer()
        {
            if (!game || game.gameOver) return;
            if (game.phase != GameState.GamePhase.Probing) return;

            awaitingTurnAdvance = false;
            placingDuringTurn = false;
            pendingCard = null;

            cachedTurnPlayer = null;
            probesTakenThisTurn = 0;

            game.AdvanceAfterProbe();
            RefreshViews();
        }

        bool HasAffordableBody(PlayerState player)
        {
            return shop != null && player != null && shop.Any(body => body != null && IsBodyUnlockedForPurchase(player, body) && player.currency >= body.price);
        }
    }
}


