using System.Collections.Generic;
using UnityEngine;

namespace DarkForest
{
    public static class BodyFactory
    {
        public static BodyCatalog BuildCatalog()
        {
            var cat = ScriptableObject.CreateInstance<BodyCatalog>();
            var list = new List<BodyDefinition>();

            RuntimeBodyDefinition Make(BodyType type, string name, int cap, int price, BodyDefinition.NearPattern near, BodyShape shape, string summary)
            {
                var def = ScriptableObject.CreateInstance<RuntimeBodyDefinition>();
                def.type = type;
                def.displayName = name;
                def.populationCapacity = cap;
                def.price = price;
                def.nearPattern = near;
                def.shape = shape;
                def.rulesSummary = summary;
                return def;
            }

            Power Power<T>(string name, string description, PowerTiming timing, System.Action<T> configure = null) where T : Power
            {
                var p = ScriptableObject.CreateInstance<T>();
                p.name = name;
                p.powerName = name;
                p.text = description;
                p.timing = timing;
                configure?.Invoke(p);
                return p;
            }

            BodyShape RectShape(int w, int h)
            {
                var pts = new List<V3>();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        pts.Add(new V3(x, y, 0));
                    }
                }
                return new BodyShape(pts.ToArray());
            }

            // Spacejunk 1x1
            list.Add(Make(BodyType.Spacejunk, "Spacejunk", 1, 1,
                BodyDefinition.NearPattern.AdjacentNoDiag,
                new BodyShape(new V3(0, 0, 0)),
                "Near-hit reporting. Cheap decoy."));

