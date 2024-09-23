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
            m_TankSM.SetStopDistanceToTarget();

            AssignRandomSection();  
            if (patrolCoroutine != null)
            {
                m_TankSM.StopCoroutine(patrolCoroutine);
                patrolCoroutine = null;
            }
            patrolCoroutine = m_TankSM.StartCoroutine(Patrolling());  
        }

        
        private void AssignRandomSection()
        {
            float mapMinX = -50f;
            float mapMaxX = 50f;
            float sectionWidth = (mapMaxX - mapMinX) / 3;

            int randomSection = Random.Range(0, 3);  
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

            
            if (m_TankSM.Target != null)
            {
                
                float dist = Vector3.Distance(m_TankSM.transform.position, m_TankSM.Target.position);

                
                if (dist <= m_TankSM.TargetDistance)
                {
                    m_TankSM.ChangeState(m_TankSM.m_States.Attack);
                }
                else
                {
                
                    FollowPlayer(dist);
                }
            }
        }


        
        private void GenerateNewPatrolDestination()
        {
            float destinationX = Random.Range(PatrolMinX, PatrolMaxX);
            float destinationZ = Random.Range(PatrolMinZ, PatrolMaxZ);
            m_Destination = new Vector3(destinationX, 0f, destinationZ);

            
            m_TankSM.NavMeshAgent.SetDestination(m_Destination);
        }


        
        private void FollowPlayer(float distance)
        {
            float closerStopDistance = m_TankSM.StopDistance / 2;  
            if (distance > closerStopDistance)  
            {
                m_TankSM.NavMeshAgent.SetDestination(m_TankSM.Target.position);
            }
        }


    
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
            if (patrolCoroutine != null)
            {
                m_TankSM.StopCoroutine(patrolCoroutine);
                patrolCoroutine = null;
            }
        }

        IEnumerator Patrolling()
        {
            while (true)
            {
                //Debug.Log("PatrollingState Coroutine Patrolling Called");
                
                if (m_TankSM.Target != null)
                {
                    m_TankSM.NavMeshAgent.SetDestination(m_TankSM.Target.position);
                }
                
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