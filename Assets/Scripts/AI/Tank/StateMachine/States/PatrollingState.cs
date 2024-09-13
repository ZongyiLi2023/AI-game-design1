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

        public PatrollingState(TankSM tankStateMachine) : base("Patrolling", tankStateMachine)
        {
            m_TankSM = (TankSM)m_StateMachine;
        }

        public override void Enter()
        {
            base.Enter();
            m_TankSM.SetStopDistanceToZero();

            AssignRandomSection();  // 分配坦克的巡逻区域
            m_TankSM.StartCoroutine(Patrolling());  // 开始巡逻
        }

        // 随机分配巡逻区域
        private void AssignRandomSection()
        {
            float mapMinX = -50f;
            float mapMaxX = 50f;
            float sectionWidth = (mapMaxX - mapMinX) / 3;

            int randomSection = Random.Range(0, 3);  // 随机选择分区
            // 怎么防止重复选择同一个分区？
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
        private bool fireAllowed = true;

        public override void Update()
        {
            base.Update();

            // 如果有玩家目标则持续追踪玩家
            if (m_TankSM.Target != null)
            {
                // 计算坦克和玩家之间的距离
                float dist = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);

                // 将计算出的距离传递给 FollowPlayer 方法
                FollowPlayer(dist);

                // 当子弹发射的Cooldown到期了则发射子弹
                if (dist <= m_TankSM.StopDistance && fireAllowed)
                {
                    fireAllowed = false;
                    m_TankSM.StartCoroutine(FireCooldown());
                    /*float launchForce = Random.Range(m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y);*/
                    /*m_TankSM.LaunchProjectile(launchForce);*/
                }
                return;
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
    /*else
    {
        Vector3 targetPosition = GetRandomPositionAroundPlayer();
        m_TankSM.NavMeshAgent.SetDestination(targetPosition);
    }*/
}


        // 在玩家周围生成随机目标位置
        private Vector3 GetRandomPositionAroundPlayer()
        {
            Vector2 randomOffset = Random.insideUnitCircle * 12f;
            Vector3 randomPosition = new Vector3(m_TankSM.Target.position.x + randomOffset.x, m_TankSM.Target.position.y, m_TankSM.Target.position.z + randomOffset.y);
            return randomPosition;
        }

        public override void Exit()
        {
            base.Exit();
            m_TankSM.StopCoroutine(Patrolling());
        }

        IEnumerator Patrolling()
        {
            while (true)
            {
                // 如果有玩家则追踪玩家
                if (m_TankSM.Target != null)
                {
                    //
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

        IEnumerator FireCooldown()
        {
            /*            float waitInSec = Random.Range(m_TankSM.FireInterval.x, m_TankSM.FireInterval.y);
            */
            float waitInSec = 0.5f;
            yield return new WaitForSeconds(waitInSec);
            fireAllowed = true;
            yield break;
        }
    }
}