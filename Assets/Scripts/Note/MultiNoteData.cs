using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MultiNoteLayer
{
    public string type;           // "track", "tap", "hold", "break"
    public float length = 1f;     // 长度
    public bool isJudged = false; // 该层是否已被判定
    public string judgmentResult = ""; // 该层的判定结果

    // 结构符号相关
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
    public int currentLayerIndex = 0; // 当前层索引
    public int totalHitsRequired = 0; // 总共需要打击的次数

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

        // 直接推进到下一层，不跳过任何层
        currentLayerIndex++;

        Debug.Log($"Multi推进到层{currentLayerIndex}/{layers.Count}");

        return !IsComplete();
    }

    public bool IsComplete()
    {
        return currentLayerIndex >= layers.Count;
    }
    // 原有的方法（如果需要保留）
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

    // 新增：获取剩余层数（包括音轨层）
    public int GetRemainingLayers()
    {
        return layers.Count - currentLayerIndex;
    }
}
