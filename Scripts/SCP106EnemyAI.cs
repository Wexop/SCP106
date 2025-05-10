using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SCP106.Scripts;

public class SCP106EnemyAI: EnemyAI
{
    private static readonly int GoToPortal = Animator.StringToHash("GoToPortal");
    private static readonly int OutOfPortal = Animator.StringToHash("OutOfPortal");
    private static readonly int Punch = Animator.StringToHash("Punch");
    private static readonly int Run = Animator.StringToHash("Run");

    private static readonly int Spawn = Animator.StringToHash("Spawn");
    //public GameObject PortalObject;

    public GameObject TrapObject;
   
    public ParticleSystem SendToPocketParticles;

    public List<AudioClip> walkSounds;
    public AudioClip seePlayerSound;
    

    private float visionWidth = 80f;
    private float baseSpeed = 5f;
    private float runSpeed = 7f;
    private float inDimensionSpeed = 9f;

    private float spawningTimer = 7f;
    
    public LayerMask layerRoom;
    
    private float createPortalDelay = 30f;
    private float createPortalTimer = 10f;
    
    private float createTrapDelay = 45f;
    private float createTrapTimer = 10f;

    private List<Vector3> savedWallPosition = new List<Vector3>();
    private float saveWallPosTimer = 0f;
    private float saveWallPosDelay = 20f;
    
    private bool alreadyCreatedPortal = false;
    private bool isGoingToPortal = false;
    
    private float chaseTimer = 0f;
    private float chaseDelay = 5f;
    
    private float hitTimer = 0f;
    private float hitDelay = 1f;
    
    private float aiInterval = 0.2f;
    private int lastBehaviorState = 0;

    private List<ulong> playersIdsInDimension = new List<ulong>();
    private float goToPocketDimensionTimer = 30f;
    private float goToPocketDimensionDelay = 30f;
    
    private float walkSoundTimer = 0f;
    private float walkSoundDelayWalk = 0.9f;
    private float walkSoundDelayRun = 0.5f;

    private Vector3 spawnPos;
    private Vector3 portalGoingToPos;

    private ulong playerChasingId;
    public override void Start()
    {

        base.Start();

        agent.speed = baseSpeed;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;
        
        spawnPos = transform.position;
    }

