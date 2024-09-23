﻿using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine.UI;

namespace CE6127.Tanks.AI
{
    /// <summary>
    /// Class <c>AttackState</c> represents the state of the tank when it is attacking. 
    /// This state controls the tank's behavior, such as its firing pattern, obstacle avoidance, and formation 
    /// when three tanks are in the AttackState. It also manages the transition to other states based on 
    /// tank health or target distance. This class uses reflection to access private fields, handles firing 
    /// with coroutine control, and implements basic pathfinding using Unity's NavMesh system.
    /// </summary>
    internal class AttackState : BaseState
    {
        private TankSM m_TankSM;        // Reference to the tank state machine.
        private Coroutine fireCoroutine; // Reference to the firing coroutine.
        private const float RaycastDistance = 5f; // Distance for obstacle detection.

        private object tankHealthInstance;  
        private FieldInfo currentHealthField; 
        private float maxHealth;  



        private static List<TankSM> tanksInAttackState = new List<TankSM>();  // Static list to track tanks in AttackState
        private const float TriangleSideLength = 10f;   // Length of the side of the triangle for tank formation
        private static readonly object stateLock = new object(); // Lock to prevent concurrency issues when adding/removing tanks


        /// <summary>
        /// Constructor <c>AttackState</c> initializes the state machine and sets up initial parameters.
        /// </summary>
        public AttackState(TankSM tankStateMachine) : base("Attack", tankStateMachine)
        {
            m_TankSM = (TankSM)m_StateMachine;


        }

        /// <summary>
        /// Method <c>Enter</c> is called when the tank enters the AttackState. It sets up health reflection, starts firing, 
        /// and adds the tank to the formation. 
        /// </summary>
        public override void Enter()
        {
            base.Enter();
            m_TankSM.SetStopDistanceToZero(); // Ensure the tank stops at the correct distance from the target

            lock (stateLock)
            {
                if (!tanksInAttackState.Contains(m_TankSM)) 
                {
                    tanksInAttackState.Add(m_TankSM);
                }
                else
                {
                    // Debug.LogWarning($"Tank {m_TankSM.name} already in AttackState.");
                }
            }

            // Use reflection to get access to the tank's current health
            var tankHealthType = typeof(TankHealth);
            tankHealthInstance = m_TankSM.GetComponent(tankHealthType);

            if (tankHealthInstance != null)
            {
                
                currentHealthField = tankHealthType.GetField("m_CurrentHealth", BindingFlags.NonPublic | BindingFlags.Instance);
                maxHealth = (float)tankHealthType.GetField("StartingHealth").GetValue(tankHealthInstance);
               // Debug.Log("get the health of the tank");
            }
            else
            {
                // Debug.LogError("TankHealth component not found on the tank.");
            }



            // Start the firing coroutine only if it's not already running
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
            }
            fireCoroutine = m_TankSM.StartCoroutine(FireAtTarget());
        }

