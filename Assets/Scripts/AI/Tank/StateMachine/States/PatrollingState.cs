using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CE6127.Tanks.AI
{
    internal class PatrollingState : BaseState
    {
        private TankSM m_TankSM;        // Reference to the tank state machine.
        private Vector3 m_Destination;  // Destination for the tank to move to.
        private float PatrolMinX;
        private float PatrolMaxX;
        private float PatrolMinZ;
        private float PatrolMaxZ;

        private Coroutine patrolCoroutine = null; // Reference to the patrol coroutine.

        public PatrollingState(TankSM tankStateMachine) : base("Patrolling", tankStateMachine)
        {
            m_TankSM = (TankSM)m_StateMachine;
        }

        public override void Enter()
        {
            base.Enter();
            m_TankSM.SetStopDistanceToZero();

            AssignRandomSection();  // 分配坦克的巡逻区域
            patrolCoroutine = m_TankSM.StartCoroutine(Patrolling());  // 开始巡逻
        }

        // 随机分配巡逻区域
        private void AssignRandomSection()
        {
            float mapMinX = -50f;
            float mapMaxX = 50f;
            float sectionWidth = (mapMaxX - mapMinX) / 3;

            int randomSection = Random.Range(0, 3);  // 随机选择分区
            switch (randomSection)
            {
                case 0:
                    PatrolMinX = mapMinX;
                    PatrolMaxX = mapMinX + sectionWidth;
                    break;
                case 1:
                    PatrolMinX = mapMinX + sectionWidth;
                    PatrolMaxX = mapMinX + sectionWidth * 2;
                    break;
                case 2:
                    PatrolMinX = mapMinX + sectionWidth * 2;
                    PatrolMaxX = mapMaxX;
                    break;
            }

            PatrolMinZ = -50f;
            PatrolMaxZ = 50f;
        }

        // private float patrolWaitTimeCounter = 0f;

        public override void Update()
        {
            base.Update();

            // 如果有玩家目标则持续追踪玩家
            if (m_TankSM.Target != null)
            {
                // 计算坦克和玩家之间的距离
                float dist = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);

                // 如果距离到达指定距离则转移状态
                if (dist <= m_TankSM.TargetDistance)
                {
                    m_TankSM.ChangeState(m_TankSM.m_States.Attack);
                }
                else
                {
                    // 将计算出的距离传递给 FollowPlayer 方法
                    FollowPlayer(dist);
                }
            }
        }


        // 生成新的巡逻目标
        private void GenerateNewPatrolDestination()
        {
            float destinationX = Random.Range(PatrolMinX, PatrolMaxX);
            float destinationZ = Random.Range(PatrolMinZ, PatrolMaxZ);
            m_Destination = new Vector3(destinationX, 0f, destinationZ);

            // 设置坦克的巡逻目标
            m_TankSM.NavMeshAgent.SetDestination(m_Destination);
        }


        // 追踪玩家的逻辑
        private void FollowPlayer(float distance)
        {
            float closerStopDistance = m_TankSM.StopDistance / 2;  // 减小停止距离的一半
            if (distance > closerStopDistance)  // 如果距离大于新的停止距离
            {
                m_TankSM.NavMeshAgent.SetDestination(m_TankSM.Target.position);
            }
        }


        // 在玩家周围生成随机目标位置
        /*
        private Vector3 GetRandomPositionAroundPlayer()
        {
            Vector2 randomOffset = Random.insideUnitCircle * 12f;
            Vector3 randomPosition = new Vector3(m_TankSM.Target.position.x + randomOffset.x, m_TankSM.Target.position.y, m_TankSM.Target.position.z + randomOffset.y);
            return randomPosition;
        }
        */

        public override void Exit()
        {
            base.Exit();
            m_TankSM.StopCoroutine(patrolCoroutine);
        }

        IEnumerator Patrolling()
        {
            while (true)
            {
                // 如果有玩家则追踪玩家
                if (m_TankSM.Target != null)
                {
                    m_TankSM.NavMeshAgent.SetDestination(m_TankSM.Target.position);
                }
                // 否则游走
                else
                {
                    float destinationX = Random.Range(PatrolMinX, PatrolMaxX);
                    float destinationZ = Random.Range(PatrolMinZ, PatrolMaxZ);
                    m_Destination = new Vector3(destinationX, 0f, destinationZ);

                    m_TankSM.NavMeshAgent.SetDestination(m_Destination);

                }

                float waitInSec = Random.Range(m_TankSM.PatrolWaitTime.x, m_TankSM.PatrolWaitTime.y);
                yield return new WaitForSeconds(waitInSec);
            }
        }
    }
}