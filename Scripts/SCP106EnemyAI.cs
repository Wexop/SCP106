using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SCP106.Scripts;

public class SCP106EnemyAI: EnemyAI
{
    public GameObject PortalObject;

    private float wallDistanceToSpawnPortal = 10f;
    private Vector3 lastPortalPosition;
    
    public LayerMask layerRoom;
    
    private float createPortalDelay = 5f;
    private float createPortalTimer = 30f;

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
            SavePortalPosition(hitWall.point);
            
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
                        if (!isGoingToPortal && savedWallPosition.Count > 1)
                        {
                            var pos = savedWallPosition[Random.Range(0, savedWallPosition.Count)];
                            StopSearch(currentSearch);
                            currentSearch = null;
                            
                            moveTowardsDestination = true;
                            movingTowardsTargetPlayer = false;
                            destination = pos;
             
                            lastPortalPosition = pos;
                            isGoingToPortal = true;
                        
                            break;
                        }

                        if (isGoingToPortal && Vector3.Distance(transform.position, lastPortalPosition) < 1f)
                        {
                            isGoingToPortal = false;
                            SwitchToBehaviourState(1);
                            break;
                        }
                    }

                    
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
                    if (!alreadyCreatedPortal)
                    {
                        CreatePortalServerRpc(lastPortalPosition);
                        alreadyCreatedPortal = true;
                    }
                   
                    
                    if (Vector3.Distance(eye.position, lastPortalPosition) < wallDistanceToSpawnPortal)
                    {
                        
                        var pos = savedWallPosition[Random.Range(0, savedWallPosition.Count)];
                        CreatePortalServerRpc(pos);
                        transform.position = pos;
                        SyncPositionToClients();
                        
                        SwitchToBehaviourState(0);
                        alreadyCreatedPortal = false;
                    }
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
    public void CreatePortalServerRpc(Vector3 position, bool goToPos = false)
    {
        CreatePortalClientRpc(position, goToPos);
    }

    [ClientRpc]
    public void CreatePortalClientRpc(Vector3 pos, bool goToPos)
    {
        
        if (PortalObject != null)
        {
            var portal = Instantiate(PortalObject, pos, Quaternion.identity);
            portal.transform.LookAt(pos);
            portal.transform.eulerAngles = new Vector3(0f, portal.transform.eulerAngles.y, 0f);
            if (goToPos)
            {
                agent.SetDestination(pos);
                createPortalTimer = createPortalDelay;
            }
        }
    }
    
}