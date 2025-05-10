using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106DimensionManager: NetworkBehaviour
{
    public Transform spawnPosition;
    public SCP106EnemyAI _scp106EnemyAI;

    public List<SCP106DimensionGate> gates;

    public void SetGateToEscape(List<int> ids)
    {
        
        gates.ForEach(g =>
        {
            g.EscapeEnabled = ids.Contains(g.id);
        });
    }


}