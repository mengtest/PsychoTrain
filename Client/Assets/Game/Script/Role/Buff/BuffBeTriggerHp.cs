﻿#region Header
/**
 * 名称：被动血量触发
 * 描述：
血量百分比,状态列表,作用对象,释放者
当别人对角色的扣血导致血量降到一定值以下的时候，注意是给自己加而且状态开始的时候如果已经达到这个条件，那么马上触发
注意这里是被别人攻击的时候判断
 **/
#endregion
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;


public class BuffBeTriggerHpCfg : BuffExCfg
{
    public LvValue value;
    public List<int> buffIds = new List<int>();
    public enBuffTargetType targetType = enBuffTargetType.self;
    public enBuffTargetType sourceType = enBuffTargetType.self;


    public override bool Init(string[] pp)
    {
        if (pp.Length < 2)
            return false;

        //血量百分比
        value = new LvValue(pp[0]);
        if (value.error)
            return false;

        //状态列表
        int i = 0;
        if (int.TryParse(pp[1], out i))
            buffIds.Add(i);
        else
        {
            if (!StringUtil.TryParse(pp[1].Split('|'), ref buffIds))
                return false;
        }

        //作用对象
        if (pp.Length > 2 && int.TryParse(pp[2], out i))
            targetType = (enBuffTargetType)i;
        //释放者
        if (pp.Length > 3 && int.TryParse(pp[3], out i))
            sourceType = (enBuffTargetType)i;
        return true;
    }
    public override void PreLoad()
    {
        for (int i = 0; i < buffIds.Count; ++i)
        {
            BuffCfg.ProLoad(buffIds[i]);
        }
    }
}


public class BuffBeTriggerHp : Buff
{
    public BuffBeTriggerHpCfg ExCfg { get { return (BuffBeTriggerHpCfg)m_cfg.exCfg; } }
    bool m_trigger = false;//是不是已经触发过
    int m_observer;


    //初始化，状态创建的时候调用，一般用来解析下参数
    public override void OnBuffInit()
    {
        m_trigger = false;

    }

    //处理，可能会调用多次
    public override void OnBuffHandle()
    {
        if (m_count > 1)
        {
            Debuger.LogError("被动血量触发状态不需要执行多次，状态id:{0}", m_cfg.id);
            return;
        }
        //监听技能事件
        m_observer = m_parent.Add(MSG_ROLE.BEHIT, OnEvent);

        //如果是自己，马上判断下
        if (ExCfg.targetType == enBuffTargetType.self)
        {
            float percent = Parent.GetPercent(enProp.hp, enProp.hpMax);
            float v = this.GetLvValue(ExCfg.value);
            if (percent < v)
            {
                m_trigger = true;
                BuffPart buffPart = m_parent.BuffPart;
                for (int i = 0; i < ExCfg.buffIds.Count; ++i)
                {
                    buffPart.AddBuff(ExCfg.buffIds[i], m_parent);
                }
            }
        }

    }

    //结束
    public override void OnBuffStop(bool isClear)
    {
        if (m_observer != EventMgr.Invalid_Id) { EventMgr.Remove(m_observer); m_observer = EventMgr.Invalid_Id; }
        m_trigger = false;
    }


    void OnEvent(object p, object p2, object p3, EventObserver ob)
    {
        Role target = (Role)p;
        float percent = Parent.GetPercent(enProp.hp, enProp.hpMax);
        float v = this.GetLvValue(ExCfg.value);

        //重置下，血量有时候会加回去
        if (m_trigger && percent > v)
        {
            m_trigger = false;
            return;
        }

        if (m_trigger || percent > v)
            return;
        m_trigger = true;

        int poolId = this.Id;
        int parentId = m_parent.Id;
        int targetId = target.Id;

        //作用对象
        Role buffTarget = this.GetRole(ExCfg.targetType, target);
        if (buffTarget == null)
            return;
        BuffPart buffPart = buffTarget.BuffPart;

        //释放者
        Role buffSource = this.GetRole(ExCfg.sourceType, target);
        if (buffSource == null)
            return;

        for (int i = 0; i < ExCfg.buffIds.Count; ++i)
        {
            buffPart.AddBuff(ExCfg.buffIds[i], buffSource);
            if (IsUnneedHandle(poolId, m_parent, parentId, target, targetId))
                return;
        }
    }


}