            // Satellite 2x1
            var satellite = Make(BodyType.Satellite, "Satellite", 2, 2,
                BodyDefinition.NearPattern.AdjacentNoDiag,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0)),
                "Near-hit. Unlocks Scan Row when colonized.");
            satellite.colonizationPowers = new[]
            {
                Power<ScanRowPower>("Scan Row", "Reveal one entire row on the opponent board.", PowerTiming.OffensiveFaceUp)
            };
            list.Add(satellite);

            // Rocket 3x1
            var rocket3 = Make(BodyType.Rocket3, "Rocket (3)", 3, 3,
                BodyDefinition.NearPattern.None,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(2, 0, 0)),
                "No near-hit. Unlocks Scan Column when colonized.");
            rocket3.colonizationPowers = new[]
            {
                Power<ScanColumnPower>("Scan Column", "Reveal the entire column containing the selected cell.", PowerTiming.OffensiveFaceUp)
            };
            list.Add(rocket3);

            // Rocket 4x1
            var rocket4 = Make(BodyType.Rocket4, "Rocket (4)", 4, 4,
                BodyDefinition.NearPattern.AdjacentNoDiag,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(2, 0, 0), new V3(3, 0, 0)),
                "Near-hit. Unlocks Half Column scan when colonized.");
            rocket4.colonizationPowers = new[]
            {
                Power<ScanColumnPower>("Half Column Scan", "Reveal a segment of the selected column.", PowerTiming.OffensiveFaceUp, p => p.halfRange = true)
            };
            list.Add(rocket4);

            // Moon (plus shape 5)
            var moon = Make(BodyType.Moon, "Moon", 5, 5,
                BodyDefinition.NearPattern.AdjacentNoDiag,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(-1, 0, 0), new V3(0, 1, 0), new V3(0, -1, 0)),
                "Defensive bulwark. Grants a False Report token when placed.");
            moon.placementPowers = new[]
            {
                Power<FalseReportPower>("False Report", "Gain one False Report token.", PowerTiming.DefensiveFaceDown)
            };
            list.Add(moon);

            // Space Station (2x3 rectangle, 6 tiles)
            var spaceStation = Make(BodyType.SpaceStation, "Space Station", 6, 6,
                BodyDefinition.NearPattern.None,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(0, 1, 0), new V3(1, 1, 0), new V3(0, 2, 0), new V3(1, 2, 0)),
                "Advanced scanning platform.");
            spaceStation.colonizationPowers = new[]
            {
                Power<RevealNeighborsPower>("Sensor Burst", "Reveal a target cell and its orthogonal neighbors.", PowerTiming.OffensiveFaceUp, p =>
                {
                    p.includeDiagonals = false;
                    p.includeVertical = false;
                })
            };
            list.Add(spaceStation);

            // Alien Artifact (L ladder 7)
            var artifact = Make(BodyType.AlienArtifact, "Alien Artifact", 7, 7,
                BodyDefinition.NearPattern.None,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(2, 0, 0), new V3(0, 1, 0), new V3(0, 2, 0), new V3(0, 3, 0), new V3(1, 3, 0)),
                "Experimental tech. Cloaks your intel when threatened.");
            artifact.placementPowers = new[]
            {
                Power<EraseProbePower>("Afterimage", "Erase intel from one of your probed cells.", PowerTiming.DefensiveFaceDown)
            };
            artifact.colonizationPowers = new[]
            {
                Power<EraseProbePower>("Afterimage", "Erase intel from one of your probed cells.", PowerTiming.DefensiveFaceDown)
            };
            list.Add(artifact);

            // Planet (2x4 rectangle, 8 tiles)
            var planet = Make(BodyType.Planet, "Planet", 8, 8,
                BodyDefinition.NearPattern.None,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(0, 1, 0), new V3(1, 1, 0), new V3(0, 2, 0), new V3(1, 2, 0), new V3(0, 3, 0), new V3(1, 3, 0)),
                "Maintains double probe tempo while intact.");
            list.Add(planet);

            // Asteroid Belt (3x4 rectangle, 12 tiles)
            var asteroid = Make(BodyType.AsteroidBelt, "Asteroid Belt", 12, 10,
                BodyDefinition.NearPattern.None,
                RectShape(3, 4),
                "Sturdy debris field that pays out on survival.");
            asteroid.colonizationPowers = new[]
            {
                Power<GainCurrencyPower>("Salvage Bounty", "Gain 5 currency when this belt is fully colonized.", PowerTiming.PassiveUpkeep, p => p.amount = 5)
            };
            list.Add(asteroid);

            // Nebula
            var nebula = Make(BodyType.Nebula, "Nebula", 8, 9,
                BodyDefinition.NearPattern.AdjacentAllDirs,
                new BodyShape(new V3(0, 0, 0), new V3(1, 0, 0), new V3(-1, 0, 0), new V3(0, 1, 0), new V3(0, -1, 0),
                              new V3(1, 1, 0), new V3(-1, 1, 0), new V3(0, 2, 0)),
                "Scan beacon on colonization.");
            nebula.colonizationPowers = new[]
            {
                Power<RevealNeighborsPower>("Beacon Sweep", "Reveal all adjacent cells (including vertical neighbors).", PowerTiming.OffensiveFaceUp, p =>
                {
                    p.includeDiagonals = true;
                    p.includeVertical = true;
                })
            };
            list.Add(nebula);

            // Star
            var star = Make(BodyType.Star, "Star", 12, 15,
                BodyDefinition.NearPattern.AdjacentAllLayers,
                new BodyShape(new List<V3>
                {
                    new V3(0,0,0), new V3(1,0,0), new V3(-1,0,0), new V3(2,0,0), new V3(-2,0,0),
                    new V3(0,1,0), new V3(0,-1,0), new V3(0,2,0), new V3(0,-2,0),
                    new V3(1,1,0), new V3(1,-1,0), new V3(-1,-1,0)
                }),
                "Generates currency every other turn.");
            star.colonizationPowers = new[]
            {
                Power<GainCurrencyPower>("Solar Dividend", "Gain 1 currency each turn while active.", PowerTiming.PassiveUpkeep, p => p.amount = 1)
            };
            list.Add(star);

            // Ringworld
            var ringworld = Make(BodyType.Ringworld, "Ringworld", 16, 14,
                BodyDefinition.NearPattern.AdjacentNoDiag,
                BuildRingShape(),
                "Massive defensive ring.");
            ringworld.colonizationPowers = new[]
            {
                Power<EraseProbePower>("Recloak Segment", "Erase probing intel from one of your cells.", PowerTiming.DefensiveFaceDown)
            };
            list.Add(ringworld);

            // Dyson Sphere
            var dyson = Make(BodyType.DysonSphere, "Dyson Sphere", 24, 18,
                BodyDefinition.NearPattern.AdjacentNoDiag,
                BuildDysonShape(),
                "Requires a Star. Upgrades its economy.");
            dyson.colonizationPowers = new[]
            {
                Power<GainCurrencyPower>("Stellar Harvest", "Gain 3 currency each turn while active (requires a Star).", PowerTiming.PassiveUpkeep, p => p.amount = 3)
            };
            list.Add(dyson);

            cat.entries = list.ToArray();
            return cat;

            BodyShape BuildRingShape()
            {
                var ring = new List<V3>();
                for (int i = -2; i <= 2; i++)
                {
                    ring.Add(new V3(i, -2, 0));
                    ring.Add(new V3(i, 2, 0));
                }
                for (int j = -1; j <= 1; j++)
                {
                    ring.Add(new V3(-2, j, 0));
                    ring.Add(new V3(2, j, 0));
                }
                return new BodyShape(ring.ToArray());
            }

            BodyShape BuildDysonShape()
            {
                var pts = new List<V3>();
                for (int i = -3; i <= 3; i++)
                {
                    pts.Add(new V3(-3, i, 0));
                    pts.Add(new V3(3, i, 0));
                }
                for (int j = -2; j <= 2; j++)
                {
                    pts.Add(new V3(j, -3, 0));
                    pts.Add(new V3(j, 3, 0));
                }
                return new BodyShape(pts.ToArray());
            }
        }
    }
}

