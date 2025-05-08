using System;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106Trap: NetworkBehaviour
{
    public SCP106EnemyAI _scp106EnemyAI;
    public ParticleSystem SendToPocketParticles;

    private void Start()
    {
        if (IsServer) NetworkObject.Spawn();
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player != null && !player.isPlayerDead && player.playerClientId == GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                SCP106Plugin.instance.InstantiateDimension();
                OnPlayerCollideServerRpc(player.playerClientId);
                player.transform.position = SCP106Plugin.instance.actualDimensionObjectManager.spawnPosition.position;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void OnPlayerCollideServerRpc(ulong id)
    {
        _scp106EnemyAI.PlayerInDimensionServerRpc(id, false);
        OnPlayerCollideClientRpc();
    }

    [ClientRpc]
    void OnPlayerCollideClientRpc()
    {
        SendToPocketParticles.Clear();
        SendToPocketParticles.Play();
    }
}