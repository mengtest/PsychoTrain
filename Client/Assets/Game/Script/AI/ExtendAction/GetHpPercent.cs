﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Simple.BehaviorTree
{
    public class GetHpPercentCfg : ConditionalCfg
    {
        public ValueRole r = new ValueRole(enValueRegion.constant);
        public Value<float> ret = new Value<float>(0, enValueRegion.tree);
#if UNITY_EDITOR
        public override void DrawAreaInfo(Node n)
        {
            r.Draw("角色", this, n);
            ret.Draw("结果", this, n);
        }
#endif

    }


    public class GetHpPercent : Conditional
    {
        GetHpPercentCfg CfgEx { get { return (GetHpPercentCfg)m_cfg; } }


        //入栈。行为树遍历过程中，遍历到一个节点就会入栈它。可以用做是当前次遍历的OnInit
        protected override void OnPush()
        {

        }


        //执行。遍历到这个节点的时候就会在OnPush()后执行，如果返回running的话就会一直执行，直到返回success或者fail，然后OnPop()
        protected override enNodeState OnExecute(enExecute executeType)
        {
            Role r = GetValue(CfgEx.r);

            if (r == null)
                return enNodeState.failure;
            SetValue(CfgEx.ret, r.GetPercent(enProp.hp, enProp.hpMax));
            return enNodeState.success;
        }

        //出栈。自己执行完了以及自己的子树执行完成后出栈
        protected override void OnPop()
        {

        }


    }
}