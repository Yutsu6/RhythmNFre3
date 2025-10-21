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
            noteData.type = "track";  // ������ �� "track" ����
            noteData.length = 1f;
            return;
        }

        int semicolonIndex = content.IndexOf(';');
        if (semicolonIndex > 0)
        {
            // �зֺŵĸ�ʽ���� "4;h"
            string lengthStr = content.Substring(0, semicolonIndex).Trim();
            string typeStr = content.Substring(semicolonIndex + 1).Trim();

            // ����������Ϣ�����Ⱥ����ͣ�
            ParseBaseInfo(content, noteData);

            // �����ṹ����
            ParseStructureSymbols(content, noteData);
        }
        else
        {
            // û�зֺţ�ֻ�л�����Ϣ
            ParseBaseInfo(content, noteData);
        }
    }

    //����������Ϣ
    void ParseBaseInfo(string baseInfo,NoteData noteData)
    {
        //������

        if (float.TryParse(baseInfo, out float trackLength))
        {
            noteData.type = "track";
            noteData.length = trackLength;
            return;
        }

        //����Ƿ��ǵ�����ĸ���������ͣ�
        if (baseInfo.Length == 1 && IsNoteType(baseInfo))
        {
            noteData.type = MapTypeToFullName(baseInfo);
            noteData.length = 1f;
            return;
        }

        //���ֺŵĳ��ȸ�ʽ
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

    // �����ṹ����
    void ParseStructureSymbols(string symbolInfo, NoteData noteData)
    {
        //ѭ������
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

            //�����ŷָ�
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
                    Debug.LogWarning($"�޷�����ѭ����: '{trimmedCode}'��ʹ��Ĭ��ֵ0");
                    codes.Add(0);
                }
            }

            noteData.loopCodes = codes.ToArray();
            Debug.Log($"����ѭ������: {string.Join(",", noteData.loopCodes)}");
        }
        else
        {
            Debug.LogError($"ѭ�����Ÿ�ʽ����: {loopStr}");
            noteData.loopCodes = new int[0];
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

    //�нṹ��
    private Dictionary<int, RowNode> BuildRowStructure()
    {
        var rowDict = new Dictionary<int, RowNode>();

        // �����ռ�������
        foreach (var note in notes)
        {
            if (!rowDict.ContainsKey(note.rowId))
            {
                rowDict[note.rowId] = new RowNode(note.rowId, note.indentLevel);
            }
            rowDict[note.rowId].notes.Add(note);
        }

        // �������ӹ�ϵ
        var sortedRowIds = rowDict.Keys.OrderByDescending(id => id).ToList();

        for (int i = 0; i < sortedRowIds.Count; i++)
        {
            int currentRowId = sortedRowIds[i];
            RowNode currentNode = rowDict[currentRowId];
            int currentIndent = currentNode.indentLevel;

            // Ѱ�Ҹ��ڵ㣨�������ȵ�ǰ��С������У�
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

    //�������е�ѭ������



}