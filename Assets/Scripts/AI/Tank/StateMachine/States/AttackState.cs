using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Reflection;


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



        private int tankIndex;     // 坦克的编号，用于队形分配
        private const float FormationRadius = 5f; // 队形的半径
        private static int tankCount = 0; // 追踪当前有多少坦克进入状态


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


            // 为坦克分配队形编号
            tankIndex = tankCount % 3; // 分配编号 0, 1, 2
            tankCount++;


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

                // 计算坦克相对玩家的位置
                Vector3 relativePosition = CalculatePositionAroundPlayer(playerPosition, tankIndex);

                // 让坦克移动到计算出的相对位置
                m_TankSM.NavMeshAgent.SetDestination(relativePosition);





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

            // Stop the firing coroutine
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
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


        private Vector3 CalculatePositionAroundPlayer(Vector3 playerPosition, int tankIndex)
        {
            // 计算每个坦克的角度，分别为0度、120度、240度
            float angle = tankIndex * 120f;
            float radians = angle * Mathf.Deg2Rad;

            // 使用队形半径计算坦克相对玩家的位置
            float xOffset = FormationRadius * Mathf.Cos(radians);
            float zOffset = FormationRadius * Mathf.Sin(radians);

            // 返回相对位置
            return new Vector3(playerPosition.x + xOffset, playerPosition.y, playerPosition.z + zOffset);
        }

    }
}
