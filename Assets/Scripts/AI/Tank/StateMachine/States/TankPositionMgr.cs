using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TankPositionMgr : MonoBehaviour
{
    public static Vector3 SharedTargetPosition = Vector3.zero;

    // 用于重置共享目标位置的方法
    // 获取玩家坐标
    Vector3 targetPosition = m_TankSM.Target.position;
    // 分配AI坐标，基于玩家坐标
    
    public static void ResetSharedTargetPosition()
    {
        SharedTargetPosition = Vector3.zero;
    }
}
