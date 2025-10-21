using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using TMPro.Examples;
using UnityEngine;

public class ChartSpawner : MonoBehaviour
{
    public ChartParser parser;

    public GameObject tapNote;
    public GameObject breakNote;
    public GameObject holdNote;
    public GameObject track;


    // 循环执行上下文
    public class LoopExecutionContext
    {
        public NoteData loopNote;           // 循环符号所在的音符
        public int currentIteration;        // 当前第几次循环 (从0开始)
        public int[] loopCodes;             // 循环码数组
        public float baseTime;              // 循环开始的基础时间
        public int indentLevel;             // 循环的缩进级别
        public int loopDepth;               // 循环深度
    }

    // 行节点结构
    public class RowNode
    {
        public int rowId;
        public int indentLevel;
        public List<NoteData> notes = new List<NoteData>();
        public List<RowNode> children = new List<RowNode>();

        public RowNode(int id, int indent)
        {
            rowId = id;
            indentLevel = indent;
        }

    }
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

        //位置
        newNote.transform.position = new Vector2(worldPos.x, worldPos.y);


        // 根据类型和长度设置物体大小
        SetupNoteSize(newNote, noteData.type, noteData.length);

        // 设置物体名称以便识别
        newNote.name = $"{noteData.type}_row{noteData.rowId}_pos{noteData.position}";

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
                // Hold
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成Hold音符，长度: {length} 格, 实际宽度: {length * parser.cellSize} 单位");
                break;

            case "track":
                // 音轨
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"生成音轨，长度: {length} 格");
                break;

            case "tap":
                // Tap
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



    void SetupHoldNote(GameObject holdNote, float length)
    {
        //调整Hold音符的宽度
        Vector3 currentScale = holdNote.transform.localScale;
        holdNote.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);

        // 因为轴心点在左侧，所以不需要调整位置
        Debug.Log($"生成Hold音符，长度: {length} 格, 实际宽度: {length * parser.cellSize} 单位");
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