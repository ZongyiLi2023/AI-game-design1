
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Reflection;
using System.Collections.Generic;


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



        private object tankHealthInstance;  // 保存TankHealth实例
        private FieldInfo currentHealthField; // 保存反射获取的m_CurrentHealth字段
        private float maxHealth;  // 保存最大血量



        private static List<TankSM> tanksInAttackState = new List<TankSM>(); // 追踪所有进入 AttackState 的坦克
        private const float TriangleSideLength = 10f;  // 三角形的边长
        private static readonly object stateLock = new object(); // 防止并发问题


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

            Debug.Log($"Tank {m_TankSM.name} is entering AttackState");
        
            lock (stateLock)
            {
                if (!tanksInAttackState.Contains(m_TankSM)) // 确保不会重复加入
                {
                    tanksInAttackState.Add(m_TankSM);
                    Debug.Log($"Tank {m_TankSM.name} entered AttackState, total tanks: {tanksInAttackState.Count}");

                    // 当三个坦克都进入攻击状态后，计算队形
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

            // 使用反射获取 TankHealth 实例
            var tankHealthType = typeof(TankHealth);
            tankHealthInstance = m_TankSM.GetComponent(tankHealthType);

            if (tankHealthInstance != null)
            {
                // 通过反射获取 m_CurrentHealth 私有字段
                currentHealthField = tankHealthType.GetField("m_CurrentHealth", BindingFlags.NonPublic | BindingFlags.Instance);

                // 获取最大血量，直接访问公开的 StartingHealth 字段
                maxHealth = (float)tankHealthType.GetField("StartingHealth").GetValue(tankHealthInstance);
            }
            else
            {
                Debug.LogError("TankHealth component not found on the tank.");
            }



            // Start the firing coroutine only if it's not already running
            if (fireCoroutine == null)
            {
                fireCoroutine = m_TankSM.StartCoroutine(FireAtTarget());
            }
        }

        /// <summary>
        /// Method <c>Update</c> is called every frame.
        /// </summary>
        public override void Update()
        {
            base.Update();


            if (tankHealthInstance != null && currentHealthField != null)
            {
                // 每帧通过反射获取当前血量
                float currentHealth = (float)currentHealthField.GetValue(tankHealthInstance);

                // 检查血量是否小于最大血量的三分之一
                if (currentHealth <= maxHealth / 3)
                {
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
                if (distance > m_TankSM.TargetDistance * 1.5) // If target is out of range, transition to another state (e.g., Patrolling)
                {
                    Debug.Log("Target out of range. Transitioning to Patrolling state.");
                    m_StateMachine.ChangeState(m_TankSM.m_States.Patrolling);
                }
                else
                {
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
                        // If no obstacles, stop dodging and resume normal attack behavior
                        if (isDodging)
                        {
                            isDodging = false;
                            // Resume shooting
                            if (fireCoroutine == null)
                            {
                                fireCoroutine = m_TankSM.StartCoroutine(FireAtTarget());
                            }
                        }
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
                    Debug.Log($"Tank {m_TankSM.name} exited AttackState, remaining tanks: {tanksInAttackState.Count}");
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
                Debug.LogWarning("Not enough tanks in AttackState to form a triangle.");
                return;
            }

            Debug.Log("Assigning triangle formation...");

            // 选定第一个坦克作为队形的中心
            TankSM centerTank = tanksInAttackState[0];
            Vector3 centerPosition = centerTank.transform.position;

            Debug.Log($"Center tank position: {centerPosition}");

            // 计算第二和第三个坦克的位置（形成一个等边三角形）
            Vector3 pos1 = centerPosition + new Vector3(TriangleSideLength, 0, 0); // 右边
            Vector3 pos2 = centerPosition + new Vector3(-TriangleSideLength / 2, 0, Mathf.Sqrt(3) * TriangleSideLength / 2); // 左上
            Vector3 pos3 = centerPosition + new Vector3(-TriangleSideLength / 2, 0, -Mathf.Sqrt(3) * TriangleSideLength / 2); // 左下

            // 输出每个计算出的目标位置
            Debug.Log($"Position 1: {pos1}, Position 2: {pos2}, Position 3: {pos3}");

            // 分配位置并打印调试信息
            tanksInAttackState[0].NavMeshAgent.SetDestination(pos1);
            Debug.Log($"Tank {tanksInAttackState[0].name} moving to position {pos1}");
            tanksInAttackState[1].NavMeshAgent.SetDestination(pos2);
            Debug.Log($"Tank {tanksInAttackState[1].name} moving to position {pos2}");
            tanksInAttackState[2].NavMeshAgent.SetDestination(pos3);
            Debug.Log($"Tank {tanksInAttackState[2].name} moving to position {pos3}");

            // 输出最终结果
            Debug.Log("Triangle formation assigned successfully.");
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


        /// <summary>
        /// Method <c>StartDodging</c> starts the dodging movement to avoid the obstacle.
        /// </summary>
        private void StartDodging()
        {
            // Calculate a random direction to dodge
            Vector3 dodgeDirection = Random.insideUnitSphere;
            dodgeDirection.y = 0; // Only move in the XZ plane
            dodgeDirection.Normalize();

            // Set a new destination to dodge to
            Vector3 dodgeDestination = m_TankSM.transform.position + dodgeDirection * 20f; // Adjust the dodge distance as needed
            m_TankSM.NavMeshAgent.SetDestination(dodgeDestination);
        }




    }
}
