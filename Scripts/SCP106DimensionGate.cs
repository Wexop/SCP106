using System;
using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106DimensionGate : MonoBehaviour
{
    private static readonly int Open = Animator.StringToHash("open");

    public bool EscapeEnabled;
    public SCP106DimensionManager dimensionManager;
    public int id;
    public Animator animator;
    public AudioSource audioSource;
    public AudioClip openClip;

    public void OnPlayerOpenDoor(PlayerControllerB player)
    {
        dimensionManager._scp106EnemyAI.OnPlayerOpenDoorServerRpc(id, player.playerClientId);
    }

    public void DoorAnimation(PlayerControllerB player)
    {
        if (EscapeEnabled)
        {
            dimensionManager._scp106EnemyAI.PlayerEscapedDimensionServerRpc(player.playerClientId);
        }
        else
        {
            animator.SetTrigger(Open);
            player.disableMoveInput = true;
            player.JumpToFearLevel(0.8f);
            audioSource.PlayOneShot(openClip);
            StartCoroutine(KillPlayerAnimation(player));
        }
    }

    private IEnumerator KillPlayerAnimation(PlayerControllerB player)
    {
        yield return new WaitForSeconds(1.25f);
        player.KillPlayer(Vector3.zero, causeOfDeath: CauseOfDeath.Crushing);
        player.disableMoveInput = false;

    }

}