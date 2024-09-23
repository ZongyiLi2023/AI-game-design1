using UnityEngine;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>FleeState</c> represents the behavior of the tank when it is in the process of fleeing from the player. 
    /// This state controls the tank's movement away from the target, avoids obstacles, and transitions into 
    /// another state after a specified flee duration. The tank rotates away from the player and flees in a 
    /// straight line while checking for obstacles, adjusting its path if necessary. The state transitions 
    /// to another behavior (like StrongAttack) after the flee duration has expired.
    /// </summary>
    internal class FleeState : BaseState
    {
        private TankSM m_TankSM; // Reference to the tank's state machine
        private const float fleeDistance = 50f;   // Distance to flee in the chosen direction
        private bool hasRotated = false;    // Indicates whether the tank has completed its rotation away from the target
        private Quaternion targetRotation;   // The target rotation direction for the tank
        private bool isFleeing = false;  // Indicates whether the tank has started fleeing
        private float fleeTimer = 0f;  // Timer to track the flee duration
        private const float fleeDuration = 6f;   // Duration the tank will flee before changing state
        private const float raycastDistance = 10f;  // Distance for detecting obstacles in front of the tank
        private const float rotationAngleOnObstacle = 90f;    // Rotation angle to turn when an obstacle is detected
        private LayerMask obstacleLayer;  // Layer mask used for detecting obstacles in the tank's path

        /// <summary>
        /// Constructor <c>FleeState</c> initializes the state machine with references to the tank.
        /// </summary>
        public FleeState(TankSM tankStateMachine) : base("Flee", tankStateMachine)
        {
            m_TankSM = tankStateMachine;
            obstacleLayer = LayerMask.GetMask("Obstacle"); 
        }


        /// <summary>
        /// Method <c>Enter</c> is called when the tank enters the FleeState. It sets up the flee direction, 
        /// calculates rotation away from the player, and prepares for the fleeing behavior.
        /// </summary>
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

        /// <summary>
        /// Method <c>Update</c> is called every frame to handle the fleeing behavior. It rotates the tank, 
        /// moves it away from the target, and checks for obstacles to avoid. If the flee duration is 
        /// exceeded, the state changes to StrongAttack.
        /// </summary>
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

        /// <summary>
        /// Method <c>RotateTank</c> rotates the tank away from the player based on the calculated targetRotation.
        /// </summary>
        private void RotateTank()
        {
            
            m_TankSM.transform.rotation = Quaternion.RotateTowards(m_TankSM.transform.rotation, targetRotation, m_TankSM.NavMeshAgent.angularSpeed * Time.deltaTime);

            
            if (Quaternion.Angle(m_TankSM.transform.rotation, targetRotation) < 0.1f)
            {
                hasRotated = true;  
                //Debug.Log($"[FleeState] Tank {m_TankSM.name} has rotated, ready to flee.");
            }
        }

        /// <summary>
        /// Method <c>CheckForObstacles</c> uses raycasting to detect obstacles in front of the tank and 
        /// adjusts its path to avoid collisions by altering its rotation and flee direction.
        /// </summary>
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

        /// <summary>
        /// Method <c>Exit</c> is called when the tank exits the FleeState.
        /// </summary>
        public override void Exit()
        {
            base.Exit();
            //Debug.Log($"[FleeState] Tank {m_TankSM.name} is exiting Flee state.");
        }
    }
}
