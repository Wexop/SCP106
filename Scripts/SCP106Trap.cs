using System.Collections;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106Trap: NetworkBehaviour
{
    private static readonly int End = Animator.StringToHash("end");
    public SCP106EnemyAI _scp106EnemyAI;
    public ParticleSystem SendToPocketParticles;
    public Animator Animator;
    
    private NetworkVariable<NetworkObjectReference> enemyAIRef = new NetworkVariable<NetworkObjectReference>();

    private float lifeTimer = 60f;

    private void Start()
    {
        if (IsServer) SetLifeTimeServerRpc(SCP106Plugin.instance.trapLifeTime.Value);
    }

    private void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer < 0f)
        {
            Animator.SetTrigger(End);
            if(IsServer) StartCoroutine(OnLifeTimeEnd());
        }
    }

    [ServerRpc]
    private void SetLifeTimeServerRpc(float value)
    {
        SetLifeTimeClientRpc(value);
    }
    
    [ClientRpc]
    private void SetLifeTimeClientRpc(float value)
    {
        lifeTimer = value;
    }

    private IEnumerator OnLifeTimeEnd()
    {
        yield return new WaitForSeconds(1f);
        NetworkObject.Despawn();
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsClient && enemyAIRef.Value.TryGet(out NetworkObject enemyAIObject))
        {
            _scp106EnemyAI = enemyAIObject.GetComponent<SCP106EnemyAI>();
        }
        
        enemyAIRef.OnValueChanged += OnEnemyAIReferenceChanged;


    }
    
    private void OnEnemyAIReferenceChanged(NetworkObjectReference previous, NetworkObjectReference current)
    {
        if (current.TryGet(out NetworkObject enemyAIObject))
        {
            _scp106EnemyAI = enemyAIObject.GetComponent<SCP106EnemyAI>();
        }
    }

    [ServerRpc]
    public void SetEnemyAIReferenceServerRpc(NetworkObjectReference aiRef)
    {
        enemyAIRef.Value = aiRef;

        if (aiRef.TryGet(out NetworkObject enemyAIObject))
        {
            _scp106EnemyAI = enemyAIObject.GetComponent<SCP106EnemyAI>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if(!_scp106EnemyAI) return;
        if (other.CompareTag("Player"))
        {
            PlayerControllerB player = other.GetComponent<PlayerControllerB>();
            if (player != null && !player.isPlayerDead && player.playerClientId == GameNetworkManager.Instance.localPlayerController.playerClientId)
            {
                SCP106Plugin.instance.InstantiateDimension(_scp106EnemyAI);
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