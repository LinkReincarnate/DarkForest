
using System;
namespace DarkForest
{
    public enum DepthLevel { Single = 1, Two = 2, Three = 3 }
    public enum CellReport { Unknown, Miss, Hit, NearMiss }
    public enum Team { PlayerA, PlayerB, PlayerC, PlayerD }
    public enum PowerTiming { OffensiveFaceUp, DefensiveFaceDown, PassiveUpkeep }
    public enum BodyType { Spacejunk, Satellite, Rocket3, Rocket4, Moon, SpaceStation, AlienArtifact, Planet, AsteroidBelt, Nebula, Star, Ringworld, DysonSphere }
}
