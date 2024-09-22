using UnityEngine;

namespace CE6127.Tanks.AI
{
    internal class FleeState : BaseState
    {
        private TankSM m_TankSM;
        private const float fleeDistance = 50f;  
        private bool hasRotated = false;  
        private Quaternion targetRotation;  
        private bool isFleeing = false;  
        private float fleeTimer = 0f;  
        private const float fleeDuration = 6f;  
        private const float raycastDistance = 10f; 
        private const float rotationAngleOnObstacle = 90f;  
        private LayerMask obstacleLayer;  

        public FleeState(TankSM tankStateMachine) : base("Flee", tankStateMachine)
        {
            m_TankSM = tankStateMachine;
            obstacleLayer = LayerMask.GetMask("Obstacle"); 
        }

        public override void Enter()
        {
            base.Enter();
            //Debug.Log($"[FleeState] Tank {m_TankSM.name} is fleeing.");
            m_TankSM.SetStopDistanceToZero();  

        
            Vector3 directionAwayFromPlayer = m_TankSM.transform.position - m_TankSM.Target.position;
            directionAwayFromPlayer.Normalize();  

            float rotationAngle = Random.value > 0.5f ? 90f : -90f;

            
            targetRotation = Quaternion.AngleAxis(rotationAngle, Vector3.up) * Quaternion.LookRotation(directionAwayFromPlayer);

            
            hasRotated = false;
            isFleeing = false;
            fleeTimer = 0f;  
        }

        public override void Update()
        {
            base.Update();

        
            if (!hasRotated)
            {
                RotateTank();
                return;
            }

            
            if (hasRotated && !isFleeing)
            {
                Vector3 fleeTarget = m_TankSM.transform.position + m_TankSM.transform.forward * fleeDistance;
                m_TankSM.NavMeshAgent.SetDestination(fleeTarget);
                isFleeing = true;
                //Debug.Log($"[FleeState] Tank {m_TankSM.name} has rotated and is now fleeing towards {fleeTarget}");
            }

            
            CheckForObstacles();

            
            if (isFleeing)
            {
                fleeTimer += Time.deltaTime;
                //Debug.Log($"[FleeState] Tank {m_TankSM.name} fleeing for {fleeTimer:F2} seconds");

                
                if (fleeTimer >= fleeDuration)
                {
                    //Debug.Log($"[FleeState] Tank {m_TankSM.name} fleeing duration reached. Switching to StrongAttack.");
                    //m_TankSM.ChangeState(m_TankSM.m_States.Patrolling);  
                    m_TankSM.ChangeState(m_TankSM.m_States.StrongAttack); 
                }
            }
        }

        private void RotateTank()
        {
            
            m_TankSM.transform.rotation = Quaternion.RotateTowards(m_TankSM.transform.rotation, targetRotation, m_TankSM.NavMeshAgent.angularSpeed * Time.deltaTime);

            
            if (Quaternion.Angle(m_TankSM.transform.rotation, targetRotation) < 0.1f)
            {
                hasRotated = true;  
                //Debug.Log($"[FleeState] Tank {m_TankSM.name} has rotated, ready to flee.");
            }
        }


        private void CheckForObstacles()
        {
            Ray ray = new Ray(m_TankSM.transform.position, m_TankSM.transform.forward);
            RaycastHit hit;

            
            if (Physics.Raycast(ray, out hit, raycastDistance, obstacleLayer))
            {
                //Debug.Log($"[FleeState] Tank {m_TankSM.name} detected obstacle: {hit.collider.name}. Avoiding...");

                
                float rotationAngle = Random.value > 0.5f ? rotationAngleOnObstacle : -rotationAngleOnObstacle;

                
                targetRotation = Quaternion.AngleAxis(rotationAngle, Vector3.up) * m_TankSM.transform.rotation;

               
                Vector3 newFleeTarget = m_TankSM.transform.position + m_TankSM.transform.forward * fleeDistance;
                m_TankSM.NavMeshAgent.SetDestination(newFleeTarget);
            }
        }

        public override void Exit()
        {
            base.Exit();
            //Debug.Log($"[FleeState] Tank {m_TankSM.name} is exiting Flee state.");
        }
    }
}
