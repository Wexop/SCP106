using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106DimensionManager: NetworkBehaviour
{
    public Transform spawnPosition;
    public SCP106EnemyAI _scp106EnemyAI;

    public List<SCP106DimensionGate> gates;

    public void SetGateToEscape(int id)
    {
        
        gates.ForEach(g =>
        {
            g.EscapeEnabled = g.id == id;
        });
    }


}