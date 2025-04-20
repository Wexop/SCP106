using System;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106Portal: MonoBehaviour
{

    public float TimeLife = 20f;

    private float tpPlayerTimer = 0f;
    private float tpPlayerDelay = 2f;

    public Vector3 connectedPos;

    private void Start()
    {
        StartCoroutine(KillPortalCoroutine());
    }

    private void Update()
    {
        tpPlayerTimer -= Time.deltaTime;
        transform.LookAt(GameNetworkManager.Instance.localPlayerController.gameplayCamera.transform);
    }

    private IEnumerator KillPortalCoroutine()
    {
        yield return new WaitForSeconds(TimeLife);
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision other)
    {
        
        Debug.Log($"{other.gameObject.name} POS {connectedPos} FROM {transform.position}");
        if(connectedPos == null || tpPlayerTimer <= 0) return;
        tpPlayerTimer = tpPlayerDelay;
        
       
        
        PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
        if (player != null && GameNetworkManager.Instance.localPlayerController.playerSteamId == player.playerSteamId)
        {
            GameNetworkManager.Instance.localPlayerController.transform.position = connectedPos;
        }
    }
}