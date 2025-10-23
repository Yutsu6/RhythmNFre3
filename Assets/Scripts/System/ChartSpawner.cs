using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ChartSpawner : MonoBehaviour
{
    public ChartParser parser;

    public GameObject tapNote;
    public GameObject breakNote;
    public GameObject holdNote;
    public GameObject track;
    public GameObject loopSymbolPrefab; // ѭ������Ԥ����
    public GameObject ifSymbolPrefab; // ������if����Ԥ����
    public GameObject breakSymbolPrefab;
    public GameObject continueSymbolPrefab;
    public GameObject returnSymbolPrefab;
    public GameObject commentSymbolPrefab;
    public GameObject speedSymbolF; // ���ٷ���Ԥ����
    public GameObject speedSymbolL; // ���ٷ���Ԥ����

    public GameObject multiIndicatorPrefab; // ������Multiָʾ��Ԥ����


    public Transform chartContainer; // ��������������

    private void Start()
    {
        StartCoroutine(WaitForParserReady());
    }

    IEnumerator WaitForParserReady()
    {
        Debug.Log("�ȴ�������׼��...");

        // �ȴ���֡�����������ʼ��
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        // ���parser����null�����Բ���
        if (parser == null)
        {
            parser = FindObjectOfType<ChartParser>();
            Debug.Log($"�Զ����ҽ�����: {parser != null}");
        }

        // �ȴ���������ɽ���
        float timeout = 5f;
        float startTime = Time.time;

        while (parser == null || parser.notes == null || parser.notes.Count == 0)
        {
            if (Time.time - startTime > timeout)
            {
                Debug.LogError("�ȴ���������ʱ��");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("������׼����������ʼ��������");
        SpawnAllNotes();
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
        Debug.Log($"=== ��ʼ�������� ===");
        Debug.Log($"��������: ��{noteData.rowId} λ��{noteData.position} ����{noteData.type}");
        Debug.Log($"�ṹ����: {noteData.GetSymbolsDebugInfo()}");

        // ����Ƿ���Multi����
        if (noteData.isMultiNote)
        {
            Debug.Log("��⵽Multi����");
            SpawnMultiNote(noteData.AsMultiNote());
            return;
        }

        GameObject prefabToUse = GetPrefabByType(noteData.type);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"û��Ϊ���� '{noteData.type}' ����Ԥ��������");
            return;
        }

        Debug.Log($"ʹ��Ԥ����: {prefabToUse.name}");

        GameObject newNote = Instantiate(prefabToUse);

        // ���ø�����
        if (chartContainer != null)
        {
            newNote.transform.SetParent(chartContainer);
        }

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

    // ����Multi���� - ʹ�õ�ǰ���Ԥ����

    void SpawnMultiNote(MultiNoteData multiNoteData)
    {
        // ����״̬��ȷ���ӵ�һ�㿪ʼ
        multiNoteData.hasEnteredJudgmentQueue = false;

        if (multiNoteData.IsComplete())
        {
            Debug.LogWarning("Multi����������ʱ�Ѿ���ɣ���������");
            return;
        }

        var currentLayer = multiNoteData.GetCurrentLayer();
        if (currentLayer == null)
        {
            Debug.LogWarning("Multi����û����Ч�Ĳ㣡");
            return;
        }

        GameObject prefabToUse = GetPrefabByType(currentLayer.type);
        if (prefabToUse == null)
        {
            Debug.LogWarning($"û��ΪMulti�����ĵ�ǰ������ '{currentLayer.type}' ����Ԥ���壡");
            return;
        }

        GameObject multiObject = Instantiate(prefabToUse);
        Vector2 worldPos = CalculateWorldPositionWithIndent(multiNoteData);
        multiObject.transform.position = new Vector2(worldPos.x, worldPos.y);

        // ʹ��Multi�������ܳ���
        SetupNoteSize(multiObject, "multi", multiNoteData.length);

        multiObject.name = $"multi_row{multiNoteData.rowId}_pos{multiNoteData.position}";

        multiNoteData.noteObject = multiObject;

        if (chartContainer != null)
        {
            multiObject.transform.SetParent(chartContainer);
        }

        // ���Multiָʾ��
        SpawnMultiIndicator(multiObject, multiNoteData);

        SpawnStructureSymbols(multiNoteData, multiObject);

        // ��ʾ���в���Ϣ
        string layerInfo = "������: ";
        for (int i = 0; i < multiNoteData.layers.Count; i++)
        {
            layerInfo += $"{multiNoteData.layers[i].type}";
            if (i < multiNoteData.layers.Count - 1) layerInfo += " �� ";
        }

        Debug.Log($"����Multi����: �ܳ���={multiNoteData.length}, ��ǰ��={currentLayer.type}({multiNoteData.currentLayerIndex + 1}/{multiNoteData.layers.Count}), {layerInfo}");
    }
    // ����������Multiָʾ��
    public void SpawnMultiIndicator(GameObject multiObject, MultiNoteData multiNoteData)
    {
        if (multiIndicatorPrefab == null)
        {
            Debug.LogWarning("Multiָʾ��Ԥ����δ���ã�");
            return;
        }

        GameObject indicator = Instantiate(multiIndicatorPrefab);
        indicator.transform.SetParent(multiObject.transform);

        // ����λ�ã����½�
        indicator.transform.localPosition = new Vector3(
            multiNoteData.length * parser.cellSize * 0.5f - 0.1f, // �Ҳ�
            -0.1f, // �·�
            -0.1f  // ��ǰ��ʾ
        );

        // ����ָʾ������
        indicator.name = "MultiIndicator";

        // ��ʼ��ָʾ����ʾ
        InitializeMultiIndicator(indicator, multiNoteData);
    }

    // ��ʼ��Multiָʾ����ʾ
    void InitializeMultiIndicator(GameObject indicator, MultiNoteData multiNoteData)
    {
        // ��ָʾ�����Ӷ����в���TextMeshPro���
        var textMesh = indicator.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.text = multiNoteData.GetRemainingHits().ToString();
            var updater = indicator.AddComponent<MultiIndicatorUpdater>();
            updater.multiNoteData = multiNoteData;
        }
        else
        {
            Debug.LogWarning("Multiָʾ��Ԥ������û���ҵ�TextMeshPro�����");
        }
    }


    // �������������нṹ����
    // �������������нṹ����
   public void SpawnStructureSymbols(NoteData noteData, GameObject parentNote)
    {
        Debug.Log($"=== ��ʼ���ɽṹ���� ===");
        Debug.Log($"����: ��{noteData.rowId} λ��{noteData.position} ����{noteData.type}");
        Debug.Log($"�ṹ��������: {noteData.structureSymbols.Count}");

        if (noteData.structureSymbols.Count == 0)
        {
            Debug.Log($"����û�нṹ����");
            return;
        }

        foreach (var symbol in noteData.structureSymbols)
        {
            Debug.Log($"׼�����ɽṹ����: {symbol.symbolType}");

            switch (symbol.symbolType)
            {
                case "loop":
                    Debug.Log($"����Loop����");
                    SpawnLoopSymbol(noteData, parentNote, symbol as LoopSymbol);
                    break;
                case "if":
                    Debug.Log($"����If����");
                    SpawnIfSymbol(noteData, parentNote, symbol as IfSymbol);
                    break;
                case "break":
                    Debug.Log($"����Break����");
                    SpawnBreakSymbol(noteData, parentNote, symbol as BreakSymbol);
                    break;
                case "continue":
                    Debug.Log($"����Continue����");
                    SpawnContinueSymbol(noteData, parentNote, symbol as ContinueSymbol);
                    break;
                case "return":
                    Debug.Log($"����Return����");
                    SpawnReturnSymbol(noteData, parentNote, symbol as ReturnSymbol);
                    break;
                case "comment":
                    Debug.Log($"����Comment����");
                    SpawnCommentSymbol(noteData, parentNote, symbol as CommentSymbol);
                    break;
                case "speed":  // ����
                SpawnSpeedSymbol(noteData, parentNote, symbol as SpeedSymbol);
                    break;
                default:
                    Debug.LogWarning($"δ֪�Ľṹ��������: {symbol.symbolType}");
                    break;
            }
        }
        Debug.Log($"=== �ṹ����������� ===");
    }

    void SpawnSpeedSymbol(NoteData noteData, GameObject parentNote, SpeedSymbol speedSymbol)
    {
        // ���� speedType ѡ���Ӧ��Ԥ����
        GameObject prefabToUse = null;

        if (speedSymbol.speedType == "F")
        {
            prefabToUse = speedSymbolF;
        }
        else if (speedSymbol.speedType == "L")
        {
            prefabToUse = speedSymbolL;
        }

        if (prefabToUse == null)
        {
            Debug.LogWarning($"���ٷ���Ԥ����δ����: {speedSymbol.speedType}");
            return;
        }

        GameObject symbolObj = Instantiate(prefabToUse);
        Vector2 basePos = CalculateWorldPositionWithIndent(noteData);
        Vector2 symbolPos = new Vector2(basePos.x, basePos.y);

        symbolObj.transform.position = symbolPos;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"SpeedSymbol_{speedSymbol.speedType}";

        Debug.Log($"���ɱ��ٷ���: {speedSymbol.speedType}");
    }

    void SpawnLoopSymbol(NoteData noteData, GameObject parentNote, LoopSymbol loopSymbol)
    {
        if (loopSymbolPrefab == null)
        {
            Debug.LogWarning("ѭ������Ԥ����δ���ã�");
            return;
        }

        GameObject symbolObj = Instantiate(loopSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"LoopSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log($"�ɹ�����ѭ������");
    }

    void SpawnIfSymbol(NoteData noteData, GameObject parentNote, IfSymbol ifSymbol)
    {
        Debug.Log($"=== ��ʼ����If���� ===");

        if (ifSymbolPrefab == null)
        {
            Debug.LogError("If����Ԥ����δ���ã�");
            return;
        }

        Debug.Log($"If����Ԥ����: {ifSymbolPrefab.name}");
        Debug.Log($"������: {parentNote.name}");

        GameObject symbolObj = Instantiate(ifSymbolPrefab);
        Debug.Log($"ʵ����If���Ŷ���: {symbolObj.name}");

        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        Debug.Log($"����λ��: {symbolPosition}");

        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"IfSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log($"If�����������: {symbolObj.name}, ������: {symbolObj.transform.parent.name}");
        Debug.Log($"=== If����������� ===");
    }

    void SpawnBreakSymbol(NoteData noteData, GameObject parentNote, BreakSymbol breakSymbol)
    {
        if (breakSymbolPrefab == null)
        {
            Debug.LogWarning("Break����Ԥ����δ���ã�");
            return;
        }

        GameObject symbolObj = Instantiate(breakSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"BreakSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("�ɹ�����Break����");
    }

    void SpawnContinueSymbol(NoteData noteData, GameObject parentNote, ContinueSymbol continueSymbol)
    {
        if (continueSymbolPrefab == null)
        {
            Debug.LogWarning("Continue����Ԥ����δ���ã�");
            return;
        }

        GameObject symbolObj = Instantiate(continueSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"ContinueSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("�ɹ�����Continue����");
    }

    void SpawnReturnSymbol(NoteData noteData, GameObject parentNote, ReturnSymbol returnSymbol)
    {
        if (returnSymbolPrefab == null)
        {
            Debug.LogWarning("Return����Ԥ����δ���ã�");
            return;
        }

        GameObject symbolObj = Instantiate(returnSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"ReturnSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("�ɹ�����Return����");
    }

    void SpawnCommentSymbol(NoteData noteData, GameObject parentNote, CommentSymbol commentSymbol)
    {
        if (commentSymbolPrefab == null)
        {
            Debug.LogWarning("Comment����Ԥ����δ���ã�");
            return;
        }

        GameObject symbolObj = Instantiate(commentSymbolPrefab);
        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, 0);
        symbolObj.transform.localScale = Vector3.one;
        symbolObj.transform.SetParent(parentNote.transform);
        symbolObj.name = $"CommentSymbol_row{noteData.rowId}_pos{noteData.position}";

        Debug.Log("�ɹ�����Comment����");
    }

    // �򻯣�ͳһ�ķ���λ�ü��㷽��
    Vector2 CalculateSymbolPosition(NoteData noteData)
    {
        return CalculateWorldPositionWithIndent(noteData);
    }

    Vector2 CalculateWorldPositionWithIndent(NoteData noteData)
    {
        Vector2 baseWorldPos = parser.GridWorld(noteData.rowId, noteData.position);
        float indentOffset = noteData.indentLevel * parser.cellSize;

        Vector2 finalWorldPos = new Vector2(
            baseWorldPos.x + indentOffset,
            baseWorldPos.y
        );

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
            case "multi":
                // Multi����Ҳ�������
                noteObject.transform.localScale = new Vector3(length * parser.cellSize, currentScale.y, currentScale.z);
                Debug.Log($"����Multi����������: {length} ��");
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

    // ������Multiָʾ��������
    public class MultiIndicatorUpdater : MonoBehaviour
    {
        public MultiNoteData multiNoteData;
        private TextMeshPro textMesh;

        void Start()
        {
            // ���Ӷ����в���TextMeshPro���
            textMesh = GetComponentInChildren<TextMeshPro>();
        }

        void Update()
        {
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            if (textMesh != null && multiNoteData != null)
            {
                // ʹ��ʣ����������Ǵ������
                int remainingLayers = multiNoteData.GetRemainingLayers();
                textMesh.text = remainingLayers.ToString();

                // ����ʣ������ı���ɫ
                if (remainingLayers <= 1)
                    textMesh.color = Color.red;
                else if (remainingLayers <= 3)
                    textMesh.color = Color.yellow;
                else
                    textMesh.color = Color.white;
            }
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