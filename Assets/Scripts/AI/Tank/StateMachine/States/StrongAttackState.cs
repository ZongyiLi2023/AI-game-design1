using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

namespace CE6127.Tanks.AI
{
    internal class StrongAttackState : BaseState
    {
        private TankSM m_TankSM;
        private Coroutine fireCoroutine;
        private Coroutine formationCoroutine;

        private bool isDodging;         // Indicates if the tank is currently dodging an obstacle.
        private const float RaycastDistance = 5f; // Distance for obstacle detection.
        private static List<TankSM> tanksInStrongAttackState = new List<TankSM>(); // 追踪所有进入 StrongAttackState 的坦克
        private const float FormationSideLength = 10f;  // 队形的边长

        private static readonly object stateLock = new object(); // 防止并发问题

        private GameObject uiCanvas;
        private Text tankPositionsText;

        public StrongAttackState(TankSM tankStateMachine) : base("StrongAttack", tankStateMachine)
        {
            m_TankSM = tankStateMachine;

            // 找到UI Canvas
            uiCanvas = GameObject.Find("TankCanvas");
            if (uiCanvas == null)
            {
                Debug.LogError("TankCanvas not found! Make sure it exists in the scene.");
                return;
            }

            // 动态创建Text对象
            GameObject newTextObj = new GameObject("DynamicTankPositionsText");
            tankPositionsText = newTextObj.AddComponent<Text>();

            // 设置Text的基本属性
            tankPositionsText.text = " ";
            tankPositionsText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            tankPositionsText.fontSize = 24;
            tankPositionsText.color = Color.white;
            tankPositionsText.alignment = TextAnchor.UpperLeft;

            // 将Text对象作为子对象附加到Canvas
            newTextObj.transform.SetParent(uiCanvas.transform, false);
            RectTransform rectTransform = newTextObj.GetComponent<RectTransform>();
            rectTransform.anchoredPosition = new Vector2(0, 0);
            rectTransform.sizeDelta = new Vector2(800, 400);
        }

        public override void Enter()
        {
            base.Enter();
            m_TankSM.SetStopDistanceToZero();

            Debug.Log($"Tank {m_TankSM.name} is entering StrongAttackState");

            lock (stateLock)
            {
                if (!tanksInStrongAttackState.Contains(m_TankSM))
                {
                    tanksInStrongAttackState.Add(m_TankSM);
                    Debug.Log($"Tank {m_TankSM.name} entered StrongAttackState, total tanks: {tanksInStrongAttackState.Count}");

                    // 根据坦克数量计算队形
                    AssignFormation();
                }
            }

            // 启动射击协程
            if (fireCoroutine != null)
            {
                m_TankSM.StopCoroutine(fireCoroutine);
                fireCoroutine = null;
            }
            fireCoroutine = m_TankSM.StartCoroutine(FireAtTarget());

            // 启动队形更新协程
            if (formationCoroutine != null)
            {
                m_TankSM.StopCoroutine(formationCoroutine);
            }
            formationCoroutine = m_TankSM.StartCoroutine(UpdateFormation());
        }

