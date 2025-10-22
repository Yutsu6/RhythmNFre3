using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoteData
{
    public int rowId;   //��
    public float position;  //�ڸ��е���ʼλ��
    public string type;     //����
    public float length;    //����

    public bool isJudged = false;
    public string judgmentResult = "";
    public GameObject noteObject;
    public float triggerTime;

    public int indentLevel = 0;                    // ��������

    public bool hasEnteredJudgmentQueue = false;

    // �ṹ��������ֶ� - �Ƴ���ֻ������
    // ѭ���������
    public bool hasLoopSymbol = false;
    public int[] loopCodes;
    public string loopRawData = "";
    public bool loopSymbolTriggered = false; // �Ƿ��Ѵ���

    // �������ṹ�����б�֧�ֶ��ַ���
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();
}