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

    // 结构符号相关字段 - 移除了只读限制
    // 循环符号相关
    public bool hasLoopSymbol = false;
    public int[] loopCodes;
    public string loopRawData = "";
    public bool loopSymbolTriggered = false; // 是否已触发

    // 新增：结构符号列表，支持多种符号
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();
}