using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

/// <summary>
/// Enemy 壳类：字段/方法实现位于基类 MonsterActor（Assets/Scripts/Combat/Actors/MonsterActor.cs）。
/// 保留本类以维持敌人 prefab 组件引用与 GetComponent&lt;Enemy&gt; 调用方的兼容。
/// </summary>
public class Enemy : MonsterActor
{
    [Header("Possession")]
    [Tooltip("Optional point where the SoulActor is attached while this enemy is possessed.")]
    public Transform soulAnchorPoint;

    void OnEnable()
    {
        EnemyRegistry.Register(this);   // 活跃注册（池化怪 Spawn 激活时注册；索敌/技能扫描读 Registry 替代全场景扫描）
    }

    void OnDisable()
    {
        EnemyRegistry.Unregister(this); // 回池/销毁注销（SetActive(false) 触发）
    }
}
