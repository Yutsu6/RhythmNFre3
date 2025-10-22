using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro.Examples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.WSA;

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
            noteData.type = "track";
            noteData.length = 1f;
            return;
        }

        Debug.Log($"解析音符内容: '{content}'");

        // 检查是否包含结构符号
        if (StructureSymbolFactory.IsStructureSymbol(content))
        {
            Debug.Log($"检测到结构符号: {content}");

            // 分离基础信息和结构符号
            int lastSemicolon = content.LastIndexOf(';');
            if (lastSemicolon > 0)
            {
                string baseInfo = content.Substring(0, lastSemicolon).Trim();
                string symbolInfo = content.Substring(lastSemicolon + 1).Trim();

                Debug.Log($"基础信息: '{baseInfo}', 符号信息: '{symbolInfo}'");

                // 解析基础信息
                ParseBaseInfo(baseInfo, noteData);

                // 解析结构符号
                ParseStructureSymbols(symbolInfo, noteData);
            }
            else
            {
                // 只有结构符号，没有基础信息
                Debug.Log("只有结构符号，直接解析基础信息");
                ParseBaseInfo(content, noteData);
            }
        }
        else
        {
            // 没有结构符号，只有基础信息
            Debug.Log("没有检测到结构符号，直接解析基础信息");
            ParseBaseInfo(content, noteData);
        }

        // 调试输出
        if (noteData.hasLoopSymbol)
        {
            Debug.Log($"音符解析完成 - 类型: {noteData.type}, 长度: {noteData.length}, 循环符号: 有, 循环码: {string.Join(",", noteData.loopCodes)}");
        }
        else
        {
            Debug.Log($"音符解析完成 - 类型: {noteData.type}, 长度: {noteData.length}, 循环符号: 无");
        }
    }

    // 修改解析结构符号的方法
    void ParseStructureSymbols(string symbolInfo, NoteData noteData)
    {
        Debug.Log($"解析结构符号: '{symbolInfo}'");

        // 循环符号
        if (symbolInfo.StartsWith("loop{"))
        {
            Debug.Log("检测到循环符号");
            LoopSymbol loopSymbol = new LoopSymbol();
            loopSymbol.Parse(symbolInfo, noteData);
            noteData.hasLoopSymbol = true;
            noteData.loopRawData = symbolInfo;
            noteData.loopCodes = loopSymbol.loopCodes;

            // 添加到结构符号列表
            noteData.structureSymbols.Add(loopSymbol);

            Debug.Log($"循环符号解析完成: {string.Join(",", noteData.loopCodes)}");
        }
        else
        {
            Debug.Log($"未知的结构符号类型: {symbolInfo}");
        }
    }

    // 分离基础信息和结构符号
    string[] SplitContentAndSymbols(string content)
    {
        List<string> parts = new List<string>();
        int currentIndex = 0;
        int safetyCounter = 0; // 防止无限循环
        const int MAX_ITERATIONS = 100;

        while (currentIndex < content.Length && safetyCounter < MAX_ITERATIONS)
        {
            safetyCounter++;

            // 查找下一个分号或结构符号
            int semicolonIndex = content.IndexOf(';', currentIndex);
            int structureSymbolIndex = FindStructureSymbolStart(content, currentIndex);

            // 确定下一个分割点
            int nextSplit = -1;
            if (semicolonIndex >= 0 && structureSymbolIndex >= 0)
                nextSplit = Mathf.Min(semicolonIndex, structureSymbolIndex);
            else if (semicolonIndex >= 0)
                nextSplit = semicolonIndex;
            else if (structureSymbolIndex >= 0)
                nextSplit = structureSymbolIndex;
            else
                nextSplit = content.Length;

            // 防止 nextSplit 小于 currentIndex
            if (nextSplit < currentIndex)
            {
                Debug.LogError($"分割位置错误: currentIndex={currentIndex}, nextSplit={nextSplit}");
                break;
            }

            // 提取部分
            string part = content.Substring(currentIndex, nextSplit - currentIndex).Trim();
            if (!string.IsNullOrEmpty(part))
                parts.Add(part);

            currentIndex = nextSplit;
            if (currentIndex < content.Length && content[currentIndex] == ';')
                currentIndex++; // 跳过分号
        }

        if (safetyCounter >= MAX_ITERATIONS)
        {
            Debug.LogError($"SplitContentAndSymbols 可能进入死循环! content: '{content}'");
        }

        return parts.ToArray();
    }

    // 查找结构符号开始位置
    int FindStructureSymbolStart(string content, int startIndex)
    {
        foreach (string symbolType in new string[] { "loop", "if", "switch" })
        {
            string searchFor = symbolType + "{";
            int index = content.IndexOf(searchFor, startIndex);

            // 添加严格的格式检查
            if (index >= 0)
            {
                // 检查是否是完整的结构符号（后面跟着有效内容）
                int braceEnd = content.IndexOf('}', index);
                if (braceEnd > index)
                {
                    return index;
                }
                // 如果不是完整的符号，继续查找
            }
        }
        return -1;
    }


    //解析基础信息
    void ParseBaseInfo(string baseInfo, NoteData noteData)
    {
        // 纯数字 → track
        if (float.TryParse(baseInfo, out float trackLength))
        {
            noteData.type = "track";
            noteData.length = trackLength;
            return;
        }

        // 单个字母 → 音符类型
        if (baseInfo.Length == 1 && IsNoteType(baseInfo))
        {
            noteData.type = MapTypeToFullName(baseInfo);
            noteData.length = 1f;
            return;
        }

        // 带分号的长度格式
        int innerSemicolon = baseInfo.IndexOf(";");
        if (innerSemicolon > 0)
        {
            string lengthStr = baseInfo.Substring(0, innerSemicolon).Trim();
            string typeStr = baseInfo.Substring(innerSemicolon + 1).Trim();

            if (float.TryParse(lengthStr, out float length))
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
    void ParseStructureSymbol(string symbolContent, NoteData noteData)
    {
        // 首先验证符号格式
        int openBrace = symbolContent.IndexOf('{');
        int closeBrace = symbolContent.IndexOf('}');

        if (openBrace < 0 || closeBrace < openBrace)
        {
            Debug.LogError($"结构符号格式错误: '{symbolContent}'");
            return;
        }

        string symbolType = symbolContent.Substring(0, openBrace).Trim();

        StructureSymbol symbol = StructureSymbolFactory.CreateSymbol(symbolType);
        if (symbol != null)
        {
            symbol.Parse(symbolContent, noteData);
            noteData.structureSymbols.Add(symbol);
        }
        else
        {
            Debug.LogWarning($"未知的结构符号类型: '{symbolType}'");
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
        // 行号是负数：-1, -2, -3... 应该从大到小排序
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

    public int IndexOfRowId(int rowId)
    {
        var sortedRowIds = GetSortedRowIds();
        return sortedRowIds.IndexOf(rowId);
    }



}
