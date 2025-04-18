namespace SCP106.Scripts;

public class SCP106EnemyAI: EnemyAI
{

    public override void Start()
    {

        base.Start();

        agent.speed = 3.5f;
        agent.acceleration = 255f;
        agent.angularSpeed = 900f;
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
                    if (currentSearch.inProgress) break;
                    AISearchRoutine aiSearchRoutine = new AISearchRoutine();
                    aiSearchRoutine.searchWidth = 50f;
                    aiSearchRoutine.searchPrecision = 8f;
                    StartSearch(ChooseFarthestNodeFromPosition(transform.position, true).position, aiSearchRoutine);
                }
                break;
                
            }
        }
        
    }
    
}