    public override void Update()
    {
        base.Update();
        
        if (currentBehaviourStateIndex == 1 && GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(transform.position + Vector3.up * 0.25f, 100f, 60))
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.8f);
        }
        
        if (lastBehaviorState != currentBehaviourStateIndex)
        {
            if(SCP106Plugin.instance.debug.Value) Debug.Log($"New behavior state : {currentBehaviourStateIndex} last : {lastBehaviorState}");
            lastBehaviorState = currentBehaviourStateIndex;
            AllClientOnSwitchBehaviorState();

        }
        walkSoundTimer -= Time.deltaTime;

        hitTimer -= Time.deltaTime;
        spawningTimer -= Time.deltaTime;
        
        //WALKSOUNDS
        if ( spawningTimer < 0 && walkSoundTimer <= 0f)
        {
            var randomSound = walkSounds[Random.Range(0, walkSounds.Count)];
            creatureSFX.PlayOneShot(randomSound);
            walkSoundTimer = currentBehaviourStateIndex == 1 ? walkSoundDelayRun : walkSoundDelayWalk;

        }
        
        if(!IsServer) return;

        if (goToPocketDimensionTimer <= 0 && currentBehaviourStateIndex != 2 && SCP106Plugin.instance.actualDimensionObjectManager != null)
        {
            StopSearch(currentSearch);
            agent.enabled = false;
            transform.position = SCP106Plugin.instance.actualDimensionObjectManager.spawnPosition.position;
            agent.enabled = true;
            SyncPositionToClients();
            SwitchToBehaviourState(2);
        }
        
        createPortalTimer -= Time.deltaTime;
        saveWallPosTimer -= Time.deltaTime;
        aiInterval -= Time.deltaTime;
        createTrapTimer -= Time.deltaTime;

        if(currentBehaviourStateIndex == 1) chaseTimer -= Time.deltaTime;
        
        if(GetPlayerCountInDimension() > 0) goToPocketDimensionTimer -= Time.deltaTime;
        
        if (aiInterval <= 0 && IsServer)
        {
            aiInterval = AIIntervalTime;
            DoAIInterval();
        }
        
        Vector3 direction = transform.forward;

        if (saveWallPosTimer <= 0 && Physics.Raycast(eye.position, direction, out RaycastHit hitWall, 10 ,layerRoom) && currentBehaviourStateIndex != 2)
        {
            saveWallPosTimer = saveWallPosDelay;
            SavePortalPosition(hitWall.point);
        }

        if (createTrapTimer <= 0 && Physics.Raycast(eye.position, Vector3.down, out RaycastHit hitGround, 3 ,layerRoom) && currentBehaviourStateIndex != 2)
        {
            if(SCP106Plugin.instance.debug.Value) Debug.Log($"Create trap");

            createTrapTimer = createTrapDelay;
            var trap = Instantiate(TrapObject, hitGround.point + Vector3.up * 0.2f , Quaternion.identity);
            SCP106Trap scp106Trap = trap.GetComponent<SCP106Trap>();
            scp106Trap._scp106EnemyAI = this;
            trap.GetComponent<NetworkObject>().Spawn(true);
            scp106Trap.SetEnemyAIReferenceServerRpc(new NetworkObjectReference(NetworkObject));

        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        
        if(spawningTimer > 0 || stunNormalizedTimer > 0 || !IsServer) return;
        
        switch (currentBehaviourStateIndex)
        {
            //ROAMING
            case 0:
            {
                
                TargetClosestPlayer(requireLineOfSight: true, viewWidth: visionWidth);
    
                if (targetPlayer == null)
                {
                    if (createPortalTimer <= 0 )
                    {
                        if(alreadyCreatedPortal) break;

                        //debug case
                        if (createPortalTimer <= -15)
                        {
                            createPortalTimer = createPortalDelay;
                            alreadyCreatedPortal = false;
                            break;
                        }
                        
                        if(SCP106Plugin.instance.debug.Value) Debug.Log($"Go through wall {createPortalTimer} {isGoingToPortal} {savedWallPosition.Count}");
                        if (!isGoingToPortal && savedWallPosition.Count > 1)
                        {
                            var pos = GetClosestWallPosition();
                            StopSearch(currentSearch);

                            SetDestinationToPosition(pos);
                            portalGoingToPos = pos;
                            
                            isGoingToPortal = true;
                        
                            break;
                        }

                        if (isGoingToPortal && agent.remainingDistance <= agent.stoppingDistance)
                        {
                            if(SCP106Plugin.instance.debug.Value) Debug.Log("Going Portal");
                            
                            transform.LookAt(portalGoingToPos);
                            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
                            var pos = savedWallPosition[Random.Range(0, savedWallPosition.Count)];
               
                            GoToPortalServerRpc(transform.position, pos);
                            alreadyCreatedPortal = true;
                            break;
                        }
                        
                        break;
                    }
                    
                    if (currentSearch.inProgress) break;
                    AISearchRoutine aiSearchRoutine = new AISearchRoutine();
                    aiSearchRoutine.searchWidth = 100f;
                    aiSearchRoutine.searchPrecision = 8f;
                    StartSearch(ChooseFarthestNodeFromPosition(transform.position, true).position, aiSearchRoutine);

                }
                else if (PlayerIsTargetable(targetPlayer))
                {
                    chaseTimer = chaseDelay;
                    playerChasingId = targetPlayer.playerClientId;
                    StopSearch(currentSearch);
                    currentSearch.inProgress = false;
                    SwitchToBehaviourState(1);
                    
                }
                break;
                
            }
            //CHASING PLAYER
            case 1:
            {
                targetPlayer = GetChasingPlayer();
                if (chaseTimer <= 0f)
                {
                    TargetClosestPlayer(requireLineOfSight: true, viewWidth: visionWidth);
                    if (targetPlayer != null)
                    {
                        playerChasingId = targetPlayer.playerClientId;
                        chaseTimer += 1f;
                    }
                    else
                    {
                        chaseTimer = chaseDelay;
                        SwitchToBehaviourState(0);
                    }
                    
                }
                if (targetPlayer != null  && PlayerIsTargetable(targetPlayer))
                {
                    SetMovingTowardsTargetPlayer(targetPlayer);
                }
                
                break;
                
            }
            //IN POCKET DIMENSION
            case 2:
            {

                if (GetPlayerCountInDimension() <= 0)
                {
                    TpToRandomWallPoint();

                }
                
                var player = GetClosestPlayer();
                if (PlayerIsTargetable(player))
                {
                    SetMovingTowardsTargetPlayer(player);
                }
                

                break;
            }
        }
        
    }
    
    private void AllClientOnSwitchBehaviorState()
    {
        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                agent.speed = baseSpeed;
                creatureAnimator.SetBool(Run, false);
                break;
            }
            case 1:
            {
                creatureVoice.PlayOneShot(seePlayerSound);
                agent.speed = runSpeed;
                creatureAnimator.SetBool(Run, true);
                
                createPortalTimer = createPortalDelay;
                alreadyCreatedPortal = false;
                break;
            }
            case 2:
            {
                creatureVoice.PlayOneShot(seePlayerSound);
                agent.speed = inDimensionSpeed;
                creatureAnimator.SetBool(Run, true);
                break;
            }
        }
    }

    private Vector3 GetClosestWallPosition()
    {
        var closestWallPosition = savedWallPosition[0];
        savedWallPosition.ForEach(wall =>
        {
            if (Vector3.Distance(wall, transform.position) < Vector3.Distance(transform.position, closestWallPosition))
            {
                closestWallPosition = wall;
            }
        });
        
        return closestWallPosition;
    }

    private PlayerControllerB GetChasingPlayer()
    {
        PlayerControllerB playerControllerB = null;
        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (!p.isPlayerDead && p.playerClientId == playerChasingId)
            {
                playerControllerB = p;
            }
        });
        
        return playerControllerB;
    }

    private void SavePortalPosition(Vector3 pos)
    {

        bool canSave = true;
        
        savedWallPosition.ForEach(p =>
        {
            if(Vector3.Distance(p, pos) < 3) canSave = false;
        });
        
        if(canSave) savedWallPosition.Add(pos);
    }

    int GetPlayerCountInDimension()
    {
        int count = 0;
        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (!p.isPlayerDead && playersIdsInDimension.Contains(p.playerClientId))
            {
                count++;
            }
        });
        
        return count;
    }

    [ServerRpc]
    public void CreatePortalServerRpc(Vector3 position, Vector3 connectedPosition, bool goToPos = false)
    {
        CreatePortalClientRpc(position, connectedPosition, goToPos );
    }

    [ClientRpc]
    public void CreatePortalClientRpc(Vector3 position, Vector3 connectedPosition, bool goToPos = false)
    {
        if (goToPos)
        {
            agent.SetDestination(position);
            createPortalTimer = createPortalDelay;
        }

    }

    [ServerRpc]
    private void GoToPortalServerRpc(Vector3 pos, Vector3 nextPos)
    {
        GoToPortalClientRpc(pos, nextPos);
    }
    
    [ClientRpc]
    private void GoToPortalClientRpc(Vector3 pos, Vector3 nextPos)
    {
        StartCoroutine(GoToPortalLocal(pos, nextPos));
    }

    private IEnumerator GoToPortalLocal(Vector3 pos, Vector3 nextPos)
    {
        transform.position = pos;
        creatureAnimator.SetTrigger(GoToPortal);
        
        yield return new WaitForSeconds(2);
        
        transform.position = nextPos;
        creatureAnimator.SetTrigger(OutOfPortal);
        alreadyCreatedPortal = false;
        if(IsServer)
        {
            SyncPositionToClients();
            
            isGoingToPortal = false;
            createPortalTimer = createPortalDelay;
        }
        
    }

    private void TpToRandomWallPoint()
    {
        goToPocketDimensionTimer = goToPocketDimensionDelay;
        agent.enabled = false;
        transform.position = savedWallPosition.Count > 0 ? savedWallPosition[Random.Range(0, savedWallPosition.Count)] : spawnPos;
        agent.enabled = true;
        spawningTimer = 2f;
        creatureAnimator.SetTrigger(Spawn);
        SwitchToBehaviourState(0);

    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerEscapedDimensionServerRpc(ulong id)
    {
        PlayerEscapedDimensionClientRpc(id);
    }
    
    [ClientRpc]
    public void PlayerEscapedDimensionClientRpc(ulong id)
    {
        playersIdsInDimension.Remove(id);
        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (p.playerClientId == id && !p.isPlayerDead)
            {
                p.transform.position = savedWallPosition.Count > 0 ? savedWallPosition[Random.Range(0, savedWallPosition.Count)] : spawnPos;
            }
        });
    }

    [ServerRpc(RequireOwnership = false)]
    void PlayerKilledInDimensionServerRpc(ulong id)
    {
        TpToRandomWallPoint();
        PlayerKilledInDimensionClientRpc(id);

    }
    
    [ClientRpc]
    void PlayerKilledInDimensionClientRpc(ulong id)
    {
        
        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (p.playerClientId != id && !p.isPlayerDead && playersIdsInDimension.Contains(p.playerClientId))
            {
                p.transform.position = savedWallPosition.Count > 0 ? savedWallPosition[Random.Range(0, savedWallPosition.Count)] : spawnPos;
            }
        });
        playersIdsInDimension.Remove(id);
        
    }

    [ServerRpc(RequireOwnership = false)]
    public void PlayerInDimensionServerRpc(ulong id, bool withParticules = true)
    {
        goToPocketDimensionTimer = goToPocketDimensionDelay;
        PlayerInDimensionClientRpc(id, withParticules);
    }
    [ClientRpc]
    void PlayerInDimensionClientRpc(ulong id, bool withParticules = true)
    {
        playersIdsInDimension.Add(id);
        if (withParticules)
        {
            SendToPocketParticles.Clear();
            SendToPocketParticles.Play();
        }
    }

    private int GetRandomGateId()
    {
        return SCP106Plugin.instance.actualDimensionObjectManager.gates[Random.Range(0, SCP106Plugin.instance.actualDimensionObjectManager.gates.Count)].id;

    }

    [ServerRpc(RequireOwnership = false)]
    public void SetRandomGateEscapeServerRpc()
    {
        List<int> ids = new List<int>();

        while (ids.Count < SCP106Plugin.instance.numberOfGoodDoor.Value)
        {
            int idToAdd = GetRandomGateId();
            if(SCP106Plugin.instance.debug.Value) Debug.Log($"ORIGINAL GATE ID TO ADD {idToAdd}");

            while (ids.Contains(idToAdd) || idToAdd == null)
            {
                if(SCP106Plugin.instance.debug.Value) Debug.Log($"GATE ID TO ADD {idToAdd}");
                idToAdd = GetRandomGateId();
            }
            ids.Add(idToAdd);
        }
        
        SetRandomGateEscapeClientRpc(ids.ToArray());
    }
    

    [ClientRpc]
    public void SetRandomGateEscapeClientRpc(int[] ids)
    {
        if (SCP106Plugin.instance.debug.Value)
        {
            ids.ToList().ForEach(id => Debug.Log($"GOOD GATE ID : {id}"));
        }

        StartCoroutine(SetGateEscape(ids.ToList()));
    }

    private IEnumerator SetGateEscape(List<int> ids)
    {

        yield return new WaitUntil(() => SCP106Plugin.instance.actualDimensionObjectManager != null);
        SCP106Plugin.instance.actualDimensionObjectManager.SetGateToEscape(ids);

    }

    [ServerRpc(RequireOwnership = false)]
    public void OnPlayerOpenDoorServerRpc(int doorId, ulong playerId)
    {
        OnPlayerOpenDoorClientRpc(doorId, playerId);
    }
    
    [ClientRpc]
    public void OnPlayerOpenDoorClientRpc(int doorId, ulong playerId)
    {
        PlayerControllerB player = null;
        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if (p.playerClientId == playerId && !p.isPlayerDead)
            {
                player = p;
            }
        });
        
        SCP106Plugin.instance.actualDimensionObjectManager.gates.ForEach(g =>
        {
            if (g.id == doorId)
            {
                g.DoorAnimation(player);
            }
        });
    }
    

    public override void OnCollideWithPlayer(Collider other)
    {
        if(spawningTimer > 0) return;
        creatureAnimator.SetTrigger(Punch);
        var player = MeetsStandardPlayerCollisionConditions(other, false, true);
        targetPlayer = player;
        if (player != null && hitTimer <= 0)
        {
            hitTimer = hitDelay;
            if (currentBehaviourStateIndex == 2)
            {
                player.KillPlayer(Vector3.forward * 3);
                PlayerKilledInDimensionServerRpc(player.playerClientId);
            }
         
            if (player.health >= 40)
            {
                player.DamagePlayer(30, causeOfDeath: CauseOfDeath.Kicking);
            }
            else
            {
                SCP106Plugin.instance.InstantiateDimension(this);
                player.transform.position = SCP106Plugin.instance.actualDimensionObjectManager.spawnPosition.position;
                PlayerInDimensionServerRpc(player.playerClientId);
                
            }
        }
    }

    public void ServerRunAfterCreatedDimension()
    {
        StartCoroutine(AfterCreatedDimension());
    }

    public IEnumerator AfterCreatedDimension()
    {
        yield return new WaitForSecondsRealtime(1f);
        SetRandomGateEscapeServerRpc();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        SCP106Plugin.instance.DestroyDimension();
    }
}