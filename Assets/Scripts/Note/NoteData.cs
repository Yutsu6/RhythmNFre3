using System.Collections;
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

    // �ṹ��������ֶ�
    public bool hasLoopSymbol = false;          // �Ƿ���ѭ������
    public int[] loopCodes;     // ѭ��������
    public string loopRawData = "";             // ԭʼѭ�����ݣ����ڵ��ԣ�

    // ѭ�������չ
    public List<float> allTriggerTimes = new List<float>(); // ���г��ֵ�ʱ���
    public int currentActivationIndex = 0; // ��ǰ��������

    // ��ȡ��ǰ�����ʱ���
    public float GetCurrentTriggerTime()
    {
        if (allTriggerTimes.Count > currentActivationIndex)
            return allTriggerTimes[currentActivationIndex];
        return triggerTime; // ���˵����ֶ�
    }


}