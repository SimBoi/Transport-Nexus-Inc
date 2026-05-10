using UnityEngine;
using System.Collections.Generic;
using System;

namespace Inventories
{
    // TODO switch to scriptable objects for dynamically loading gameplay data instead of recompiling the game
    [Serializable]
    public enum ResourceType
    {
        Wood,
        Coal,
        Iron,
    }
}
