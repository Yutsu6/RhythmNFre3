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


    // ѭ��ִ��������
    public class LoopExecutionContext
    {
        public NoteData loopNote;           // ѭ���������ڵ�����
        public int currentIteration;        // ��ǰ�ڼ���ѭ�� (��0��ʼ)
        public int[] loopCodes;             // ѭ��������
        public float baseTime;              // ѭ����ʼ�Ļ���ʱ��
        public int indentLevel;             // ѭ������������
        public int loopDepth;               // ѭ�����
    }

    // �нڵ�ṹ
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

        //λ��
        newNote.transform.position = new Vector2(worldPos.x, worldPos.y);


        // �������ͺͳ������������С
        SetupNoteSize(newNote, noteData.type, noteData.length);

        // �������������Ա�ʶ��
        newNote.name = $"{noteData.type}_row{noteData.rowId}_pos{noteData.position}";

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
                // Hold
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"����Hold����������: {length} ��, ʵ�ʿ��: {length * parser.cellSize} ��λ");
                break;

            case "track":
                // ����
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"�������죬����: {length} ��");
                break;

            case "tap":
                // Tap
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



    void SetupHoldNote(GameObject holdNote, float length)
    {
        //����Hold�����Ŀ��
        Vector3 currentScale = holdNote.transform.localScale;
        holdNote.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);

        // ��Ϊ���ĵ�����࣬���Բ���Ҫ����λ��
        Debug.Log($"����Hold����������: {length} ��, ʵ�ʿ��: {length * parser.cellSize} ��λ");
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