using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MultiNoteLayer
{
    public string type;           // "track", "tap", "hold", "break"
    public float length = 1f;     // ����
    public bool isJudged = false; // �ò��Ƿ��ѱ��ж�
    public string judgmentResult = ""; // �ò���ж����

    // �ṹ�������
    public List<StructureSymbol> structureSymbols = new List<StructureSymbol>();
    public bool hasLoopSymbol = false;
    public int[] loopCodes;

    public MultiNoteLayer(string layerType, float layerLength = 1f)
    {
        type = layerType;
        length = layerLength;
    }
}
[System.Serializable]
public class MultiNoteData : NoteData
{
    public List<MultiNoteLayer> layers = new List<MultiNoteLayer>();
    public int currentLayerIndex = 0; // ��ǰ������
    public int totalHitsRequired = 0; // �ܹ���Ҫ����Ĵ���

    public MultiNoteData()
    {
        type = "multi";
    }

    public MultiNoteLayer GetCurrentLayer()
    {
        if (currentLayerIndex < layers.Count)
            return layers[currentLayerIndex];
        return null;
    }

    public bool MoveToNextLayer()
    {
        if (IsComplete()) return false;

        // ֱ���ƽ�����һ�㣬�������κβ�
        currentLayerIndex++;

        Debug.Log($"Multi�ƽ�����{currentLayerIndex}/{layers.Count}");

        return !IsComplete();
    }

    public bool IsComplete()
    {
        return currentLayerIndex >= layers.Count;
    }
    // ԭ�еķ����������Ҫ������
    public int GetRemainingHits()
    {
        int remaining = 0;
        for (int i = currentLayerIndex; i < layers.Count; i++)
        {
            if (layers[i].type != "track")
                remaining++;
        }
        return remaining;
    }

    // ��������ȡʣ���������������㣩
    public int GetRemainingLayers()
    {
        return layers.Count - currentLayerIndex;
    }
}
