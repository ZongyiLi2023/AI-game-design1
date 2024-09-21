using UnityEngine;

namespace CE6127.Tanks.AI
{
    internal class FleeState : BaseState
    {
        private TankSM m_TankSM;
        private const float fleeDistance = 50f;  // 逃跑的距离
        private bool hasRotated = false;  // 标记是否已经完成旋转
        private Quaternion targetRotation;  // 目标旋转方向
        private bool isFleeing = false;  // 标记是否已经开始逃跑
        private float fleeTimer = 0f;  // 逃跑的时间计时器
        private const float fleeDuration = 6f;  // 逃跑持续时间
        private const float raycastDistance = 10f;  // 射线检测距离
        private const float rotationAngleOnObstacle = 90f;  // 遇到障碍时的旋转角度
        private LayerMask obstacleLayer;  // 用于检测障碍物的层

        public FleeState(TankSM tankStateMachine) : base("Flee", tankStateMachine)
        {
            m_TankSM = tankStateMachine;
            obstacleLayer = LayerMask.GetMask("Obstacle");  // 假设障碍物使用的层名为"Obstacle"
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log($"[FleeState] Tank {m_TankSM.name} is fleeing.");
            m_TankSM.SetStopDistanceToZero();  // 确保坦克不会在逃跑时停下

            // 计算远离玩家的方向
            Vector3 directionAwayFromPlayer = m_TankSM.transform.position - m_TankSM.Target.position;
            directionAwayFromPlayer.Normalize();  // 归一化方向向量

            // 随机选择左转还是右转 90 度
            float rotationAngle = Random.value > 0.5f ? 90f : -90f;

            // 计算目标旋转方向
            targetRotation = Quaternion.AngleAxis(rotationAngle, Vector3.up) * Quaternion.LookRotation(directionAwayFromPlayer);

            // 开始旋转
            hasRotated = false;
            isFleeing = false;
            fleeTimer = 0f;  // 重置逃跑时间
        }

        public override void Update()
        {
            base.Update();

            // 先进行旋转
            if (!hasRotated)
            {
                RotateTank();
                return;
            }

            // 如果旋转完成且还没有开始逃跑，开始计算逃跑目标
            if (hasRotated && !isFleeing)
            {
                Vector3 fleeTarget = m_TankSM.transform.position + m_TankSM.transform.forward * fleeDistance;
                m_TankSM.NavMeshAgent.SetDestination(fleeTarget);
                isFleeing = true;
                Debug.Log($"[FleeState] Tank {m_TankSM.name} has rotated and is now fleeing towards {fleeTarget}");
            }

            // 进行逃跑时的射线检测
            CheckForObstacles();

            // 计算逃跑时间
            if (isFleeing)
            {
                fleeTimer += Time.deltaTime;
                Debug.Log($"[FleeState] Tank {m_TankSM.name} fleeing for {fleeTimer:F2} seconds");

                // 逃跑持续一段时间后切换回巡逻状态
                if (fleeTimer >= fleeDuration)
                {
                    Debug.Log($"[FleeState] Tank {m_TankSM.name} fleeing duration reached. Switching to StrongAttack.");
                    //m_TankSM.ChangeState(m_TankSM.m_States.Patrolling);  // 切换回强攻状态
                    m_TankSM.ChangeState(m_TankSM.m_States.StrongAttack); 
                }
            }
        }

        private void RotateTank()
        {
            // 平滑旋转坦克
            m_TankSM.transform.rotation = Quaternion.RotateTowards(m_TankSM.transform.rotation, targetRotation, m_TankSM.NavMeshAgent.angularSpeed * Time.deltaTime);

            // 检查是否完成旋转
            if (Quaternion.Angle(m_TankSM.transform.rotation, targetRotation) < 0.1f)
            {
                hasRotated = true;  // 标记旋转完成
                Debug.Log($"[FleeState] Tank {m_TankSM.name} has rotated, ready to flee.");
            }
        }

        // 射线检测障碍物
        private void CheckForObstacles()
        {
            Ray ray = new Ray(m_TankSM.transform.position, m_TankSM.transform.forward);
            RaycastHit hit;

            // 如果射线检测到障碍物
            if (Physics.Raycast(ray, out hit, raycastDistance, obstacleLayer))
            {
                Debug.Log($"[FleeState] Tank {m_TankSM.name} detected obstacle: {hit.collider.name}. Avoiding...");

                // 随机选择左转或右转来避开障碍物
                float rotationAngle = Random.value > 0.5f ? rotationAngleOnObstacle : -rotationAngleOnObstacle;

                // 计算新的旋转方向
                targetRotation = Quaternion.AngleAxis(rotationAngle, Vector3.up) * m_TankSM.transform.rotation;

                // 立即调整坦克的目标位置
                Vector3 newFleeTarget = m_TankSM.transform.position + m_TankSM.transform.forward * fleeDistance;
                m_TankSM.NavMeshAgent.SetDestination(newFleeTarget);
            }
        }

        public override void Exit()
        {
            base.Exit();
            Debug.Log($"[FleeState] Tank {m_TankSM.name} is exiting Flee state.");
        }
    }
}
