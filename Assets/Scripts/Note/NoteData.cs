using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    // 现有字段...
    public int rowId;
    public float position;
    public string type;
    public float length;
    public bool isJudged = false;
    public string judgmentResult = "";
    public GameObject noteObject;
    public float triggerTime;
    public int indentLevel = 0;
    public bool hasEnteredJudgmentQueue = false;

    // 循环符号相关字段
    public bool hasLoopSymbol = false;
    public int[] loopCodes;
    public string loopRawData = "";
    public bool loopSymbolTriggered = false;

    // 新增：if符号相关字段
    public bool hasIfSymbol = false;
    public int[] ifConditionCodes;
    public string ifRawData = "";
    public bool ifSymbolTriggered = false;

    // 结构符号列表
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();
}