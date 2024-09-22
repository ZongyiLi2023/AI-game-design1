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
    /// </summary>
    internal class AttackState : BaseState
    {
        private TankSM m_TankSM;        // Reference to the tank state machine.
        private Coroutine fireCoroutine; // Reference to the firing coroutine.
        private bool isDodging;         // Indicates if the tank is currently dodging an obstacle.
        private const float RaycastDistance = 5f; // Distance for obstacle detection.

        private object tankHealthInstance;  
        private FieldInfo currentHealthField; 
        private float maxHealth;  



        private static List<TankSM> tanksInAttackState = new List<TankSM>(); 
        private const float TriangleSideLength = 10f;  
        private static readonly object stateLock = new object(); 




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
            // m_TankSM.SetStopDistanceToTarget(); // Ensure the tank stops at the correct distance from the target
            m_TankSM.SetStopDistanceToZero(); // Ensure the tank stops at the correct distance from the target

            //Debug.Log($"Tank {m_TankSM.name} is entering AttackState");

            lock (stateLock)
            {
                if (!tanksInAttackState.Contains(m_TankSM)) 
                {
                    tanksInAttackState.Add(m_TankSM);
                    //Debug.Log($"Tank {m_TankSM.name} entered AttackState, total tanks: {tanksInAttackState.Count}");

                    
                    if (tanksInAttackState.Count == 3)
                    {
                        AssignTriangleFormation();
                    }
                }
                else
                {
                    Debug.LogWarning($"Tank {m_TankSM.name} already in AttackState.");
                }
            }

           
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
                Debug.LogError("TankHealth component not found on the tank.");
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
        /// Method <c>Update</c> is called every frame.
        /// </summary>
        public override void Update()
        {
            base.Update();
            // Debug.Log("AttackState Update");

     
            if (tanksInAttackState.Count == 3)
            {
                Vector3 destination1 = tanksInAttackState[0].NavMeshAgent.destination;
                Vector3 destination2 = tanksInAttackState[1].NavMeshAgent.destination;
                Vector3 destination3 = tanksInAttackState[2].NavMeshAgent.destination;

                //tankPositionsText.text = $"Tank 1 Destination: {destination1}\nTank 2 Destination: {destination2}\nTank 3 Destination: {destination3}";
            }


            if (tankHealthInstance != null && currentHealthField != null)
            {
                
                float currentHealth = (float)currentHealthField.GetValue(tankHealthInstance);
                //Debug.Log("the health can be got");

                if (currentHealth <= maxHealth* 0.8f)
                {
                    //Debug.Log("change to flee state");
                    m_StateMachine.ChangeState(new FleeState(m_TankSM));
                    
                    if (fireCoroutine != null)
                    {
                        m_TankSM.StopCoroutine(fireCoroutine);
                        fireCoroutine = null;
                    }
                    return;
                }
            }



            // Check if the target is still within range
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

            lock (stateLock)
            {
                if (tanksInAttackState.Contains(m_TankSM))
                {
                    tanksInAttackState.Remove(m_TankSM);
                    //Debug.Log($"Tank {m_TankSM.name} exited AttackState, remaining tanks: {tanksInAttackState.Count}");
                }
                else
                {
                    Debug.LogWarning($"Tank {m_TankSM.name} was not found in AttackState list.");
                }
            }


            // Stop the firing coroutine
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
            }
        }

        private void AssignTriangleFormation()
        {
            if (tanksInAttackState.Count < 3)
            {
                //Debug.LogWarning("Not enough tanks in AttackState to form a triangle.");
                return;
            }

            // Debug.Log("Assigning triangle formation...");

            
            // TankSM centerTank = tanksInAttackState[0];
            Vector3 centerPosition = m_TankSM.Target.position;

            //Debug.Log($"Center tank position: {centerPosition}");

           
            Vector3 pos1 = centerPosition + new Vector3(TriangleSideLength, 0, 0); // 右边
            Vector3 pos2 = centerPosition + new Vector3(-TriangleSideLength / 2, 0, Mathf.Sqrt(3) * TriangleSideLength / 2); // 左上
            Vector3 pos3 = centerPosition + new Vector3(-TriangleSideLength / 2, 0, -Mathf.Sqrt(3) * TriangleSideLength / 2); // 左下



            Vector3 destination1 = tanksInAttackState[0].NavMeshAgent.destination;
            Vector3 destination2 = tanksInAttackState[1].NavMeshAgent.destination;
            Vector3 destination3 = tanksInAttackState[2].NavMeshAgent.destination;
            
            //Debug.Log($"Position 1: {pos1}, Position 2: {pos2}, Position 3: {pos3}");
          
            //tankPositionsText.text = $"Tank 1 Destination: {destination1}\nTank 2 Destination: {destination2}\nTank 3 Destination: {destination3}";

          
            tanksInAttackState[0].NavMeshAgent.SetDestination(pos1);
            // tanksInAttackState[0].m_States.Attack.dest = pos1;
            // Debug.Log($"Tank {tanksInAttackState[0].name} moving to position {pos1}");
            tanksInAttackState[1].NavMeshAgent.SetDestination(pos2);
            // tanksInAttackState[1].m_States.Attack.dest = pos2;
            // Debug.Log($"Tank {tanksInAttackState[1].name} moving to position {pos2}");
            tanksInAttackState[2].NavMeshAgent.SetDestination(pos3);
            // tanksInAttackState[2].m_States.Attack.dest = pos3;
            // Debug.Log($"Tank {tanksInAttackState[2].name} moving to position {pos3}");

            // Debug.Log("Triangle formation assigned successfully.");

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

                    // Move towards the target if the distance is greater than the stop distance
                    float distanceToTarget = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                    /* if (distanceToTarget > m_TankSM.StopDistance)
                    {
                        m_TankSM.NavMeshAgent.SetDestination(m_TankSM.Target.position);
                    }
                    else
                    {
                        // Stop the tank if within stopping distance
                        Debug.Log("Stopping the tank.");
                        // m_TankSM.NavMeshAgent.ResetPath();
                    } */

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