using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChartSpawner : MonoBehaviour
{
    public ChartParser parser;

    public GameObject tapNote;
    public GameObject breakNote;
    public GameObject holdNote;
    public GameObject track;
    public GameObject loopSymbolPrefab; // 循环符号预制体

    private void Start()
    {
        // 等待解析器完成工作后生成音符
        Invoke("SpawnAllNotes", 0.1f);
    }

    void SpawnAllNotes()
    {
        if (parser == null || parser.notes.Count == 0)
        {
            Debug.LogError("解析器呢喵！");
            return;
        }

        foreach (var noteData in parser.notes)
        {
            SpawnSingleNote(noteData);
        }

        Debug.Log("生成音符完毕喵！");
    }

    void SpawnSingleNote(NoteData noteData)
    {
        GameObject prefabToUse = GetPrefabByType(noteData.type);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"没有为类型 '{noteData.type}' 设置预制体喵！");
            return;
        }

        GameObject newNote = Instantiate(prefabToUse);

        // 世界坐标（考虑缩进量）
        Vector2 worldPos = CalculateWorldPositionWithIndent(noteData);

        // 位置
        newNote.transform.position = new Vector2(worldPos.x, worldPos.y);

        // 根据类型和长度设置物体大小
        SetupNoteSize(newNote, noteData.type, noteData.length);

        // 设置物体名称以便识别
        newNote.name = $"{noteData.type}_row{noteData.rowId}_pos{noteData.position}";

        // 生成结构符号（包括循环符号）
        SpawnStructureSymbols(noteData, newNote);
    }

    // 新增：生成所有结构符号
    void SpawnStructureSymbols(NoteData noteData, GameObject parentNote)
    {
        Debug.Log($"检查音符的结构符号 - 行{noteData.rowId} 位置{noteData.position}: 有{noteData.structureSymbols.Count}个符号");

        foreach (var symbol in noteData.structureSymbols)
        {
            Debug.Log($"生成结构符号: {symbol.symbolType}");

            if (symbol is LoopSymbol)
            {
                SpawnLoopSymbol(noteData, parentNote);
            }
            // 可以在这里添加其他类型的结构符号生成
        }
    }

    void SpawnLoopSymbol(NoteData noteData, GameObject parentNote)
    {
        if (loopSymbolPrefab == null)
        {
            Debug.LogWarning("循环符号预制体未设置！");
            return;
        }

        GameObject loopSymbol = Instantiate(loopSymbolPrefab);

        // 计算循环符号的位置
        Vector2 symbolPosition = CalculateLoopSymbolPosition(noteData, parentNote);
        loopSymbol.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);

        // 设置循环符号大小为1x1单位，不缩放
        loopSymbol.transform.localScale = Vector3.one;

        // 设置父对象，便于管理
        loopSymbol.transform.SetParent(parentNote.transform);

        // 设置名称
        loopSymbol.name = $"LoopSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log($"成功生成循环符号在位置: ({symbolPosition.x:F2}, {symbolPosition.y:F2})");
    }

    // 计算循环符号的位置
    Vector2 CalculateLoopSymbolPosition(NoteData noteData, GameObject parentNote)
    {
        // 基础音符位置
        Vector2 baseWorldPos = CalculateWorldPositionWithIndent(noteData);

        // 根据音符类型调整位置
        if (noteData.type == "hold")
        {
            // Hold音符：放在最前面一个单位上
            // 由于轴心点在左侧，直接使用基础位置即可
            return baseWorldPos;
        }
        else
        {
            // Tap/Break/Track音符：叠在音符中心
            // 对于长度>1的音轨，仍然放在起始位置
            return baseWorldPos;
        }
    }

    // 计算考虑缩进的世界坐标
    Vector2 CalculateWorldPositionWithIndent(NoteData noteData)
    {
        // 基础位置（不考虑缩进）
        Vector2 baseWorldPos = parser.GridWorld(noteData.rowId, noteData.position);

        // 应用缩进偏移：缩进量 × 单元格大小
        float indentOffset = noteData.indentLevel * parser.cellSize;

        Vector2 finalWorldPos = new Vector2(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y
        );

        Debug.Log($"位置计算: 行{noteData.rowId} 基础位置{noteData.position} + 缩进{noteData.indentLevel} → 世界坐标({finalWorldPos.x:F1}, {finalWorldPos.y:F1})");

        return finalWorldPos;
    }

    // 统一设置音符大小的方法
    void SetupNoteSize(GameObject noteObject, string type, float length)
    {
        Vector3 currentScale = noteObject.transform.localScale;

        switch (type)
        {
            case "hold":
                // Hold - 调整宽度
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成Hold音符，长度: {length} 格, 实际宽度: {length * parser.cellSize} 单位");
                break;

            case "track":
                // 音轨 - 调整宽度
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成音轨，长度: {length} 格");
                break;

            case "tap":
            case "break":
                // Tap/Break - 调整宽度（如果长度>1）
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成{type}音符，长度: {length} 格");
                break;
        }
    }

    GameObject GetPrefabByType(string type)
    {
        switch (type)
        {
            case "tap": return tapNote;
            case "break": return breakNote;
            case "hold": return holdNote;
            case "track": return track;
            default: return null;
        }
    }

    // 在编辑器中添加一个按钮，方便重新生成
    [ContextMenu("重新生成所有音符")]
    void RegenerateAllNotes()
    {
        // 先删除所有已生成的音符
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }

        // 重新解析和生成
        if (parser != null)
        {
            parser.ParseChart();
            SpawnAllNotes();
        }
    }
}