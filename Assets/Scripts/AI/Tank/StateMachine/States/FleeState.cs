using UnityEngine;

namespace CE6127.Tanks.AI
{
    internal class FleeState : BaseState
    {
        private TankSM m_TankSM;
        private const float fleeDistance = 50f;  // 逃跑的距离
        private bool hasRotated = false;  // 标记是否已经完成旋转
        private Quaternion targetRotation;  // 目标旋转方向

        public FleeState(TankSM tankStateMachine) : base("Flee", tankStateMachine)
        {
            m_TankSM = tankStateMachine;
        }

        public override void Enter()
        {
            base.Enter();
            Debug.Log("FleeState: Tank is fleeing.");
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

            // 如果已经旋转完毕，则计算逃跑目标并逃跑
            if (m_TankSM.NavMeshAgent.remainingDistance < m_TankSM.StopDistance)
            {
                Debug.Log("FleeState: Tank is moving after rotation.");
                // 计算新的逃跑目标
                Vector3 fleeTarget = m_TankSM.transform.position + m_TankSM.transform.forward * fleeDistance;
                m_TankSM.NavMeshAgent.SetDestination(fleeTarget);
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
                Debug.Log("FleeState: Tank has rotated, now fleeing.");
            }
        }

        public override void Exit()
        {
            base.Exit();
            Debug.Log("FleeState: Exiting Flee state.");
        }
    }
}
