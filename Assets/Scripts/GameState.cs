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
            playerCount = playersCount;
            depth = depthLevels;
            players.Clear();

            for (int i = 0; i < playerCount; i++)
            {
                var ps = new PlayerState
                {
                    team = (Team)i,
                    hiddenBoard = new Board(config.width, config.height, depthLevels),
                    currency = config.GetStartingCurrency(playerCount, depthLevels),
                    placementReady = false
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

            foreach (var candidate in players)
            {
                if (candidate != player && !candidate.IsEliminated)
                {
                    return candidate;
                }
            }

            return players.FirstOrDefault(p => p != player) ?? player;
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

            for (int i = 1; i <= players.Count; i++)
            {
                int idx = (currentIndex + i) % players.Count;
                if (!players[idx].IsEliminated)
                {
                    currentIndex = idx;
                    ResetPassivePowers(players[idx]);
                    return;
                }
            }
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
            }
        }

        public void HandlePlacement(PlayerState owner, BodyInstance instance)
        {
            if (instance == null || instance.Definition == null || instance.PlacementPowersGranted) return;

            if (instance.Definition.placementPowers != null && instance.Definition.placementPowers.Length > 0)
            {
                GrantPowers(owner, instance.Definition.placementPowers, instance);
            }

            instance.PlacementPowersGranted = true;
        }

        void GrantPowers(PlayerState recipient, Power[] prototypes, BodyInstance sourceBody)
        {
            if (recipient == null || prototypes == null) return;

            foreach (var proto in prototypes)
            {
                if (!proto) continue;

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
            }
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
