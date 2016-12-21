﻿using UnityEngine;
using System.Collections;

public class UILevelBloodRadar : MonoBehaviour
{
    public Role m_role;
    public ImageEx m_progressImg;

    void LateUpdate()
    {

        if (m_role == null || m_role.State == Role.enState.dead || m_role.transform == null || m_role.GetFlag(GlobalConst.FLAG_SHOW_FRIENDBLOOD) <= 0)
        {
            this.gameObject.SetActive(false);
            return;
        }


        Camera m_gameCam = CameraMgr.instance.CurCamera;
        if (m_gameCam == null)
        {
            this.gameObject.SetActive(false);
            return;
        }

        UISmoothProgress progress = this.GetComponent<UISmoothProgress>();
        progress.SetProgress(m_role.GetInt(enProp.hp) / m_role.GetFloat(enProp.hpMax), true);

        Vector3 targetPos = m_role.RoleModel.Title.position;
        Vector3 pos = m_gameCam.WorldToViewportPoint(targetPos);
        if (pos.z < 0)
        {
            pos.x *= -1;
            pos.y *= -1;
            pos.x += 1;
        }

        bool isVisible = false;
        if (pos.z < 0 || (pos.z > 0 && (pos.x < 0 || pos.x > 1 || pos.y < 0 || pos.y > 1)))
            isVisible = true;
        else
            isVisible = false;
        m_progressImg.gameObject.SetActive(true);
        RectTransform areaRect = UIMgr.instance.Get<UILevel>().Get<UILevelAreaGizmos>().gameObject.GetComponent<RectTransform>();

        if (isVisible)
        {
            Rect rect = areaRect.rect;

            Vector2 screenSize = new Vector2(rect.width, rect.height);

            pos.x = Mathf.Clamp01(pos.x);
            pos.y = Mathf.Clamp01(pos.y);

            pos.x -= 0.5f;
            pos.y -= 0.5f;

            pos.x *= screenSize.x;
            pos.y *= screenSize.y;

            float angle = 0f;
            Vector2 offset = OffsetPosition(CreateRect(pos.x, pos.y, 100, 50), CreateRect(0f, 0f, screenSize.x, screenSize.y), ref angle);
            offset.x += pos.x;
            offset.y += pos.y;

            this.gameObject.transform.localPosition = offset;
            this.gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            //设置下位置
            Camera caUI = UIMgr.instance.UICameraHight;
            if (caUI != null)
            {
                Vector2 pos2D;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(areaRect, m_gameCam.WorldToScreenPoint(m_role.RoleModel.Title.position), caUI, out pos2D))
                {
                    this.GetComponent<RectTransform>().anchoredPosition = pos2D;
                }
                else
                    Debuger.LogError("UILevelAreaBlood计算不出2d位置2");
            }
        }
      
    }
    Rect CreateRect(float x, float y, float width, float height)
    {
        return new Rect(x - width * 0.5f, y - height * 0.5f, width, height);
    }

    public void SetData(Role role, int mode = 1)
    {
        m_role = role;
        this.gameObject.SetActive(true);
    }

    Vector2 OffsetPosition(Rect rect, Rect maxRect, ref float angle)
    {
        angle = 0f;
        Vector2 offset = Vector2.zero;
        if (maxRect.xMin > rect.xMin)// 左
        {
            offset.x += (maxRect.xMin - rect.xMin);
            //angle = 0;
        }

        if (maxRect.xMax < rect.xMax)// 右
        {
            offset.x -= (rect.xMax - maxRect.xMax);
            //angle = 180;
        }

        if (maxRect.yMin > rect.yMin)// 下
        {
            offset.y += (maxRect.yMin - rect.yMin);
            //angle = 90;
        }

        if (maxRect.yMax < rect.yMax)// 上
        {
            offset.y -= (rect.yMax - maxRect.yMax);
            //angle = -90;
        }

        return offset;
    }
}
