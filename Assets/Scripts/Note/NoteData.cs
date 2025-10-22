using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    // �����ֶ�...
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

    // ѭ����������ֶ�
    public bool hasLoopSymbol = false;
    public int[] loopCodes;
    public string loopRawData = "";
    public bool loopSymbolTriggered = false;

    // ������if��������ֶ�
    public bool hasIfSymbol = false;
    public int[] ifConditionCodes;
    public string ifRawData = "";
    public bool ifSymbolTriggered = false;

    // �ṹ�����б�
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();
}