        public override void Update()
        {
            base.Update();

            // if (tanksInStrongAttackState.Count >= 2)
            // {
            //     Vector3 destination1 = tanksInStrongAttackState[0].NavMeshAgent.destination;
            //     Vector3 destination2 = tanksInStrongAttackState[1].NavMeshAgent.destination;

            //     if (tanksInStrongAttackState.Count == 3)
            //     {
            //         Vector3 destination3 = tanksInStrongAttackState[2].NavMeshAgent.destination;
            //         tankPositionsText.text = $"Tank 1 Destination: {destination1}\nTank 2 Destination: {destination2}\nTank 3 Destination: {destination3}";
            //     }
            //     else
            //     {
            //         tankPositionsText.text = $"Tank 1 Destination: {destination1}\nTank 2 Destination: {destination2}";
            //     }
            // }

            if (tanksInStrongAttackState.Count == 3)
            {
                Vector3 destination1 = tanksInStrongAttackState[0].NavMeshAgent.destination;
                Vector3 destination2 = tanksInStrongAttackState[1].NavMeshAgent.destination;
                Vector3 destination3 = tanksInStrongAttackState[2].NavMeshAgent.destination;
                tankPositionsText.text = $"strong attack: Tank 1 Destination: {destination1}\nTank 2 Destination: {destination2}\nTank 3 Destination: {destination3}";

            }

            // Check if the target is still within range
            if (m_TankSM.Target != null)
            {
                Vector3 playerPosition = m_TankSM.Target.position;


                float distance = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);
                if (distance > m_TankSM.TargetDistance * 1.5) // If target is out of range, transition to another state (e.g., Patrolling)
                {
                    Debug.Log("strong attack: Target out of range. Transitioning to Patrolling state.");
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
                    if (false)
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
                        /*if (isDodging)
                        {
                            isDodging = false;
                            // Resume shooting
                            if (fireCoroutine == null)
                            {
                                fireCoroutine = m_TankSM.StartCoroutine(FireAtTarget());
                            }
                        }*/
                        // turn the tank to face the target

                    }
                }
            }
        }

        private void AssignFormation()
        {
            // 获取所有处于 AttackState 或 StrongAttackState 的坦克
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

            if (tanksInAttackOrStrongAttack.Count < 2)
            {
                Debug.LogWarning("Not enough tanks in AttackState or StrongAttackState to form a formation.");
                return;
            }

            Vector3 centerPosition = m_TankSM.Target.position;

            if (tanksInAttackOrStrongAttack.Count == 2)
            {
                // 两个坦克，形成一条直线
                Vector3 pos1 = centerPosition + new Vector3(FormationSideLength, 0, 0); // 右边
                Vector3 pos2 = centerPosition + new Vector3(-FormationSideLength, 0, 0); // 左边

                tanksInAttackOrStrongAttack[0].NavMeshAgent.SetDestination(pos1);
                tanksInAttackOrStrongAttack[1].NavMeshAgent.SetDestination(pos2);
                Debug.LogWarning("the 2 tanks are in a line.");
            }
            else if (tanksInAttackOrStrongAttack.Count >= 3)
            {
                Vector3 centerPosition2 = m_TankSM.Target.position;

                Debug.Log($"Center tank position: {centerPosition2}");

                // 三个或更多坦克，形成一个等边三角形
                Vector3 pos1 = centerPosition2 + new Vector3(FormationSideLength, 0, 0); // 右边
                Vector3 pos2 = centerPosition2 + new Vector3(-FormationSideLength / 2, 0, Mathf.Sqrt(3) * FormationSideLength / 2); // 左上
                Vector3 pos3 = centerPosition2 + new Vector3(-FormationSideLength / 2, 0, -Mathf.Sqrt(3) * FormationSideLength / 2); // 左下

                tanksInAttackOrStrongAttack[0].NavMeshAgent.SetDestination(pos1);
                tanksInAttackOrStrongAttack[1].NavMeshAgent.SetDestination(pos2);
                tanksInAttackOrStrongAttack[2].NavMeshAgent.SetDestination(pos3);
                Debug.LogWarning("the 3 tanks are in a triangle.");
            }
        }


        private IEnumerator UpdateFormation()
        {
            while (true)
            {
                AssignFormation();
                yield return new WaitForSeconds(0.5f);
            }
        }

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
                    // float launchForce = CalculateLaunchForce(distanceToTarget);
                    // m_TankSM.LaunchProjectile(launchForce);
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

        // private float CalculateLaunchForce(float distance)
        // {
        //     float gravity = Mathf.Abs(Physics.gravity.y);
        //     float angleInRadians = Mathf.Deg2Rad * 45;
        //     float launchForce = Mathf.Sqrt((distance * gravity) / Mathf.Sin(2 * angleInRadians));
        //     return Mathf.Clamp(launchForce, m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y);
        // }

        // private bool DetectObstacle()
        // {
        //     RaycastHit[] hits = Physics.RaycastAll(m_TankSM.transform.position, m_TankSM.transform.forward, 5f);
        //     foreach (var hit in hits)
        //     {
        //         if (hit.collider != null && hit.collider.gameObject != m_TankSM.Target.gameObject)
        //         {
        //             return true;
        //         }
        //     }
        //     return false;
        // }

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

        public override void Exit()
        {
            base.Exit();

            lock (stateLock)
            {
                if (tanksInStrongAttackState.Contains(m_TankSM))
                {
                    tanksInStrongAttackState.Remove(m_TankSM);
                    Debug.Log($"Tank {m_TankSM.name} exited StrongAttackState, remaining tanks: {tanksInStrongAttackState.Count}");
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
