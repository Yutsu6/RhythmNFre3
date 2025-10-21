using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    public int rowId;   //行
    public float position;  //在该行的起始位置
    public string type;     //类型
    public float length;    //长度

    public bool isJudged = false;
    public string judgmentResult = "";
    public GameObject noteObject;
    public float triggerTime;

    public int indentLevel = 0;                    // 缩进级别

    public bool hasEnteredJudgmentQueue = false;

    // 结构符号相关字段
    public bool hasLoopSymbol = false;          // 是否有循环符号
    public int[] loopCodes;     // 循环码数组
    public string loopRawData = "";             // 原始循环数据（用于调试）

    // 循环相关扩展
    public List<float> allTriggerTimes = new List<float>(); // 所有出现的时间戳
    public int currentActivationIndex = 0; // 当前激活的序号

    // 获取当前激活的时间戳
    public float GetCurrentTriggerTime()
    {
        if (allTriggerTimes.Count > currentActivationIndex)
            return allTriggerTimes[currentActivationIndex];
        return triggerTime; // 回退到旧字段
    }


}