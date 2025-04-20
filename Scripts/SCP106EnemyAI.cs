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
    public GameObject PortalObject;

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
    
    private float aiInterval = 0.2f;
    public override void Start()
    {

        base.Start();

        agent.speed = 3.5f;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;
    }

    public override void Update()
    {
        base.Update();
        
        if(!IsServer) return;
        

        
        createPortalTimer -= Time.deltaTime;
        saveWallPosTimer -= Time.deltaTime;
        aiInterval -= Time.deltaTime;
        
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

        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
    
                if (targetPlayer == null)
                {
                    if (createPortalTimer <= 0)
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
                break;
                
            }
            case 1:
            {
                if (IsServer)
                {

                }
   
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
    
    
}