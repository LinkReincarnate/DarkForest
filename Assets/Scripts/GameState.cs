using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DarkForest
{
    public class GrantedPower
    {
        public Power power;
        public BodyInstance sourceBody;
        public bool consumed;
    }

    public class PendingProbeData
    {
        public PlayerState attacker;
        public PlayerState defender;
        public int x;
        public int y;
        public int z;
    }

    [System.Serializable]
    public class PlayerCard
    {
        public GrantedPower grantedPower;
        public Sprite art;
        public BodyType sourceType;
        public string title;
        public string description;

        public bool IsFaceDown => grantedPower != null && grantedPower.power && grantedPower.power.timing == PowerTiming.DefensiveFaceDown;
        public bool IsReusable => grantedPower != null && grantedPower.power && grantedPower.power.timing == PowerTiming.PassiveUpkeep;
        public bool IsAvailable => grantedPower != null && grantedPower.power && (!IsReusable || !grantedPower.consumed);
    }

    public class PlayerState
    {
        public Team team;
        public Board hiddenBoard;
        public int currency;
        public List<BodyInstance> bodies = new List<BodyInstance>();
        public int falseReportTokens = 0;
        public bool placementReady = false;
        public int preferredTargetIndex = -1;
        public List<GrantedPower> readyPowers = new List<GrantedPower>();
        public List<PlayerCard> hand = new List<PlayerCard>();

        public bool HasPlacedBodies => bodies.Count > 0;
        public bool IsEliminated => bodies.Count > 0 && bodies.TrueForAll(b => b.IsFullyColonized);
    }

    public class GameState : MonoBehaviour
    {
        public enum GamePhase { Placement, Probing }

        public GameConfig config;
        public BodyCatalog catalog;
        public CardArtCatalog cardArtCatalog;

        public int playerCount = 2;
        public int depth = 1;
        public List<PlayerState> players = new List<PlayerState>();
        public int currentIndex = 0;
        public GamePhase phase = GamePhase.Placement;
        public System.Random rng = new System.Random();
    [Tooltip("Seconds to wait after a probe before advancing to the next player. Set to 0 for immediate advance.")]
    public float postProbeDelay = 1.0f;
        public bool gameOver = false;
        public PlayerState winner;
        public PendingProbeData pendingProbe;
        private class SensorLogSubscription
        {
            public PlayerState owner;
            public BodyInstance station;
            public int initialHitCount;
        }

        private readonly List<SensorLogSubscription> activeSensorLoggers = new List<SensorLogSubscription>();
    private Coroutine scheduledAdvanceCoroutine = null;
    // Fired when the current player is advanced (after any delay).
    public System.Action OnPlayerAdvanced;


        public bool HasPendingProbe => pendingProbe != null;
        public int GetProbesPerTurn(PlayerState player)
        {
            return HasPlanetDoubleProbe(player) ? 2 : 1;
        }

        public bool HasPlanetDoubleProbe(PlayerState player)
        {
            if (player == null) return false;

            foreach (var body in player.bodies)
            {
                if (body?.Definition == null) continue;
                if (body.Definition.type != BodyType.Planet) continue;

                int occupied = body.OccupiedCells != null ? body.OccupiedCells.Count : 0;
                if (occupied == 0) continue;

                if (body.HitCount <= 0 || body.IsFullyColonized)
                {
                    return true;
                }
            }

            return false;
        }


        public void NewGame(int playersCount, int depthLevels)
        {
            int maxPlayers = System.Enum.GetValues(typeof(Team)).Length;
            int safeMaxPlayers = Mathf.Max(2, maxPlayers);
            playerCount = Mathf.Clamp(playersCount, 2, safeMaxPlayers);
            if (playerCount != playersCount)
            {
                Debug.LogWarning("[GameState] Requested {playersCount} players but clamped to {playerCount} (max supported).");
            }

            depth = depthLevels;
            players.Clear();

            for (int i = 0; i < playerCount; i++)
            {
                var ps = new PlayerState
                {
                    team = (Team)i,
                    hiddenBoard = new Board(config.width, config.height, depthLevels),
                    currency = config.GetStartingCurrency(playerCount, depthLevels),
                    placementReady = false,
                    preferredTargetIndex = -1
                };
                players.Add(ps);
            }

            currentIndex = 0;
            phase = GamePhase.Placement;
            gameOver = false;
            winner = null;
            pendingProbe = null;
        }

        public void RestartCurrentConfig()
        {
            NewGame(playerCount, depth);
        }

        public PlayerState Current => players.Count > 0 ? players[Mathf.Clamp(currentIndex, 0, players.Count - 1)] : null;

        public PlayerState GetOpponent(PlayerState player)
        {
            if (player == null || players.Count == 0) return null;

            int startIndex = players.IndexOf(player);
            if (startIndex < 0)
            {
                startIndex = Mathf.Clamp(currentIndex, 0, players.Count - 1);
            }

            for (int offset = 1; offset <= players.Count; offset++)
            {
                int idx = (startIndex + offset) % players.Count;
                var candidate = players[idx];
                if (candidate != null && candidate != player && !candidate.IsEliminated)
                {
                    return candidate;
                }
            }

            var fallback = players.FirstOrDefault(p => p != null && p != player && !p.IsEliminated)
                         ?? players.FirstOrDefault(p => p != null && p != player);
            return fallback ?? player;
        }

        public void CompletePlacementForCurrentPlayer()
        {
            if (phase != GamePhase.Placement || gameOver) return;

            var current = Current;
            if (current == null) return;

            current.placementReady = true;
            if (players.TrueForAll(p => p.placementReady))
            {
                phase = GamePhase.Probing;
                currentIndex = 0;
                ResetPassivePowers(Current);
            }
            else
            {
                AdvanceToNextPlacementPlayer();
            }
        }

        public void AdvanceToNextPlacementPlayer()
        {
            if (phase != GamePhase.Placement || gameOver) return;

            for (int i = 1; i <= players.Count; i++)
            {
                int idx = (currentIndex + i) % players.Count;
                if (!players[idx].placementReady)
                {
                    currentIndex = idx;
                    ResetPassivePowers(players[idx]);
                    return;
                }
            }
        }

        public void AdvanceAfterProbe()
        {
            if (phase != GamePhase.Probing || players.Count == 0 || gameOver || HasPendingProbe) return;

            // If a post-probe delay is configured, start a coroutine to advance after the delay so the player
            // can see the result of their probe. If delay is zero or negative, advance immediately.
            if (postProbeDelay > 0f)
            {
                if (scheduledAdvanceCoroutine != null) return; // already scheduled
                scheduledAdvanceCoroutine = StartCoroutine(AdvanceAfterProbeWithDelay(postProbeDelay));
                return;
            }

            for (int i = 1; i <= players.Count; i++)
            {
                int idx = (currentIndex + i) % players.Count;
                if (!players[idx].IsEliminated)
                {
                    currentIndex = idx;
                    ResetPassivePowers(players[idx]);
                    OnPlayerAdvanced?.Invoke();
                    return;
                }
            }
        }

        System.Collections.IEnumerator AdvanceAfterProbeWithDelay(float delay)
        {
            // Wait for the UI/animation to show the probe result
            yield return new WaitForSeconds(delay);

            // Double-check that advancing is still appropriate (no pending probe, not game over)
            if (phase != GamePhase.Probing || players.Count == 0 || gameOver || HasPendingProbe)
            {
                scheduledAdvanceCoroutine = null;
                yield break;
            }

            for (int i = 1; i <= players.Count; i++)
            {
                int idx = (currentIndex + i) % players.Count;
                if (!players[idx].IsEliminated)
                {
                    currentIndex = idx;
                    ResetPassivePowers(players[idx]);
                    scheduledAdvanceCoroutine = null;
                    OnPlayerAdvanced?.Invoke();
                    yield break;
                }
            }
            scheduledAdvanceCoroutine = null;
        }

        public void ActivateSpaceStationLogging(PlayerState owner, BodyInstance station)
        {
            if (owner == null || station == null) return;
            if (station.Definition != null && station.Definition.type != BodyType.SpaceStation) return;

            activeSensorLoggers.RemoveAll(s => s.owner == owner && s.station == station);

            activeSensorLoggers.Add(new SensorLogSubscription
            {
                owner = owner,
                station = station,
                initialHitCount = station.HitCount
            });

            var stationName = station.Definition != null ? station.Definition.displayName : "Space Station";
            Debug.Log($"[Sensor Burst] {owner.team} sensors online. Tracking hits until {stationName} is partially colonized.");
        }

        void ProcessHitLoggers(PlayerState attacker, PlayerState defender, BodyInstance hitBody)
        {
            if (activeSensorLoggers.Count == 0) return;

            for (int i = activeSensorLoggers.Count - 1; i >= 0; i--)
            {
                var subscription = activeSensorLoggers[i];
                if (subscription.owner == null || subscription.station == null)
                {
                    activeSensorLoggers.RemoveAt(i);
                    continue;
                }

                if (subscription.station.HitCount > subscription.initialHitCount)
                {
                    var stationName = subscription.station.Definition != null ? subscription.station.Definition.displayName : "Space Station";
                    Debug.Log($"[Sensor Burst] {stationName} has been partially colonized. Sensors offline for {subscription.owner.team}.");

                    // If this station has become fully colonized, remove the visual card
                    // that the placing player received when they placed the station.
                    try
                    {
                        if (subscription.station.IsFullyColonized)
                        {
                            var placer = players.FirstOrDefault(p => p != null && p.team == subscription.station.Owner);
                            if (placer != null)
                            {
                                int removed = placer.hand.RemoveAll(card => card != null && card.sourceType == BodyType.SpaceStation && card.title == subscription.station.Definition.displayName && card.grantedPower == null);
                                if (removed > 0)
                                {
                                    Debug.Log($"[Sensor Burst] Removed {removed} Space Station card(s) from {placer.team}'s hand due to full colonization of {subscription.station.Definition.displayName}.");
                                }
                            }
                        }
                    }
                    catch { }

                    activeSensorLoggers.RemoveAt(i);
                }
            }

            if (attacker == null || hitBody == null || hitBody.Definition == null) return;

            foreach (var subscription in activeSensorLoggers)
            {
                if (subscription.owner != attacker) continue;

                var targetName = hitBody.Definition.displayName;
                Debug.Log($"[Sensor Burst] {attacker.team} detected a hit on {targetName}.");
            }
        }
        public ProbeOutcome LaunchProbe(PlayerState attacker, PlayerState defender, int x, int y, int z)
        {
            if (HasPendingProbe) return ProbeOutcome.Pending();
            if (gameOver || defender == null) return ProbeOutcome.OutOfBounds;

            var cell = defender.hiddenBoard.Get(x, y, z);
            if (cell == null) return ProbeOutcome.OutOfBounds;

            if (cell.occupant != null && defender.falseReportTokens > 0)
            {
                pendingProbe = new PendingProbeData
                {
                    attacker = attacker,
                    defender = defender,
                    x = x,
                    y = y,
                    z = z
                };
                return ProbeOutcome.Pending();
            }

            var outcome = defender.hiddenBoard.Probe(x, y, z);
            if (outcome.isHit && outcome.hitBody != null)
            {
                HandleHit(attacker, defender, outcome.hitBody);
            }

            if (!gameOver && defender.IsEliminated)
            {
                EvaluateVictory(attacker);
            }

            return outcome;
        }

        public ProbeOutcome ResolvePendingProbe(bool spendToken)
        {
            if (!HasPendingProbe) return ProbeOutcome.OutOfBounds;

            var data = pendingProbe;
            pendingProbe = null;

            var defender = data.defender;
            var attacker = data.attacker;
            var cell = defender.hiddenBoard.Get(data.x, data.y, data.z);

            if (spendToken && defender.falseReportTokens > 0 && cell != null && cell.occupant != null)
            {
                defender.falseReportTokens--;
                // When a false report token is spent, also consume and remove one corresponding FalseReport granted power/card if present
                var consumed = defender.readyPowers.FirstOrDefault(gp => gp != null && gp.power is FalseReportPower && !gp.consumed);
                if (consumed != null)
                {
                    consumed.consumed = true;
                    // remove the card associated with this granted power
                    defender.hand.RemoveAll(card => card != null && card.grantedPower == consumed);
                }
                cell.probed = true;
                cell.lastReport = CellReport.Miss;
                return ProbeOutcome.FalseReport();
            }

            ProbeOutcome outcome = ProbeOutcome.OutOfBounds;
            if (cell != null)
            {
                outcome = defender.hiddenBoard.Probe(data.x, data.y, data.z);
                if (outcome.isHit && outcome.hitBody != null)
                {
                    HandleHit(attacker, defender, outcome.hitBody);
                }

                if (!gameOver && defender.IsEliminated)
                {
                    EvaluateVictory(attacker);
                }
            }

            return outcome;
        }

        void HandleHit(PlayerState attacker, PlayerState defender, BodyInstance body)
        {
            if (body != null)
            {
                ProcessHitLoggers(attacker, defender, body);
            }

            if (body?.Definition == null) return;

            if (body.Definition.colonizationPowers != null && body.Definition.colonizationPowers.Length > 0 &&
                body.IsFullyColonized && !body.ColonizationPowersGranted)
            {
                GrantPowers(attacker, body.Definition.colonizationPowers, body);
                body.ColonizationPowersGranted = true;
                // If GrantPowers somehow didn't add any granted powers for this body (edge cases when prototypes are null),
                // create the expected granted power(s) for known body types so the colonizer receives a functional card.
                if (attacker != null)
                {
                    var existingForBody = attacker.readyPowers.FirstOrDefault(gp => gp != null && gp.sourceBody == body);
                            if (existingForBody == null)
                            {
                                // Try to use canonical prototypes from the game's catalog for this body type.
                                bool grantedFromCatalog = false;
                                try
                                {
                                    BodyDefinition canonical = null;
                                    if (this.catalog != null)
                                    {
                                        canonical = this.catalog.Get(body.Definition.type);
                                    }

                                    // If the assigned catalog doesn't contain the body, build a runtime catalog and try again
                                    if (canonical == null)
                                    {
                                        try
                                        {
                                            var runtimeCat = BodyFactory.BuildCatalog();
                                            if (runtimeCat != null)
                                            {
                                                canonical = runtimeCat.Get(body.Definition.type);
                                            }
                                        }
                                        catch { /* ignore build failures */ }
                                    }

                                    if (canonical != null && canonical.colonizationPowers != null && canonical.colonizationPowers.Length > 0)
                                    {
                                        GrantPowers(attacker, canonical.colonizationPowers, body);
                                        grantedFromCatalog = attacker.readyPowers.Any(gp => gp != null && gp.sourceBody == body);
                                    }
                                }
                                catch { /* ignore catalog lookup failures and continue to fallbacks */ }

                                if (!grantedFromCatalog)
                                {
                                    // Create fallback granted powers for Satellite, Rocket3, Rocket4
                                    GrantedPower fallback = null;
                                    if (body.Definition.type == BodyType.Satellite)
                                    {
                                        var p = ScriptableObject.CreateInstance<ScanRowPower>();
                                        p.name = "Scan Row";
                                        p.powerName = "Scan Row";
                                        p.text = "Reveal one entire row on the opponent board.";
                                        p.timing = PowerTiming.OffensiveFaceUp;
                                        fallback = new GrantedPower { power = p, sourceBody = body, consumed = false };
                                    }
                                    else if (body.Definition.type == BodyType.Rocket3)
                                    {
                                        var p = ScriptableObject.CreateInstance<ScanColumnPower>();
                                        p.name = "Scan Column";
                                        p.powerName = "Scan Column";
                                        p.text = "Reveal the entire column containing the selected cell.";
                                        p.timing = PowerTiming.OffensiveFaceUp;
                                        fallback = new GrantedPower { power = p, sourceBody = body, consumed = false };
                                    }
                                    else if (body.Definition.type == BodyType.Rocket4)
                                    {
                                        var p = ScriptableObject.CreateInstance<ScanColumnPower>();
                                        p.name = "Half Column Scan";
                                        p.powerName = "Half Column Scan";
                                        p.text = "Reveal a segment of the selected column.";
                                        p.timing = PowerTiming.OffensiveFaceUp;
                                        try { ((ScanColumnPower)p).halfRange = true; } catch { }
                                        fallback = new GrantedPower { power = p, sourceBody = body, consumed = false };
                                    }

                                    if (fallback != null)
                                    {
                                        attacker.readyPowers.Add(fallback);
                                        attacker.hand.Add(CreateCardForPower(fallback, body));
                                        Debug.Log($"[GameState] Fallback: granted '{fallback.power?.powerName}' to {attacker.team} for body '{body.Definition?.displayName}'.");
                                    }
                                }
                            }
                }
                // Diagnostic: report what granted powers and hand entries exist for attacker
                if (attacker != null)
                {
                    var gps = attacker.readyPowers.Where(gp => gp != null && gp.sourceBody == body).ToList();
                    Debug.Log($"[GameState] After GrantPowers: attacker.readyPowers has {gps.Count} entries for body '{(body?.Definition?.displayName ?? "<unknown>")}'.");
                    foreach (var gp in gps)
                    {
                        Debug.Log($"[GameState] ReadyPower: {(gp.power != null ? gp.power.powerName : "<null>")}, consumed={gp.consumed}");
                    }

                    var handCards = attacker.hand.Where(c => c != null).ToList();
                    Debug.Log($"[GameState] Attacker hand contains {handCards.Count} cards after colonization for body '{(body?.Definition?.displayName ?? "<unknown>")}'.");
                    foreach (var c in handCards)
                    {
                        Debug.Log($"[GameState] Hand card: title='{c.title}', hasGranted={(c.grantedPower!=null)}, grantedPowerName={(c.grantedPower!=null && c.grantedPower.power!=null?c.grantedPower.power.powerName:"<null>")}");
                    }
                }
                // Award a visual card for certain bodies to the colonizer (old UI behavior)
                if (attacker != null && body.Definition != null)
                {
                    var t = body.Definition.type;
                    if (t == BodyType.Satellite || t == BodyType.Rocket3 || t == BodyType.Rocket4)
                    {
                        // If a card representing the granted power for this body already exists, don't add a duplicate.
                        bool hasLinkedCard = attacker.hand.Any(c => c != null && c.grantedPower != null && c.grantedPower.sourceBody == body);
                        if (!hasLinkedCard)
                        {
                            // Try to find the granted power that was just added by GrantPowers and create a functional card for it.
                            var linkedGranted = attacker.readyPowers.FirstOrDefault(gp => gp != null && gp.sourceBody == body);
                            if (linkedGranted != null)
                            {
                                var powerCard = CreateCardForPower(linkedGranted, body);
                                attacker.hand.Add(powerCard);
                            }
                            else
                            {
                                // Fallback: add a visual body card (non-playable). This preserves the previous visual behavior.
                                var bodyCard = CreateCardForBody(body);
                                if (bodyCard != null)
                                {
                                    attacker.hand.Add(bodyCard);
                                }
                            }
                        }
                    }
                }
            }

            // If a Space Station has become fully colonized, ensure the visual card
            // that was added to the player who placed it (if any) is removed. This
            // is done here so removal happens even if the sensor subscription was
            // removed earlier (e.g., when partially colonized).
            try
            {
                if (body.Definition.type == BodyType.SpaceStation && body.IsFullyColonized)
                {
                    var placer = players.FirstOrDefault(p => p != null && p.team == body.Owner);
                    if (placer != null)
                    {
                        int removed = placer.hand.RemoveAll(card => card != null && card.sourceType == BodyType.SpaceStation && card.title == body.Definition.displayName && card.grantedPower == null);
                        if (removed > 0)
                        {
                            Debug.Log($"[GameState] Removed {removed} placed Space Station card(s) from {placer.team}'s hand due to full colonization of {body.Definition.displayName}.");
                        }
                    }
                }
            }
            catch { }

            if (body.Definition.type == BodyType.Star && body.IsFullyColonized)
            {
                RevokeSolarDividend(defender, body);
            }
        }

        public void HandlePlacement(PlayerState owner, BodyInstance instance)
        {
            if (instance == null || instance.Definition == null || instance.PlacementPowersGranted) return;

            if (instance.Definition.placementPowers != null && instance.Definition.placementPowers.Length > 0)
            {
                GrantPowers(owner, instance.Definition.placementPowers, instance);
            }

            if (instance.Definition.type == BodyType.Moon)
            {
                EnsureMoonFalseReport(owner, instance);
            }

            // If a Space Station is placed, activate sensor logging for the placing player
            if (instance.Definition.type == BodyType.SpaceStation)
            {
                ActivateSpaceStationLogging(owner, instance);
            }

            instance.PlacementPowersGranted = true;
        }

        void GrantPowers(PlayerState recipient, Power[] prototypes, BodyInstance sourceBody)
        {
            if (recipient == null || prototypes == null) return;

            // Diagnostic: log incoming prototype info
            Debug.Log($"[GameState] GrantPowers called for recipient={recipient?.team} sourceBody='{sourceBody?.Definition?.displayName}' prototypesLength={(prototypes==null?0:prototypes.Length)}");

            // If prototypes appear to be missing/null (which can happen when the
            // BodyInstance.Definition used at placement was a non-canonical copy),
            // attempt to fetch canonical prototypes from the game's catalog by
            // body type and use those instead.
            Power[] usePrototypes = prototypes;
            bool hasValid = false;
            foreach (var p in prototypes)
            {
                if (p != null) { hasValid = true; break; }
            }

            if (!hasValid && sourceBody != null && sourceBody.Definition != null)
            {
                try
                {
                    BodyDefinition canonical = null;
                    if (this.catalog != null)
                    {
                        canonical = this.catalog.Get(sourceBody.Definition.type);
                        Debug.Log($"[GameState] Catalog present. Lookup for type {sourceBody.Definition.type} returned canonical={(canonical!=null)}");
                    }
                    else
                    {
                        Debug.Log("[GameState] Catalog is null on GameState when attempting canonical lookup.");
                    }
                    if (canonical != null)
                    {
                        Debug.Log($"[GameState] Canonical definition '{canonical.displayName}' prototypes: placement={(canonical.placementPowers!=null?canonical.placementPowers.Length:0)} colonization={(canonical.colonizationPowers!=null?canonical.colonizationPowers.Length:0)}");
                        // Prefer colonizationPowers if the call is from colonization flow
                        if (canonical.colonizationPowers != null && canonical.colonizationPowers.Length > 0)
                        {
                            usePrototypes = canonical.colonizationPowers;
                        }
                        else if (canonical.placementPowers != null && canonical.placementPowers.Length > 0)
                        {
                            usePrototypes = canonical.placementPowers;
                        }
                    }
                }
                catch { /* defensive: if catalog lookup fails, continue with original prototypes */ }
            }

            // If the chosen prototype array doesn't actually contain any valid entries,
            // attempt to fetch a runtime-built catalog (BodyFactory.BuildCatalog) and use
            // its prototypes for this body type. This addresses cases where the
            // serialized/inspector catalog exists but its prototype references are null.
            bool useFallbackRuntimeCatalog = false;
            if (!hasValid)
            {
                try
                {
                    var runtimeCat = BodyFactory.BuildCatalog();
                    if (runtimeCat != null && sourceBody != null)
                    {
                        var runtimeCanonical = runtimeCat.Get(sourceBody.Definition.type);
                        if (runtimeCanonical != null)
                        {
                            Power[] runtimeProtos = null;
                            if (runtimeCanonical.colonizationPowers != null && runtimeCanonical.colonizationPowers.Length > 0)
                                runtimeProtos = runtimeCanonical.colonizationPowers;
                            else if (runtimeCanonical.placementPowers != null && runtimeCanonical.placementPowers.Length > 0)
                                runtimeProtos = runtimeCanonical.placementPowers;

                            if (runtimeProtos != null)
                            {
                                int validCount = 0;
                                foreach (var p in runtimeProtos) if (p != null) validCount++;
                                if (validCount > 0)
                                {
                                    usePrototypes = runtimeProtos;
                                    hasValid = true;
                                    useFallbackRuntimeCatalog = true;
                                }
                            }
                        }
                    }
                }
                catch { /* ignore runtime build failures */ }
            }

            Debug.Log($"[GameState] Using { (usePrototypes==null?0:usePrototypes.Length) } prototypes for granting (hasValid={hasValid}){(useFallbackRuntimeCatalog?" (runtime catalog fallback)":"") }.");

            foreach (var proto in usePrototypes)
            {
                if (proto == null) continue;

                var clone = Instantiate(proto);
                clone.name = proto.name;
                clone.powerName = proto.powerName;
                clone.timing = proto.timing;
                clone.text = proto.text;

                var granted = new GrantedPower
                {
                    power = clone,
                    sourceBody = sourceBody,
                    consumed = false
                };

                recipient.readyPowers.Add(granted);

                var card = CreateCardForPower(granted, sourceBody);
                recipient.hand.Add(card);

                Debug.Log($"[GameState] Granted power '{(clone != null ? clone.powerName : "<null>")}' to {recipient.team} from '{(sourceBody?.Definition?.displayName ?? "<unknown>")}'. Added card '{card.title}'.");
            }
        }

        void EnsureMoonFalseReport(PlayerState owner, BodyInstance sourceBody)
        {
            if (owner == null || sourceBody == null) return;

            var existing = owner.readyPowers.FirstOrDefault(gp => gp != null && gp.sourceBody == sourceBody && gp.power is FalseReportPower);
            if (existing != null)
            {
                bool hasCard = owner.hand.Any(card => card != null && card.grantedPower == existing);
                if (!hasCard)
                {
                    owner.hand.Add(CreateCardForPower(existing, sourceBody));
                }
                return;
            }

            Power prototype = null;
            if (sourceBody.Definition != null && sourceBody.Definition.placementPowers != null)
            {
                prototype = sourceBody.Definition.placementPowers.FirstOrDefault(p => p is FalseReportPower);
            }

            if (!prototype)
            {
                var generated = ScriptableObject.CreateInstance<FalseReportPower>();
                generated.name = "False Report";
                generated.powerName = "False Report";
                generated.text = "Gain one False Report token.";
                generated.timing = PowerTiming.DefensiveFaceDown;
                prototype = generated;
            }

            var clone = Instantiate(prototype);
            clone.name = prototype.name;
            clone.powerName = prototype.powerName;
            clone.timing = prototype.timing;
            clone.text = prototype.text;

            var granted = new GrantedPower
            {
                power = clone,
                sourceBody = sourceBody,
                consumed = false
            };

            owner.readyPowers.Add(granted);
            owner.hand.Add(CreateCardForPower(granted, sourceBody));
            // Grant the false report token immediately upon placement (one token per Moon placement)
            owner.falseReportTokens++;
        }

        void RevokeSolarDividend(PlayerState owner, BodyInstance body)
        {
            if (owner == null || body == null) return;

            var revoked = owner.readyPowers
                .Where(gp => gp != null && gp.sourceBody == body && gp.power is GainCurrencyPower)
                .ToList();

            if (revoked.Count == 0) return;

            owner.readyPowers.RemoveAll(gp => revoked.Contains(gp));
            owner.hand.RemoveAll(card => card != null && card.grantedPower != null && revoked.Contains(card.grantedPower));
        }

        PlayerCard CreateCardForPower(GrantedPower granted, BodyInstance sourceBody)
        {
            var card = new PlayerCard
            {
                grantedPower = granted,
                sourceType = ResolveBodyType(sourceBody),
                title = granted.power ? (!string.IsNullOrEmpty(granted.power.powerName) ? granted.power.powerName : sourceBody?.Definition?.displayName ?? "Power") : "Power",
                description = granted.power ? granted.power.text : string.Empty
            };

            card.art = ResolveCardArt(card.sourceType);
            return card;
        }

        // Create a simple PlayerCard representing the placed/colonized body itself (no granted power)
        PlayerCard CreateCardForBody(BodyInstance sourceBody)
        {
            if (sourceBody == null) return null;
            var card = new PlayerCard
            {
                grantedPower = null,
                sourceType = ResolveBodyType(sourceBody),
                title = sourceBody.Definition != null ? sourceBody.Definition.displayName : "Body",
                description = sourceBody.Definition != null ? sourceBody.Definition.rulesSummary : string.Empty
            };

            card.art = ResolveCardArt(card.sourceType);
            return card;
        }

        BodyType ResolveBodyType(BodyInstance sourceBody)
        {
            if (sourceBody != null && sourceBody.Definition != null)
            {
                return sourceBody.Definition.type;
            }

            return BodyType.Spacejunk;
        }

        Sprite ResolveCardArt(BodyType type)
        {
            if (cardArtCatalog == null) return null;

            var sprite = cardArtCatalog.GetSprite(type);
            if (!sprite)
            {
                Debug.LogWarning($"[CardArtCatalog] Missing card art for body type {type}.");
            }
            return sprite;
        }

        public void ResetPassivePowers(PlayerState player)
        {
            if (player == null) return;

            foreach (var gp in player.readyPowers)
            {
                if (gp != null && gp.power && gp.power.timing == PowerTiming.PassiveUpkeep)
                {
                    gp.consumed = false;
                }
            }
        }

        public bool EvaluateVictory(PlayerState lastAttacker)
        {
            var survivors = players.Where(p => !p.IsEliminated).ToList();
            if (survivors.Count <= 1)
            {
                gameOver = true;
                winner = survivors.Count == 1 ? survivors[0] : lastAttacker ?? survivors.FirstOrDefault();
                return true;
            }

            return false;
        }

        public void RevealRow(PlayerState viewer, PlayerState target, int rowY, int layerZ)
        {
            if (target == null || target.hiddenBoard == null || config == null) return;

            layerZ = Mathf.Clamp(layerZ, 0, depth - 1);
            rowY = Mathf.Clamp(rowY, 0, config.height - 1);

            bool viewerIsOpponent = viewer != null && viewer != target;

            for (int x = 0; x < config.width; x++)
            {
                var cell = target.hiddenBoard.Get(x, rowY, layerZ);
                if (cell == null || cell.probed) continue;

                var outcome = target.hiddenBoard.Probe(x, rowY, layerZ);
                if (viewerIsOpponent && outcome.isHit && outcome.hitBody != null)
                {
                    HandleHit(viewer, target, outcome.hitBody);
                }
            }

            if (!gameOver && viewerIsOpponent && target.IsEliminated)
            {
                EvaluateVictory(viewer);
            }
        }

        public void RevealColumn(PlayerState viewer, PlayerState target, int columnX, int rowY, int layerZ, bool halfRange)
        {
            if (target == null || target.hiddenBoard == null || config == null) return;

            layerZ = Mathf.Clamp(layerZ, 0, depth - 1);
            columnX = Mathf.Clamp(columnX, 0, config.width - 1);
            rowY = Mathf.Clamp(rowY, 0, config.height - 1);

            int startY = 0;
            int endY = config.height - 1;

            if (halfRange)
            {
                int span = Mathf.Max(3, config.height / 2);
                startY = Mathf.Max(0, rowY - span / 2);
                endY = Mathf.Min(config.height - 1, rowY + span / 2);
            }

            bool viewerIsOpponent = viewer != null && viewer != target;

            for (int y = startY; y <= endY; y++)
            {
                var cell = target.hiddenBoard.Get(columnX, y, layerZ);
                if (cell == null || cell.probed) continue;

                var outcome = target.hiddenBoard.Probe(columnX, y, layerZ);
                if (viewerIsOpponent && outcome.isHit && outcome.hitBody != null)
                {
                    HandleHit(viewer, target, outcome.hitBody);
                }
            }

            if (!gameOver && viewerIsOpponent && target.IsEliminated)
            {
                EvaluateVictory(viewer);
            }
        }
    }
}


