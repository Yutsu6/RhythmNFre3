using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.WSA;
using static ChartSpawner;

public class ChartParser : MonoBehaviour
{

    public List<NoteData> notes = new List<NoteData>();

    public TextAsset chartTextFile;

    public Vector2 gridOrigin = Vector2.zero; //坐标原点
    public float cellSize = 1.0f;           //单位大小
    public float rowHeight = 1.5f;          //垂直间距

    private void Start()
    {
        ParseChart();
    }


    public void ParseChart()
    {
        notes.Clear();


        if (chartTextFile == null)
        {
            Debug.LogError("没有谱面文件喵！");
            return;
        }

        string fileContent = chartTextFile.text;
        string[] lines = fileContent.Split('\n');


        int currentRowId = -1;  //当前正在处理的行ID
        bool inMainSection = false;
        int currentIndent = 0; // 当前行的缩进量

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            Debug.Log($"第{i}行: '{line}'");

            int commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
            {
                line = line.Substring(0, commentIndex).Trim();
            }

            if (string.IsNullOrEmpty(line))
            {
                Debug.Log($"  跳过空行");
                continue;
            }

            if (line.StartsWith("#"))
            {

                if (line == "# Main")
                {

                    inMainSection = true;
                    continue;
                }
                else if (line == "# Meta")
                {
                    inMainSection = false;

                    continue;
                }
                else
                {
                    Debug.Log($"  其他注释，跳过");
                    continue;
                }

            }

            if (!inMainSection)
            {

                continue;
            }

            // 解析 tab 语句
            if (line.StartsWith("tab:"))
            {
                string tabValueStr = line.Substring(4).Trim(); // 去掉 "tab:"
                if (int.TryParse(tabValueStr, out int tabValue))
                {
                    currentIndent = tabValue;
                    Debug.Log($"行{currentRowId} 设置缩进量: {currentIndent}");
                }
                else
                {
                    Debug.LogError($"无法解析缩进量: {tabValueStr}");
                }
                continue;
            }

            if (line.StartsWith("-") && line.EndsWith(":"))
            {
                string rowIdStr = line.Substring(0, line.Length - 1).Trim();
                if (int.TryParse(rowIdStr, out currentRowId))
                {
                    // 新行开始时重置缩进量为默认值0
                    currentIndent = 0;
                    Debug.Log($"切换到行: {currentRowId}, 重置缩进量");
                }
                continue;
            }

            //处理单元
            int colonIndex = line.IndexOf(':');

            if (colonIndex > 0)
            {
                //位置
                string posString = line.Substring(0, colonIndex).Trim();

                if (float.TryParse(posString, out float notePosition))
                {



                    //解析单元内容
                    string content = line.Substring(colonIndex + 1).Trim();


                    if (content.StartsWith("[") && content.EndsWith("]"))
                    {


                        string innerContent = content.Substring(1, content.Length - 2).Trim();


                        NoteData newNote = new NoteData();
                        newNote.rowId = currentRowId;
                        newNote.position = notePosition;
                        newNote.indentLevel = currentIndent; // 设置缩进量

                        //单元类型和长度

                        ParseNoteContent(innerContent, newNote);
                        notes.Add(newNote);

                    }
                }
            }
        }

    }

    public Vector2 GridWorld(int rowId, float position)
    {
        // 计算世界坐标
        // rowId: 行号（如 -1, -2）
        // position: 在该行内的横向位置
        float worldX = gridOrigin.x + position * cellSize;
        float worldY = gridOrigin.y + rowId * rowHeight;

        return new Vector2(worldX, worldY);
    }

    void ParseNoteContent(string content, NoteData noteData)
    {
        if (string.IsNullOrEmpty(content) || content == "0")
        {
            noteData.type = "track";  // 空内容 → "track" 类型
            noteData.length = 1f;
            return;
        }

        int semicolonIndex = content.IndexOf(';');
        if (semicolonIndex > 0)
        {
            // 有分号的格式，如 "4;h"
            string lengthStr = content.Substring(0, semicolonIndex).Trim();
            string typeStr = content.Substring(semicolonIndex + 1).Trim();

            // 解析基础信息（长度和类型）
            ParseBaseInfo(content, noteData);

            // 解析结构符号
            ParseStructureSymbols(content, noteData);
        }
        else
        {
            // 没有分号，只有基础信息
            ParseBaseInfo(content, noteData);
        }
    }

    //解析基础信息
    void ParseBaseInfo(string baseInfo,NoteData noteData)
    {
        //纯数字

        if (float.TryParse(baseInfo, out float trackLength))
        {
            noteData.type = "track";
            noteData.length = trackLength;
            return;
        }

        //检查是否是单个字母（音符类型）
        if (baseInfo.Length == 1 && IsNoteType(baseInfo))
        {
            noteData.type = MapTypeToFullName(baseInfo);
            noteData.length = 1f;
            return;
        }

        //带分号的长度格式
        int innerSemicolon = baseInfo.IndexOf(";");
        if (innerSemicolon > 0)
        {
            string lengthStr = baseInfo.Substring(0, innerSemicolon).Trim();
            string typeStr = baseInfo.Substring(innerSemicolon + 1).Trim();

            if(float.TryParse(lengthStr,out float length))
            {
                noteData.length = length;
            }
            noteData.type = MapTypeToFullName(typeStr);
        }
        else
        {
            noteData.type = MapTypeToFullName(baseInfo);
            noteData.length = 1f;
        }

    }

    // 解析结构符号
    void ParseStructureSymbols(string symbolInfo, NoteData noteData)
    {
        //循环符号
        if (symbolInfo.StartsWith("loop{"))
        {
            ParseLoopSymbol(symbolInfo, noteData);
        }
    }

    void ParseLoopSymbol(string loopStr,NoteData noteData)
    {
        noteData.hasLoopSymbol = true;
        noteData.loopRawData = loopStr;

        int startBrace = loopStr.IndexOf('{');
        int endBrace = loopStr.IndexOf('}');

        if (startBrace >= 0 && endBrace > startBrace)
        {
            string innerContent = loopStr.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();

            //按逗号分割
            string[] codeStrings = innerContent.Split(',');
            List<int> codes = new List<int>();

            foreach(string codeStr in codeStrings)
            {
                string trimmedCode = codeStr.Trim();
                if(int.TryParse(trimmedCode,out int code))
                {
                    codes.Add(code);
                }
                else
                {
                    Debug.LogWarning($"无法解析循环码: '{trimmedCode}'，使用默认值0");
                    codes.Add(0);
                }
            }

            noteData.loopCodes = codes.ToArray();
            Debug.Log($"解析循环符号: {string.Join(",", noteData.loopCodes)}");
        }
        else
        {
            Debug.LogError($"循环符号格式错误: {loopStr}");
            noteData.loopCodes = new int[0];
        }
    }
    // 缩写到全称的映射方法
    string MapTypeToFullName(string shortType)
    {
        switch (shortType)
        {
            case "t": return "tap";
            case "b": return "break";
            case "h": return "hold";
            // 可以继续添加其他映射
            case "0": return "track";
            default: return shortType; // 如果不是缩写，直接返回原值
        }
    }

    // 检查字符串是否是有效的音符类型
    bool IsNoteType(string str)
    {
        return str == "t" || str == "b" || str == "h" || str == "0";
    }

    //计算行长度
    public float CalculateRowLength(int rowId)
    {
        float maxPosition = 0f;
        foreach (var note in notes)
        {
            if (note.rowId == rowId)
            {
                float noteEnd = note.position + note.length;
                if (noteEnd > maxPosition)
                    maxPosition = noteEnd;
            }

        }
        return maxPosition;
    }

    //得到所有行
    public List<int> GetSortedRowIds()
    {
        var rowIds = new HashSet<int>();
        foreach (var note in notes)
        {
            rowIds.Add(note.rowId);
        }
        var sortedRowIds = rowIds.ToList();
        sortedRowIds.Sort((a, b) => b.CompareTo(a));
        return sortedRowIds;
    }

    //计算时间戳
    public void CalculateNoteTimestamps(float cursorSpeed)
    {
        if (notes.Count == 0)
        {
            Debug.LogError("没有音符数据！");
            return;
        }

        Debug.Log($"=== 开始时间戳计算 ===");
        Debug.Log($"光标速度: {cursorSpeed}");

        var sortedRowIds = GetSortedRowIds();
        Debug.Log($"行号顺序: {string.Join(", ", sortedRowIds)}");

        float accumulatedTime = 0f;

        foreach (int rowId in sortedRowIds)
        {
            float rowLength = CalculateRowLength(rowId);
            Debug.Log($"处理行{rowId}, 长度: {rowLength}, 累计时间: {accumulatedTime:F2}s");

            // 先计算这一行所有音符的时间戳
            foreach (var note in notes)
            {
                if (note.rowId == rowId)
                {
                    note.triggerTime = accumulatedTime + (note.position / cursorSpeed);
                    Debug.Log($"  音符: 位置{note.position} -> 时间{note.triggerTime:F2}s");
                }
            }

            // 这一行扫描完成后才累加时间
            accumulatedTime += rowLength / cursorSpeed;
        }

        Debug.Log($"时间戳计算完成！谱面总时长: {accumulatedTime:F2}s");

        // 验证时间戳
        Debug.Log("=== 时间戳验证 ===");
        foreach (var note in notes.OrderBy(n => n.triggerTime))
        {
            Debug.Log($"行{note.rowId} 位置{note.position} {note.type} -> {note.triggerTime:F2}s");
        }
    }

    //行结构树
    private Dictionary<int, RowNode> BuildRowStructure()
    {
        var rowDict = new Dictionary<int, RowNode>();

        // 首先收集所有行
        foreach (var note in notes)
        {
            if (!rowDict.ContainsKey(note.rowId))
            {
                rowDict[note.rowId] = new RowNode(note.rowId, note.indentLevel);
            }
            rowDict[note.rowId].notes.Add(note);
        }

        // 构建父子关系
        var sortedRowIds = rowDict.Keys.OrderByDescending(id => id).ToList();

        for (int i = 0; i < sortedRowIds.Count; i++)
        {
            int currentRowId = sortedRowIds[i];
            RowNode currentNode = rowDict[currentRowId];
            int currentIndent = currentNode.indentLevel;

            // 寻找父节点（缩进量比当前行小的最近行）
            for (int j = i + 1; j < sortedRowIds.Count; j++)
            {
                int potentialParentId = sortedRowIds[j];
                RowNode potentialParent = rowDict[potentialParentId];

                if (potentialParent.indentLevel < currentIndent)
                {
                    potentialParent.children.Add(currentNode);
                    break;
                }
            }
        }

        return rowDict;
    }

    //查找行中的循环符号



}