        /// <summary>
        /// Method <c>Update</c> is called every frame while the tank is in AttackState. It checks the tank's health, 
        /// transitions to other states if needed, and ensures the tank is targeting and firing correctly.
        /// </summary>
        public override void Update()
        {
            base.Update();
            if (tankHealthInstance != null && currentHealthField != null)
            {
                // Reflectively check the current health and transition to FleeState if health is critically low
                float currentHealth = (float)currentHealthField.GetValue(tankHealthInstance);
                //Debug.Log("the health can be got");

                if (currentHealth <= maxHealth* 0.2f)
                {
                    //Debug.Log("change to flee state");
                    m_StateMachine.ChangeState(new FleeState(m_TankSM)); // Transition to FleeState when health is low

                    if (fireCoroutine != null)
                    {
                        m_TankSM.StopCoroutine(fireCoroutine); // Stop firing when fleeing
                        fireCoroutine = null;
                    }
                    return;
                }
            }



            // Check if the target is still within range; otherwise transition to Patrolling state
            if (m_TankSM.Target != null)
            {
                Vector3 playerPosition = m_TankSM.Target.position;


                float distance = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                if (distance > 35.0f) // If target is out of range, transition to another state (e.g., Patrolling)
                {
                    //Debug.Log("Target out of range. Transitioning to Patrolling state.");
                    m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                }
                else
                {
                    // Smoothly rotate towards the target
                    Vector3 directionToTarget = m_TankSM.Target.position - m_TankSM.transform.position;
                    directionToTarget.y = 0; // Ignore the y-axis for rotation
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    // Smoothly rotate the tank towards the target, using the OrientSlerpScalar for interpolation
                    float rotationSpeed = m_TankSM.OrientSlerpScalar * m_TankSM.NavMeshAgent.angularSpeed;
                    // rotate the tank but subject to the rotation speed
                    m_TankSM.transform.rotation = Quaternion.Slerp(m_TankSM.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }
            // Assign triangle formation to the tanks in AttackState
            AssignTriangleFormation();
        }


        /// <summary>
        /// Method <c>Exit</c> is called when the tank exits the AttackState. It stops firing and removes the tank from the AttackState list.
        /// </summary>
        public override void Exit()
        {
            base.Exit();

            lock (stateLock)
            {
                if (tanksInAttackState.Contains(m_TankSM))
                {
                    tanksInAttackState.Remove(m_TankSM); // Remove tank from the AttackState list
                    //Debug.Log($"Tank {m_TankSM.name} exited AttackState, remaining tanks: {tanksInAttackState.Count}");
                }
                else
                {
                    // Debug.LogWarning($"Tank {m_TankSM.name} was not found in AttackState list.");
                }
            }


            // Stop the firing coroutine when exiting the state
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
            }
        }


        /// <summary>
        /// Method <c>AssignTriangleFormation</c> assigns tanks in the AttackState to a triangle formation based on their position around a target.
        /// </summary>
        private void AssignTriangleFormation()
        {
            // Determine the available tanks (those not dead)
            List<TankSM> availableTanks = new List<TankSM>();
            foreach (TankSM tank in tanksInAttackState)
            {
                if (tank != null && tank.GetCurrentState().Name == "Attack" && tank.GetComponent<TankHealth>() != null && !tank.GetComponent<TankHealth>().IsDead)
                {
                    availableTanks.Add(tank); // Add tanks that are still alive and in AttackState
                }
            }

            // Debug.Log("Available tanks: " + availableTanks.Count);

            Vector3 centerPosition = m_TankSM.Target.position;

            float deltaAngle = 360.0f / availableTanks.Count;

            // Assign each available tank its position in the triangle formation
            for (int i = 0; i < availableTanks.Count; i++)
            {
                Vector3 pos = centerPosition + new Vector3(TriangleSideLength * Mathf.Cos(Mathf.Deg2Rad * (i * deltaAngle)), 0, TriangleSideLength * Mathf.Sin(Mathf.Deg2Rad * (i * deltaAngle)));
                availableTanks[i].NavMeshAgent.SetDestination(pos);
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
                // Debug.Log("AttackState Coroutine FireAtTarget Called");
                // Make sure the tank faces the target before firing
                if (m_TankSM.Target != null && !DetectObstacle())
                {
                    Vector3 directionToTarget = m_TankSM.Target.position - m_TankSM.transform.position;
                    directionToTarget.y = 0; // Ignore the y-axis for rotation

                    // Calculate the distance to target
                    float distanceToTarget = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);

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
                    // Debug.Log("force: " + launchForce);
                }

                // Use the cooldown time from TankSM's FireInterval
                float waitInSec = m_TankSM.FireInterval.x;
                yield return new WaitForSeconds(waitInSec);
            }
        }

        /// <summary>
        /// Method <c>DetectObstacle</c> uses raycasting to detect obstacles or friend tanks in front of the tank.
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




    }
}