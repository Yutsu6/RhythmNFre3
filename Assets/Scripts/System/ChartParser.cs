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

    public Vector2 gridOrigin = Vector2.zero; //����ԭ��
    public float cellSize = 1.0f;           //��λ��С
    public float rowHeight = 1.5f;          //��ֱ���

    private void Start()
    {
        ParseChart();
    }


    public void ParseChart()
    {
        notes.Clear();


        if (chartTextFile == null)
        {
            Debug.LogError("û�������ļ�����");
            return;
        }

        string fileContent = chartTextFile.text;
        string[] lines = fileContent.Split('\n');


        int currentRowId = -1;  //��ǰ���ڴ������ID
        bool inMainSection = false;
        int currentIndent = 0; // ��ǰ�е�������

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            Debug.Log($"��{i}��: '{line}'");

            int commentIndex = line.IndexOf("//");
            if (commentIndex >= 0)
            {
                line = line.Substring(0, commentIndex).Trim();
            }

            if (string.IsNullOrEmpty(line))
            {
                Debug.Log($"  ��������");
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
                    Debug.Log($"  ����ע�ͣ�����");
                    continue;
                }

            }

            if (!inMainSection)
            {

                continue;
            }

            // ���� tab ���
            if (line.StartsWith("tab:"))
            {
                string tabValueStr = line.Substring(4).Trim(); // ȥ�� "tab:"
                if (int.TryParse(tabValueStr, out int tabValue))
                {
                    currentIndent = tabValue;
                    Debug.Log($"��{currentRowId} ����������: {currentIndent}");
                }
                else
                {
                    Debug.LogError($"�޷�����������: {tabValueStr}");
                }
                continue;
            }

            if (line.StartsWith("-") && line.EndsWith(":"))
            {
                string rowIdStr = line.Substring(0, line.Length - 1).Trim();
                if (int.TryParse(rowIdStr, out currentRowId))
                {
                    // ���п�ʼʱ����������ΪĬ��ֵ0
                    currentIndent = 0;
                    Debug.Log($"�л�����: {currentRowId}, ����������");
                }
                continue;
            }

            //����Ԫ
            int colonIndex = line.IndexOf(':');

            if (colonIndex > 0)
            {
                //λ��
                string posString = line.Substring(0, colonIndex).Trim();

                if (float.TryParse(posString, out float notePosition))
                {



                    //������Ԫ����
                    string content = line.Substring(colonIndex + 1).Trim();


                    if (content.StartsWith("[") && content.EndsWith("]"))
                    {


                        string innerContent = content.Substring(1, content.Length - 2).Trim();


                        NoteData newNote = new NoteData();
                        newNote.rowId = currentRowId;
                        newNote.position = notePosition;
                        newNote.indentLevel = currentIndent; // ����������

                        //��Ԫ���ͺͳ���

                        ParseNoteContent(innerContent, newNote);
                        notes.Add(newNote);

                    }
                }
            }
        }

    }

    public Vector2 GridWorld(int rowId, float position)
    {
        // ������������
        // rowId: �кţ��� -1, -2��
        // position: �ڸ����ڵĺ���λ��
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

        Debug.Log($"������������: '{content}'");

        // ����Ƿ�����ṹ����
        if (StructureSymbolFactory.IsStructureSymbol(content))
        {
            Debug.Log($"��⵽�ṹ����: {content}");

            // ���������Ϣ�ͽṹ����
            int lastSemicolon = content.LastIndexOf(';');
            if (lastSemicolon > 0)
            {
                string baseInfo = content.Substring(0, lastSemicolon).Trim();
                string symbolInfo = content.Substring(lastSemicolon + 1).Trim();

                Debug.Log($"������Ϣ: '{baseInfo}', ������Ϣ: '{symbolInfo}'");

                // ����������Ϣ
                ParseBaseInfo(baseInfo, noteData);

                // �����ṹ����
                ParseStructureSymbols(symbolInfo, noteData);
            }
            else
            {
                // ֻ�нṹ���ţ�û�л�����Ϣ
                Debug.Log("ֻ�нṹ���ţ�ֱ�ӽ���������Ϣ");
                ParseBaseInfo(content, noteData);
            }
        }
        else
        {
            // û�нṹ���ţ�ֻ�л�����Ϣ
            Debug.Log("û�м�⵽�ṹ���ţ�ֱ�ӽ���������Ϣ");
            ParseBaseInfo(content, noteData);
        }

        // �������
        if (noteData.hasLoopSymbol)
        {
            Debug.Log($"����������� - ����: {noteData.type}, ����: {noteData.length}, ѭ������: ��, ѭ����: {string.Join(",", noteData.loopCodes)}");
        }
        else
        {
            Debug.Log($"����������� - ����: {noteData.type}, ����: {noteData.length}, ѭ������: ��");
        }
    }

    // �޸Ľ����ṹ���ŵķ���
    void ParseStructureSymbols(string symbolInfo, NoteData noteData)
    {
        Debug.Log($"�����ṹ����: '{symbolInfo}'");

        // ѭ������
        if (symbolInfo.StartsWith("loop{"))
        {
            Debug.Log("��⵽ѭ������");
            LoopSymbol loopSymbol = new LoopSymbol();
            loopSymbol.Parse(symbolInfo, noteData);
            noteData.hasLoopSymbol = true;
            noteData.loopRawData = symbolInfo;
            noteData.loopCodes = loopSymbol.loopCodes;

            // ��ӵ��ṹ�����б�
            noteData.structureSymbols.Add(loopSymbol);

            Debug.Log($"ѭ�����Ž������: {string.Join(",", noteData.loopCodes)}");
        }
        else
        {
            Debug.Log($"δ֪�Ľṹ��������: {symbolInfo}");
        }
    }

    // ���������Ϣ�ͽṹ����
    string[] SplitContentAndSymbols(string content)
    {
        List<string> parts = new List<string>();
        int currentIndex = 0;
        int safetyCounter = 0; // ��ֹ����ѭ��
        const int MAX_ITERATIONS = 100;

        while (currentIndex < content.Length && safetyCounter < MAX_ITERATIONS)
        {
            safetyCounter++;

            // ������һ���ֺŻ�ṹ����
            int semicolonIndex = content.IndexOf(';', currentIndex);
            int structureSymbolIndex = FindStructureSymbolStart(content, currentIndex);

            // ȷ����һ���ָ��
            int nextSplit = -1;
            if (semicolonIndex >= 0 && structureSymbolIndex >= 0)
                nextSplit = Mathf.Min(semicolonIndex, structureSymbolIndex);
            else if (semicolonIndex >= 0)
                nextSplit = semicolonIndex;
            else if (structureSymbolIndex >= 0)
                nextSplit = structureSymbolIndex;
            else
                nextSplit = content.Length;

            // ��ֹ nextSplit С�� currentIndex
            if (nextSplit < currentIndex)
            {
                Debug.LogError($"�ָ�λ�ô���: currentIndex={currentIndex}, nextSplit={nextSplit}");
                break;
            }

            // ��ȡ����
            string part = content.Substring(currentIndex, nextSplit - currentIndex).Trim();
            if (!string.IsNullOrEmpty(part))
                parts.Add(part);

            currentIndex = nextSplit;
            if (currentIndex < content.Length && content[currentIndex] == ';')
                currentIndex++; // �����ֺ�
        }

        if (safetyCounter >= MAX_ITERATIONS)
        {
            Debug.LogError($"SplitContentAndSymbols ���ܽ�����ѭ��! content: '{content}'");
        }

        return parts.ToArray();
    }

    // ���ҽṹ���ſ�ʼλ��
    int FindStructureSymbolStart(string content, int startIndex)
    {
        foreach (string symbolType in new string[] { "loop", "if", "switch" })
        {
            string searchFor = symbolType + "{";
            int index = content.IndexOf(searchFor, startIndex);

            // ����ϸ�ĸ�ʽ���
            if (index >= 0)
            {
                // ����Ƿ��������Ľṹ���ţ����������Ч���ݣ�
                int braceEnd = content.IndexOf('}', index);
                if (braceEnd > index)
                {
                    return index;
                }
                // ������������ķ��ţ���������
            }
        }
        return -1;
    }


    //����������Ϣ
    void ParseBaseInfo(string baseInfo, NoteData noteData)
    {
        // ������ �� track
        if (float.TryParse(baseInfo, out float trackLength))
        {
            noteData.type = "track";
            noteData.length = trackLength;
            return;
        }

        // ������ĸ �� ��������
        if (baseInfo.Length == 1 && IsNoteType(baseInfo))
        {
            noteData.type = MapTypeToFullName(baseInfo);
            noteData.length = 1f;
            return;
        }

        // ���ֺŵĳ��ȸ�ʽ
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

    // �����ṹ����
    void ParseStructureSymbol(string symbolContent, NoteData noteData)
    {
        // ������֤���Ÿ�ʽ
        int openBrace = symbolContent.IndexOf('{');
        int closeBrace = symbolContent.IndexOf('}');

        if (openBrace < 0 || closeBrace < openBrace)
        {
            Debug.LogError($"�ṹ���Ÿ�ʽ����: '{symbolContent}'");
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
            Debug.LogWarning($"δ֪�Ľṹ��������: '{symbolType}'");
        }
    }


    // ��д��ȫ�Ƶ�ӳ�䷽��
    string MapTypeToFullName(string shortType)
    {
        switch (shortType)
        {
            case "t": return "tap";
            case "b": return "break";
            case "h": return "hold";
            // ���Լ����������ӳ��
            case "0": return "track";
            default: return shortType; // ���������д��ֱ�ӷ���ԭֵ
        }
    }

    // ����ַ����Ƿ�����Ч����������
    bool IsNoteType(string str)
    {
        return str == "t" || str == "b" || str == "h" || str == "0";
    }

    //�����г���
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

    //�õ�������
    public List<int> GetSortedRowIds()
    {
        var rowIds = new HashSet<int>();
        foreach (var note in notes)
        {
            rowIds.Add(note.rowId);
        }
        var sortedRowIds = rowIds.ToList();
        // �к��Ǹ�����-1, -2, -3... Ӧ�ôӴ�С����
        sortedRowIds.Sort((a, b) => b.CompareTo(a));

        return sortedRowIds;
    }

    //����ʱ���
    public void CalculateNoteTimestamps(float cursorSpeed)
    {
        if (notes.Count == 0)
        {
            Debug.LogError("û���������ݣ�");
            return;
        }

        Debug.Log($"=== ��ʼʱ������� ===");
        Debug.Log($"����ٶ�: {cursorSpeed}");

        var sortedRowIds = GetSortedRowIds();
        Debug.Log($"�к�˳��: {string.Join(", ", sortedRowIds)}");

        float accumulatedTime = 0f;

        foreach (int rowId in sortedRowIds)
        {
            float rowLength = CalculateRowLength(rowId);
            Debug.Log($"������{rowId}, ����: {rowLength}, �ۼ�ʱ��: {accumulatedTime:F2}s");

            // �ȼ�����һ������������ʱ���
            foreach (var note in notes)
            {
                if (note.rowId == rowId)
                {
                    note.triggerTime = accumulatedTime + (note.position / cursorSpeed);
                    Debug.Log($"  ����: λ��{note.position} -> ʱ��{note.triggerTime:F2}s");
                }
            }

            // ��һ��ɨ����ɺ���ۼ�ʱ��
            accumulatedTime += rowLength / cursorSpeed;
        }

        Debug.Log($"ʱ���������ɣ�������ʱ��: {accumulatedTime:F2}s");

        // ��֤ʱ���
        Debug.Log("=== ʱ�����֤ ===");
        foreach (var note in notes.OrderBy(n => n.triggerTime))
        {
            Debug.Log($"��{note.rowId} λ��{note.position} {note.type} -> {note.triggerTime:F2}s");
        }
    }

    public int IndexOfRowId(int rowId)
    {
        var sortedRowIds = GetSortedRowIds();
        return sortedRowIds.IndexOf(rowId);
    }



}
