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
    public GameObject loopSymbolPrefab; // ѭ������Ԥ����

    private void Start()
    {
        // �ȴ���������ɹ�������������
        Invoke("SpawnAllNotes", 0.1f);
    }

    void SpawnAllNotes()
    {
        if (parser == null || parser.notes.Count == 0)
        {
            Debug.LogError("������������");
            return;
        }

        foreach (var noteData in parser.notes)
        {
            SpawnSingleNote(noteData);
        }

        Debug.Log("���������������");
    }

    void SpawnSingleNote(NoteData noteData)
    {
        GameObject prefabToUse = GetPrefabByType(noteData.type);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"û��Ϊ���� '{noteData.type}' ����Ԥ��������");
            return;
        }

        GameObject newNote = Instantiate(prefabToUse);

        // �������꣨������������
        Vector2 worldPos = CalculateWorldPositionWithIndent(noteData);

        // λ��
        newNote.transform.position = new Vector2(worldPos.x, worldPos.y);

        // �������ͺͳ������������С
        SetupNoteSize(newNote, noteData.type, noteData.length);

        // �������������Ա�ʶ��
        newNote.name = $"{noteData.type}_row{noteData.rowId}_pos{noteData.position}";

        // ���ɽṹ���ţ�����ѭ�����ţ�
        SpawnStructureSymbols(noteData, newNote);
    }

    // �������������нṹ����
    void SpawnStructureSymbols(NoteData noteData, GameObject parentNote)
    {
        Debug.Log($"��������Ľṹ���� - ��{noteData.rowId} λ��{noteData.position}: ��{noteData.structureSymbols.Count}������");

        foreach (var symbol in noteData.structureSymbols)
        {
            Debug.Log($"���ɽṹ����: {symbol.symbolType}");

            if (symbol is LoopSymbol)
            {
                SpawnLoopSymbol(noteData, parentNote);
            }
            // ��������������������͵Ľṹ��������
        }
    }

    void SpawnLoopSymbol(NoteData noteData, GameObject parentNote)
    {
        if (loopSymbolPrefab == null)
        {
            Debug.LogWarning("ѭ������Ԥ����δ���ã�");
            return;
        }

        GameObject loopSymbol = Instantiate(loopSymbolPrefab);

        // ����ѭ�����ŵ�λ��
        Vector2 symbolPosition = CalculateLoopSymbolPosition(noteData, parentNote);
        loopSymbol.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);

        // ����ѭ�����Ŵ�СΪ1x1��λ��������
        loopSymbol.transform.localScale = Vector3.one;

        // ���ø����󣬱��ڹ���
        loopSymbol.transform.SetParent(parentNote.transform);

        // ��������
        loopSymbol.name = $"LoopSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log($"�ɹ�����ѭ��������λ��: ({symbolPosition.x:F2}, {symbolPosition.y:F2})");
    }

    // ����ѭ�����ŵ�λ��
    Vector2 CalculateLoopSymbolPosition(NoteData noteData, GameObject parentNote)
    {
        // ��������λ��
        Vector2 baseWorldPos = CalculateWorldPositionWithIndent(noteData);

        // �����������͵���λ��
        if (noteData.type == "hold")
        {
            // Hold������������ǰ��һ����λ��
            // �������ĵ�����ֱ࣬��ʹ�û���λ�ü���
            return baseWorldPos;
        }
        else
        {
            // Tap/Break/Track������������������
            // ���ڳ���>1�����죬��Ȼ������ʼλ��
            return baseWorldPos;
        }
    }

    // ���㿼����������������
    Vector2 CalculateWorldPositionWithIndent(NoteData noteData)
    {
        // ����λ�ã�������������
        Vector2 baseWorldPos = parser.GridWorld(noteData.rowId, noteData.position);

        // Ӧ������ƫ�ƣ������� �� ��Ԫ���С
        float indentOffset = noteData.indentLevel * parser.cellSize;

        Vector2 finalWorldPos = new Vector2(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y
        );

        Debug.Log($"λ�ü���: ��{noteData.rowId} ����λ��{noteData.position} + ����{noteData.indentLevel} �� ��������({finalWorldPos.x:F1}, {finalWorldPos.y:F1})");

        return finalWorldPos;
    }

    // ͳһ����������С�ķ���
    void SetupNoteSize(GameObject noteObject, string type, float length)
    {
        Vector3 currentScale = noteObject.transform.localScale;

        switch (type)
        {
            case "hold":
                // Hold - �������
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"����Hold����������: {length} ��, ʵ�ʿ��: {length * parser.cellSize} ��λ");
                break;

            case "track":
                // ���� - �������
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"�������죬����: {length} ��");
                break;

            case "tap":
            case "break":
                // Tap/Break - ������ȣ��������>1��
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"����{type}����������: {length} ��");
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

    // �ڱ༭�������һ����ť��������������
    [ContextMenu("����������������")]
    void RegenerateAllNotes()
    {
        // ��ɾ�����������ɵ�����
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }

        // ���½���������
        if (parser != null)
        {
            parser.ParseChart();
            SpawnAllNotes();
        }
    }
}