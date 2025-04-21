using System;
using System.Collections;
using System.Collections.Generic;
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
    public GameObject PortalObject;

    private float visionWidth = 60f;
    private float baseSpeed = 3.5f;
    private float runSpeed = 4.5f;

    private float spawningTimer = 2f;

    private float wallDistanceToSpawnPortal = 10f;
    private Vector3 lastPortalPosition;
    
    public LayerMask layerRoom;
    
    private float createPortalDelay = 30f;
    private float createPortalTimer = 5f;

    private List<Vector3> savedWallPosition = new List<Vector3>();
    private float saveWallPosTimer = 0f;
    private float saveWallPosDelay = 20f;
    
    private bool alreadyCreatedPortal = false;
    private bool isGoingToPortal = false;
    
    private float chaseTimer = 0f;
    private float chaseDelay = 4f;
    
    private float hitTimer = 0f;
    private float hitDelay = 1f;
    
    private float aiInterval = 0.2f;
    private int lastBehaviorState = 0;

    private List<ulong> playersIdsInDimension = new List<ulong>();
    private float goToPocketDimensionTimer = 30f;
    private float goToPocketDimensionDelay = 30f;
    public override void Start()
    {

        base.Start();

        agent.speed = baseSpeed;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;
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
            lastBehaviorState = currentBehaviourStateIndex;
            AllClientOnSwitchBehaviorState();

        }
        
        hitTimer -= Time.deltaTime;
        spawningTimer -= Time.deltaTime;

        if(currentBehaviourStateIndex == 1) chaseTimer -= Time.deltaTime;
        
        if(!IsServer) return;

        if (goToPocketDimensionTimer <= 0)
        {
            SwitchToBehaviourState(2);
        }
        
        //createPortalTimer -= Time.deltaTime;
        saveWallPosTimer -= Time.deltaTime;
        aiInterval -= Time.deltaTime;
        if(playersIdsInDimension.Count > 0) goToPocketDimensionTimer -= Time.deltaTime;
        
        if (aiInterval <= 0 && IsOwner)
        {
            aiInterval = AIIntervalTime;
            DoAIInterval();
        }
        
        Vector3 direction = transform.forward;

        if (saveWallPosTimer <= 0 && Physics.Raycast(eye.position, direction, out RaycastHit hitWall, 10 ,layerRoom))
        {
            saveWallPosTimer = saveWallPosDelay;
            SavePortalPosition(hitWall.point - direction * 0.2f);
            
            Debug.Log($"Add wall point {hitWall.point}");

        }
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        
        if(spawningTimer > 0) return;

        switch (currentBehaviourStateIndex)
        {
            //ROAMING
            case 0:
            {
                
                TargetClosestPlayer(requireLineOfSight: true, viewWidth: visionWidth);
    
                if (targetPlayer == null)
                {
                    if (createPortalTimer <= 0 && false)
                    {
                        if(alreadyCreatedPortal) break;
                        
                        Debug.Log($"Create Portal {createPortalTimer} {isGoingToPortal} {savedWallPosition.Count}");
                        if (!isGoingToPortal && savedWallPosition.Count > 1)
                        {
                            var pos = savedWallPosition[Random.Range(0, savedWallPosition.Count)];
                            StopSearch(currentSearch);

                            SetDestinationToPosition(pos);
             
                            lastPortalPosition = pos;
                            isGoingToPortal = true;
                        
                            break;
                        }

                        if (isGoingToPortal && agent.remainingDistance <= agent.stoppingDistance)
                        {
                            Debug.Log("Going Portal");
                            
                            var pos = savedWallPosition[Random.Range(0, savedWallPosition.Count)];
               
                            GoToPortalServerRpc(transform.position, pos);
                            alreadyCreatedPortal = true;
                            break;
                        }
                        
                        break;
                    }
                    
                    Debug.Log("Search");
                    if (currentSearch.inProgress) break;
                    AISearchRoutine aiSearchRoutine = new AISearchRoutine();
                    aiSearchRoutine.searchWidth = 100f;
                    aiSearchRoutine.searchPrecision = 8f;
                    StartSearch(ChooseFarthestNodeFromPosition(transform.position, true).position, aiSearchRoutine);

                }
                else if (PlayerIsTargetable(targetPlayer))
                {
                    chaseTimer = chaseDelay;
                    SwitchToBehaviourState(1);
                }
                break;
                
            }
            case 1:
            {
                if (chaseTimer <= 0f)
                {
                    TargetClosestPlayer(requireLineOfSight: true, viewWidth: visionWidth);
                    if (targetPlayer != null)
                    {
                        chaseTimer += 1f;
                    }
                    else
                    {
                        SwitchToBehaviourState(0);
                    }
                    
                }
                else if (targetPlayer != null && PlayerIsTargetable(targetPlayer))
                {
                    SetMovingTowardsTargetPlayer(targetPlayer);
                }
   
                break;
                
            }
            case 2:
            {
                StopSearch(currentSearch);
                transform.position = SCP106Plugin.instance.actualDimensionObjectManager.spawnPosition.position;
                SyncPositionToClients();
                break;
            }
        }
        
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

    [ServerRpc]
    public void CreatePortalServerRpc(Vector3 position, Vector3 connectedPosition, bool goToPos = false)
    {
        CreatePortalClientRpc(position, connectedPosition, goToPos );
    }

    [ClientRpc]
    public void CreatePortalClientRpc(Vector3 position, Vector3 connectedPosition, bool goToPos = false)
    {
        
        if (PortalObject != null)
        {
            var portal = Instantiate(PortalObject, position, Quaternion.identity);
            portal.transform.LookAt(position);
            portal.transform.eulerAngles = new Vector3(0f, portal.transform.eulerAngles.y, 0f);
            portal.GetComponent<SCP106Portal>().connectedPos = connectedPosition;
            if (goToPos)
            {
                agent.SetDestination(position);
                createPortalTimer = createPortalDelay;
            }
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
        CreatePortalServerRpc(pos, connectedPosition: nextPos);
        CreatePortalServerRpc(nextPos, connectedPosition: pos);
        
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
                agent.speed = runSpeed;
                creatureAnimator.SetBool(Run, true);
                break;
            }
            case 2:
            {
                creatureAnimator.SetBool(Run, true);
                break;
            }
        }
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        creatureAnimator.SetTrigger(Punch);
        var player = MeetsStandardPlayerCollisionConditions(other, false, true);
        if (player != null && hitTimer <= 0)
        {
            hitTimer = hitDelay;
            if (player.health >= 40)
            {
                player.DamagePlayer(30, causeOfDeath: CauseOfDeath.Kicking);
            }
            else
            {
                SCP106Plugin.instance.InstantiateDimension();
                player.transform.position = SCP106Plugin.instance.actualDimensionObjectManager.spawnPosition.position;
                playersIdsInDimension.Add(player.playerSteamId);
            }
        }
    }
}