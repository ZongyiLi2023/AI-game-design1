using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CE6127.Tanks.AI
{
    internal class AttackState : BaseState
    {
        private TankSM m_TankSM;        // Reference to the tank state machine.

        public AttackState(TankSM tankStateMachine) : base("Attack", tankStateMachine)
        {
            m_TankSM = (TankSM)m_StateMachine;
            m_TankSM.StartCoroutine(Fire());
        }

        public override void Enter()
        {
            base.Enter();
        }

        public override void Update()
        {
            base.Update();

        }

        public override void Exit()
        {
            base.Exit();
            m_TankSM.StopCoroutine(Fire());
        }

        IEnumerator Fire()
        {
            while (true)
            {
                float launchForce = Random.Range(m_TankSM.LaunchForceMinMax.x, m_TankSM.LaunchForceMinMax.y);
                Debug.Log("Fire" + launchForce);
                m_TankSM.LaunchProjectile(launchForce);

                float waitInSec = Random.Range(m_TankSM.PatrolWaitTime.x, m_TankSM.PatrolWaitTime.y);
                yield return new WaitForSeconds(waitInSec);
            }
        }
    }
}