using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>StrongAttackState</c> defines the tank's behavior during a strong attack phase.
    /// In this state, tanks will engage the target with powerful attacks and attempt to form a coordinated 
    /// formation. The tanks will periodically check for obstacles and adjust their paths accordingly.
    /// Tanks can also dodge obstacles, and if the target goes out of range, they will switch to a patrolling state.
    /// The state also handles launching projectiles towards the target at calculated intervals.
    /// </summary>
    internal class StrongAttackState : BaseState
    {
        private TankSM m_TankSM;  // Reference to the tank state machine.
        private Coroutine fireCoroutine; // Coroutine reference for firing projectiles.
        private Coroutine formationCoroutine; // Coroutine reference for maintaining the formation.

        private const float TriangleSideLength = 10f;  // Side length of the triangular formation.

        private bool isDodging;         // Indicates if the tank is currently dodging an obstacle.
        private const float RaycastDistance = 5f; // Distance for obstacle detection.
        private static List<TankSM> tanksInStrongAttackState = new List<TankSM>(); 
        private const float FormationSideLength = 10f;  // Side length of the formation.
        private static readonly object stateLock = new object();  // Lock object for thread safety.


        /// <summary>
        /// Constructor <c>StrongAttackState</c> initializes the tank state machine.
        /// </summary>
        public StrongAttackState(TankSM tankStateMachine) : base("StrongAttack", tankStateMachine)
        {
            m_TankSM = tankStateMachine;

        }

        /// <summary>
        /// Method <c>Enter</c> is called when the tank enters the StrongAttackState.
        /// It initiates the attack sequence, sets up the formation, and starts necessary coroutines.
        /// </summary>
        public override void Enter()
        {
            base.Enter();
            m_TankSM.SetStopDistanceToZero();

            //Debug.Log($"Tank {m_TankSM.name} is entering StrongAttackState");

            lock (stateLock)
            {
                if (!tanksInStrongAttackState.Contains(m_TankSM))
                {
                    tanksInStrongAttackState.Add(m_TankSM);
                    //Debug.Log($"Tank {m_TankSM.name} entered StrongAttackState, total tanks: {tanksInStrongAttackState.Count}");
                    AssignFormation();
                }
            }

            // start shooting coroutine
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
            }

            // start formation coroutine
            if (formationCoroutine != null)
            {
                m_TankSM.StopCoroutine(formationCoroutine);
            }
            formationCoroutine = m_TankSM.StartCoroutine(UpdateFormation());
        }

        /// <summary>
        /// Method <c>Update</c> is called every frame to handle behavior while in StrongAttackState.
        /// The tank continues to aim at the target, dodge obstacles, and maintain formation.
        /// </summary>
        public override void Update()
        {
            base.Update();


            // Check if the target is still within range
            if (m_TankSM.Target != null)
            {
                Vector3 playerPosition = m_TankSM.Target.position;


                float distance = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                if (distance > m_TankSM.TargetDistance * 1.5) // If target is out of range, transition to another state (e.g., Patrolling)
                {
                    //Debug.Log("strong attack: Target out of range. Transitioning to Patrolling state.");
                    m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                }
                else
                {
                    Vector3 directionToTarget = m_TankSM.Target.position - m_TankSM.transform.position;
                    directionToTarget.y = 0; // Ignore the y-axis for rotation
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    // Smoothly rotate the tank towards the target, using the OrientSlerpScalar for interpolation
                    float rotationSpeed = m_TankSM.OrientSlerpScalar * m_TankSM.NavMeshAgent.angularSpeed;
                    // rotate the tank but subject to the rotation speed
                    m_TankSM.transform.rotation = Quaternion.Slerp(m_TankSM.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                    // Detect obstacles using raycasting
                    if (DetectObstacle())
                    {
                        // If an obstacle is detected, start dodging
                        if (!isDodging)
                        {
                            isDodging = true;
                            // Stop shooting while dodging
                            if (fireCoroutine != null)
                            {
                                m_TankSM.StopCoroutine(fireCoroutine);
                                fireCoroutine = null;
                            }
                            StartDodging();
                        }
                    }
                    else
                    {

                    }
                }
            }
        }

        /// <summary>
        /// Method <c>AssignFormation</c> assigns tanks to specific positions in a triangular formation based on 
        /// their current state and health status.
        /// </summary>
        private void AssignFormation()
        {
            
            List<TankSM> tanksInAttackOrStrongAttack = new List<TankSM>();

            lock (stateLock)
            {
                foreach (TankSM tank in tanksInStrongAttackState)
                {
                    if (tank.GetCurrentState() is AttackState || tank.GetCurrentState() is StrongAttackState)
                    {
                        tanksInAttackOrStrongAttack.Add(tank);
                    }
                }
            }

            //List<TankSM> availableTanks = new List<TankSM>();
            foreach (TankSM tank in tanksInStrongAttackState)
            {
                if (tank != null && tank.NavMeshAgent != null && tank.NavMeshAgent.isActiveAndEnabled && !tank.GetComponent<TankHealth>().IsDead)
                {
                    tanksInAttackOrStrongAttack.Add(tank);
                }
            }

            Vector3 centerPosition = m_TankSM.Target.position;

            float deltaAngle = 360.0f / tanksInAttackOrStrongAttack.Count;

            for (int i = 0; i < tanksInAttackOrStrongAttack.Count; i++)
            {
                Vector3 pos = centerPosition + new Vector3(
                    TriangleSideLength * Mathf.Cos(Mathf.Deg2Rad * (i * deltaAngle)),
                    0,
                    TriangleSideLength * Mathf.Sin(Mathf.Deg2Rad * (i * deltaAngle))
                );

                   tanksInAttackOrStrongAttack[i].NavMeshAgent.SetDestination(pos);
               
            }
        }

        /// <summary>
        /// Coroutine <c>UpdateFormation</c> periodically updates the tank's formation every 0.5 seconds.
        /// </summary>
        private IEnumerator UpdateFormation()
        {
            while (true)
            {
                AssignFormation();
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>
        /// Coroutine <c>FireAtTarget</c> handles the tank's firing behavior while in StrongAttackState.
        /// The tank launches projectiles towards the target at regular intervals, adjusting the launch force based on distance.
        /// </summary>
        private IEnumerator FireAtTarget()
        {
            float initialWait = m_TankSM.FireInterval.x;
            yield return new WaitForSeconds(initialWait);

            while (true)
            {
                if (m_TankSM.Target != null && !DetectObstacle())
                {
                    Vector3 directionToTarget = m_TankSM.Target.position - m_TankSM.transform.position;
                    directionToTarget.y = 0;

                    float distanceToTarget = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                     float gravity = Mathf.Abs(Physics.gravity.y);
                    float angleInRadians = Mathf.Deg2Rad * 45; // Use a 45 degree angle for optimal distance

                    // Calculate the required launch velocity based on the projectile motion formula
                    float launchForce = Mathf.Sqrt((distanceToTarget * gravity) / Mathf.Sin(2 * angleInRadians));

                    float variation = Random.Range(-0.05f, 0.25f) * launchForce; // variations to make sure the target is not hidding
                    launchForce += variation;

                    // Clamp the launch force to respect the min/max values, hey! we are following the rules!!!
                    launchForce = Mathf.Clamp(launchForce, m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y);

                    m_TankSM.LaunchProjectile(launchForce);
                }

                float waitInSec = m_TankSM.FireInterval.x;
                yield return new WaitForSeconds(waitInSec);
            }
        }

        /// <summary>
        /// Method <c>DetectObstacle</c> uses raycasting to detect obstacles in front of the tank and 
        /// returns true if an obstacle is found, prompting the tank to dodge.
        /// </summary>
        private bool DetectObstacle()
        {
            RaycastHit[] hits;
            // Perform a raycast in the direction the tank is facing
            hits = Physics.RaycastAll(m_TankSM.transform.position, m_TankSM.transform.forward, RaycastDistance);

            foreach (RaycastHit hit in hits)
            {
                // If the raycast hits an obstacle with a collider
                if (hit.collider != null)
                {
                    // Check if the hit object is not the target and not the tank itself
                    if (hit.collider.gameObject != m_TankSM.Target.gameObject && hit.collider.gameObject != m_TankSM.gameObject)
                    {
                        // Check if it's a friend tank
                        if (hit.collider.GetComponent<TankSM>() != null)
                        {
                            // Debug.Log("Friend tank detected along the path: " + hit.collider.name);
                            return true; // Friend tank is along the path
                        }

                        // Check if it's an obstacle
                        // Debug.Log("Obstacle detected: " + hit.collider.name);
                        return true; // Obstacle detected
                    }
                }
            }

            return false;
        }

         private void StartDodging()
        {
            // Calculate a random direction to dodge
            Vector3 dodgeDirection = Random.insideUnitSphere;
            dodgeDirection.y = 0; // Only move in the XZ plane
            dodgeDirection.Normalize();

            // Set a new destination to dodge to
            Vector3 dodgeDestination = m_TankSM.transform.position + dodgeDirection * 20f; // Adjust the dodge distance as needed
            m_TankSM.NavMeshAgent.SetDestination(dodgeDestination);
            // dest = dodgeDestination;
        }

        /// <summary>
        /// Method <c>Exit</c> is called when the tank exits the StrongAttackState.
        /// It stops any ongoing coroutines and removes the tank from the list of tanks in the strong attack state.
        /// </summary>
        public override void Exit()
        {
            base.Exit();

            lock (stateLock)
            {
                if (tanksInStrongAttackState.Contains(m_TankSM))
                {
                    tanksInStrongAttackState.Remove(m_TankSM);
                    //Debug.Log($"Tank {m_TankSM.name} exited StrongAttackState, remaining tanks: {tanksInStrongAttackState.Count}");
                }
            }

            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
            }
            if (formationCoroutine != null)
            {
                m_TankSM.StopCoroutine(formationCoroutine);
                formationCoroutine = null;
            }
        }
    }
}
