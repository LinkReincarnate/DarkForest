
using UnityEngine;
using System.Collections.Generic;

namespace DarkForest
{
    // A runtime, serializable BodyDefinition so we can CreateInstance<> at runtime
    [CreateAssetMenu(menuName="DarkForest/RuntimeBodyDefinition")]
    public class RuntimeBodyDefinition : BodyDefinition
    {
        // Helper to build shapes quickly
        public static BodyShape Make(params V3[] pts) => new BodyShape(pts);
    }
}
