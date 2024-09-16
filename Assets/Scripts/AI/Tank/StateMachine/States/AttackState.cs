using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>AttackState</c> represents the state of the tank when it is attacking.
    /// </summary>
    internal class AttackState : BaseState
    {
        private TankSM m_TankSM;        // Reference to the tank state machine.
        private Coroutine fireCoroutine; // Reference to the firing coroutine.

        /// <summary>
        /// Constructor <c>AttackState</c> constructor.
        /// </summary>
        public AttackState(TankSM tankStateMachine) : base("Attack", tankStateMachine)
        {
            m_TankSM = (TankSM)m_StateMachine;
        }

        /// <summary>
        /// Method <c>Enter</c> is called when the state is entered.
        /// </summary>
        public override void Enter()
        {
            base.Enter();
            m_TankSM.SetStopDistanceToTarget(); // Ensure the tank stops at the correct distance from the target

            // Start the firing coroutine
            fireCoroutine = m_TankSM.StartCoroutine(FireAtTarget());
        }

        /// <summary>
        /// Method <c>Update</c> is called every frame.
        /// </summary>
        public override void Update()
        {
            base.Update();

            // Check if the target is still within range
            if (m_TankSM.Target != null)
            {
                float distance = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                if (distance > m_TankSM.TargetDistance * 1.5) // If target is out of range, transition to another state (e.g., Patrolling)
                {
                    Debug.Log("Target out of range. Transitioning to Patrolling state.");
                    m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                }
                else
                {
                    Debug.Log("turning");
                    // turn the tank to face the target
                    Vector3 directionToTarget = m_TankSM.Target.position - m_TankSM.transform.position;
                    directionToTarget.y = 0; // Ignore the y-axis for rotation
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    // Smoothly rotate the tank towards the target, using the OrientSlerpScalar for interpolation
                    float rotationSpeed = m_TankSM.OrientSlerpScalar * m_TankSM.NavMeshAgent.angularSpeed;
                    // rotate the tank but subject to the rotation speed
                    m_TankSM.transform.rotation = Quaternion.Slerp(m_TankSM.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
        }

        /// <summary>
        /// Method <c>Exit</c> is called when exiting the state.
        /// </summary>
        public override void Exit()
        {
            base.Exit();

            // Stop the firing coroutine
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
            }
        }

        /// <summary>
        /// Coroutine <c>FireAtTarget</c> handles the tank's firing behavior while in the AttackState.
        /// </summary>
        private IEnumerator FireAtTarget()
        {
            // Initial cooldown before firing the first shot
            float initialWait = m_TankSM.FireInterval.x;
            yield return new WaitForSeconds(initialWait);

            while (true)
            {
                // Make sure the tank faces the target before firing
                if (m_TankSM.Target != null)
                {
                    Vector3 directionToTarget = m_TankSM.Target.position - m_TankSM.transform.position;
                    directionToTarget.y = 0; // Ignore the y-axis for rotation

                    // Move towards the target if the distance is greater than the stop distance
                    float distanceToTarget = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                    if (distanceToTarget > m_TankSM.StopDistance)
                    {
                        m_TankSM.NavMeshAgent.SetDestination(m_TankSM.Target.position);
                    }
                    else
                    {
                        // Stop the tank if within stopping distance
                        m_TankSM.NavMeshAgent.ResetPath();
                    }

/*                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);

                    // Smoothly rotate the tank towards the target, using the OrientSlerpScalar for interpolation
                    float rotationSpeed = m_TankSM.OrientSlerpScalar * m_TankSM.NavMeshAgent.angularSpeed;

                    // Gradually rotate the tank over multiple frames until facing the target
                    while (Quaternion.Angle(m_TankSM.transform.rotation, targetRotation) > 0.1f)
                    {
                        m_TankSM.transform.rotation = Quaternion.Slerp(m_TankSM.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                        yield return null; // Wait for the next frame
                    }
*/
                    // Once the tank is facing the target, calculate the required launch force
                    float gravity = Mathf.Abs(Physics.gravity.y);
                    float angleInRadians = Mathf.Deg2Rad * 45; // Use a 45 degree angle for optimal distance

                    // Calculate the required launch velocity based on the projectile motion formula
                    float launchForce = Mathf.Sqrt((distanceToTarget * gravity) / Mathf.Sin(2 * angleInRadians));

                    float variation = Random.Range(-0.05f, 0.25f) * launchForce; // variations to make sure the target is not hidding
                    launchForce += variation;

                    // Clamp the launch force to respect the min/max values, hey! we are following the rules!!!
                    launchForce = Mathf.Clamp(launchForce, m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y);

                    m_TankSM.LaunchProjectile(launchForce);
                    Debug.Log("force: " + launchForce);
                }

                // Use the cooldown time from TankSM's FireInterval
                float waitInSec = m_TankSM.FireInterval.x;
                yield return new WaitForSeconds(waitInSec);
            }
        }
    }
}
