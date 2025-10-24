using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class ChartSpawner : MonoBehaviour
{
    [Header("����������")]
    public ChartParser parser;

    [Header("��������Ԥ���� - ͷ��β����")]
    public GameObject tapHeadPrefab;
    public GameObject tapBodyPrefab;
    public GameObject tapTailPrefab;

    public GameObject breakHeadPrefab;
    public GameObject breakBodyPrefab;
    public GameObject breakTailPrefab;

    public GameObject holdHeadPrefab;
    public GameObject holdBodyPrefab;
    public GameObject holdTailPrefab;

    public GameObject trackHeadPrefab;
    public GameObject trackBodyPrefab;
    public GameObject trackTailPrefab;

    [Header("����Ԥ����")]
    public GameObject loopSymbolPrefab;
    public GameObject ifSymbolPrefab;
    public GameObject breakSymbolPrefab;
    public GameObject continueSymbolPrefab;
    public GameObject returnSymbolPrefab;
    public GameObject commentSymbolPrefab;
    public GameObject speedSymbolF;
    public GameObject speedSymbolL;

    [Header("Multi���")]
    public GameObject multiIndicatorPrefab;
    public GameObject holdLinePrefab; // Holdб��Ԥ����

    // ��������
    private const float HEAD_LENGTH = 0.2f;
    private const float TAIL_LENGTH = 0.2f;
    private const float MIN_BODY_LENGTH = 0.01f;
    private const float HOLD_LINE_SPACING_MIN = 0.18f;
    private const float HOLD_LINE_SPACING_MAX = 0.25f;
    private const float HOLD_LINE_SPACING_OPTIMAL = 0.2f;

    private List<GameObject> allSpawnedObjects = new List<GameObject>();

    private void Start()
    {
        StartCoroutine(WaitForParserReady());
    }

    IEnumerator WaitForParserReady()
    {
        Debug.Log("�ȴ�������׼��...");
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (parser == null)
        {
            parser = FindObjectOfType<ChartParser>();
            Debug.Log($"�Զ����ҽ�����: {parser != null}");
        }

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
            Debug.LogError("û���������ݣ�");
            return;
        }

        ClearAllSpawnedObjects();

        foreach (var noteData in parser.notes)
        {
            if (noteData.isMultiNote)
            {
                SpawnMultiNote(noteData.AsMultiNote());
            }
            else
            {
                SpawnSingleNote(noteData);
            }
        }

        Debug.Log($"����������ɣ�������{allSpawnedObjects.Count}������");
    }

    void SpawnSingleNote(NoteData noteData)
    {
        Debug.Log($"���ɵ�����: ��{noteData.rowId} λ��{noteData.position} ����{noteData.type} ����{noteData.length}");

        // ������������
        GameObject noteContainer = new GameObject(GetNoteContainerName(noteData));
        noteContainer.transform.position = CalculateWorldPosition(noteData);
        noteContainer.transform.SetParent(this.transform);
        allSpawnedObjects.Add(noteContainer);

        // ����ͷ��β����
        SpawnNoteParts(noteContainer, noteData);

        // ����������������
        noteData.noteObject = noteContainer;

        // ���ɷ���
        SpawnStructureSymbols(noteData, noteContainer);
    }

    void SpawnNoteParts(GameObject container, NoteData noteData)
    {
        float totalLength = noteData.length;
        float bodyLength = Mathf.Max(MIN_BODY_LENGTH, totalLength - HEAD_LENGTH - TAIL_LENGTH);

        Debug.Log($"�����ֶ�: �ܳ�={totalLength}, ͷ={HEAD_LENGTH}, ��={bodyLength}, β={TAIL_LENGTH}");

        // ��ȡ��Ӧ���͵�Ԥ����
        var prefabs = GetNotePartPrefabs(noteData.type);
        if (prefabs == null)
        {
            Debug.LogError($"�޷���ȡ�������� '{noteData.type}' ��Ԥ����");
            return;
        }

        // ����ͷ��
        GameObject headPart = SpawnNotePart(container, prefabs.headPrefab, HEAD_LENGTH, 0f, "Head");
        if (headPart != null)
        {
            SetupPartVisual(headPart, "head", noteData.type);
        }

        // �������壨��������㹻��
        if (bodyLength > MIN_BODY_LENGTH)
        {
            GameObject bodyPart = SpawnNotePart(container, prefabs.bodyPrefab, bodyLength, HEAD_LENGTH, "Body");
            if (bodyPart != null)
            {
                SetupPartVisual(bodyPart, "body", noteData.type);

                // ���⴦��Hold������б��
                if (noteData.type == "hold")
                {
                    SpawnHoldLines(bodyPart, bodyLength);
                }
            }
        }

        // ����β��
        GameObject tailPart = SpawnNotePart(container, prefabs.tailPrefab, TAIL_LENGTH, HEAD_LENGTH + bodyLength, "Tail");
        if (tailPart != null)
        {
            SetupPartVisual(tailPart, "tail", noteData.type);
        }
    }

    GameObject SpawnNotePart(GameObject container, GameObject prefab, float length, float offsetX, string partName)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"��������Ԥ����Ϊ��: {partName}");
            return null;
        }

        GameObject part = Instantiate(prefab, container.transform);
        part.name = partName;

        // �޸����Ӧ���Ӿ�����
        Vector3 originalScale = part.transform.localScale;
        part.transform.localScale = new Vector3(
            length * parser.cellSize * parser.visualScale,
            originalScale.y * parser.visualScale,
            originalScale.z
        );

        // �޸����ƫ��λ��ҲҪ����
        part.transform.localPosition = new Vector3(
            offsetX * parser.cellSize * parser.visualScale,
            0f,
            0f
        );

        allSpawnedObjects.Add(part);
        return part;
    }

    void SetupPartVisual(GameObject part, string partType, string noteType)
    {
        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
        if (renderer == null) return;

        Color baseColor = GetNoteBaseColor(noteType);

        switch (partType)
        {
            case "head":
                renderer.color = new Color(baseColor.r * 1.2f, baseColor.g * 1.2f, baseColor.b * 1.2f); // ����
                break;
            case "body":
                renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f); // ��͸��
                break;
            case "tail":
                renderer.color = new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f); // ����
                break;
        }
    }

    Color GetNoteBaseColor(string noteType)
    {
        switch (noteType)
        {
            case "tap": return Color.blue;
            case "break": return Color.red;
            case "hold": return Color.green;
            case "track": return Color.gray;
            default: return Color.white;
        }
    }

    void SpawnHoldLines(GameObject bodyPart, float bodyLength)
    {
        // ��������hold�������ܳ���
        float totalHoldLength = bodyLength + HEAD_LENGTH + TAIL_LENGTH;

        HoldLineData lineData = CalculateHoldLinePositions(totalHoldLength);

        Debug.Log($"����Holdб��: ����={lineData.positions.Length}, ������={lineData.spacing:F3}");

        for (int i = 0; i < lineData.positions.Length; i++)
        {
            // ��������λ��ת��Ϊbody�ֲ�����
            // body�ľֲ�����ϵ�У�0��Ӧ���������HEAD_LENGTH��1��Ӧ���������HEAD_LENGTH + bodyLength
            float worldPos = lineData.positions[i];
            float localPosInBody = (worldPos - HEAD_LENGTH) / bodyLength;

            CreateHoldLine(bodyPart, localPosInBody, i, bodyLength);
        }
    }


    GameObject CreateHoldLine(GameObject bodyPart, float localPosition, int index, float bodyLength)
    {
        GameObject line;

        if (holdLinePrefab != null)
        {
            line = Instantiate(holdLinePrefab, bodyPart.transform);
        }
        else
        {
            line = new GameObject($"HoldLine_{index}");
            line.transform.SetParent(bodyPart.transform);
            SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 1f, 1f, 0.7f);
        }

        // ���þֲ�λ�ã�body�ľֲ�����ϵ��0~1��
        line.transform.localPosition = new Vector3(
            localPosition,
            0.5f * parser.visualScale,  // Yλ��ҲҪ����
            -0.1f
        );

        // �������ţ���׼���1/6����Ҫ����body������
        float standardLineWidth = 1f / 6f;
        float localScaleX = standardLineWidth / bodyLength;

        // ������Y��������Ϊ0.68
        line.transform.localScale = new Vector3(
           localScaleX * parser.visualScale,
           0.68f * parser.visualScale,
           1f
       );


        allSpawnedObjects.Add(line);
        return line;
    }

    HoldLineData CalculateHoldLinePositions(float totalHoldLength)
    {
        List<float> positions = new List<float>();

        // �������������Ϊ1ʱ�Ĺ̶�����
        if (Mathf.Abs(totalHoldLength - 1.0f) < 0.01f)
        {
            return new HoldLineData
            {
                positions = new float[] { 0.3f, 0.5f, 0.7f },
                spacing = 0.2f,
                lineCount = 3
            };
        }

        // ������ÿռ䣺��0.3��totalHoldLength-0.3
        float startPos = 0.3f;
        float endPos = totalHoldLength - 0.3f;
        float availableSpace = endPos - startPos;

        if (availableSpace <= 0f)
        {
            Debug.LogWarning($"Hold����{totalHoldLength}̫С���޷���������");
            return new HoldLineData { positions = new float[0], spacing = 0f, lineCount = 0 };
        }

        Debug.Log($"Hold�ܳ���: {totalHoldLength}, ���ÿռ�: {availableSpace} (��{startPos}��{endPos})");

        // ������Է��ö��ٸ�HoldLine��������� = HoldLine���� - 1��
        // ÿ��HoldLineռ�ÿռ䣺��� + HoldLine���(0.167)
        float lineWidth = 1f / 6f;

        // ����������С���ܵ�HoldLine����
        int maxLines = Mathf.FloorToInt(availableSpace / (HOLD_LINE_SPACING_MIN + lineWidth)) + 1;
        int minLines = Mathf.CeilToInt(availableSpace / (HOLD_LINE_SPACING_MAX + lineWidth)) + 1;

        // ȷ������3��������ռ��㹻��
        minLines = Mathf.Max(3, minLines);
        maxLines = Mathf.Max(minLines, maxLines);

        Debug.Log($"HoldLine������Χ: {minLines} ~ {maxLines}");

        // Ѱ�����Ž�
        int bestLineCount = -1;
        float bestSpacing = 0f;
        float minSpacingDiff = float.MaxValue;

        for (int lineCount = minLines; lineCount <= maxLines; lineCount++)
        {
            if (lineCount <= 1) continue;

            // �����ࣺ�ܿ��ÿռ��ȥ����HoldLine�Ŀ�ȣ�Ȼ����Լ������
            float totalLineWidth = lineWidth * lineCount;
            float totalSpacingSpace = availableSpace - totalLineWidth;
            float spacing = totalSpacingSpace / (lineCount - 1);

            float spacingDiff = Mathf.Abs(spacing - HOLD_LINE_SPACING_OPTIMAL);

            Debug.Log($"���� {lineCount}��HoldLine: ���={spacing:F3}, ��ֵ={spacingDiff:F3}");

            if (spacingDiff < minSpacingDiff &&
                spacing >= HOLD_LINE_SPACING_MIN &&
                spacing <= HOLD_LINE_SPACING_MAX)
            {
                minSpacingDiff = spacingDiff;
                bestLineCount = lineCount;
                bestSpacing = spacing;
            }
        }

        // ���û���ҵ����ʽ⣬ʹ�ù̶�3��
        if (bestLineCount == -1)
        {
            bestLineCount = 3;
            bestSpacing = (availableSpace - (lineWidth * bestLineCount)) / (bestLineCount - 1);
            Debug.LogWarning($"ʹ�ù̶�����: {bestLineCount}��HoldLine, ���={bestSpacing:F3}");
        }

        // ����HoldLine���ĵ�λ��
        float currentPos = startPos + lineWidth / 2f; // ��һ��HoldLine����

        for (int i = 0; i < bestLineCount; i++)
        {
            positions.Add(currentPos);

            // ��һ��HoldLine����λ�� = ��ǰ���� + HoldLine��� + ���
            if (i < bestLineCount - 1)
            {
                currentPos += lineWidth + bestSpacing;
            }
        }

        Debug.Log($"���շ���: {bestLineCount}��HoldLine, ���={bestSpacing:F3}");
        Debug.Log($"HoldLine����λ��: {string.Join(", ", positions.Select(p => p.ToString("F3")))}");

        // ��֤���һ��HoldLineλ���Ƿ���ȷ
        float lastLineCenter = positions[positions.Count - 1];
        float lastLineEnd = lastLineCenter + lineWidth / 2f;
        Debug.Log($"��֤: ���һ��HoldLine����λ��={lastLineEnd:F3}, Ҫ��λ��={endPos:F3}, ��ֵ={Mathf.Abs(lastLineEnd - endPos):F3}");

        return new HoldLineData
        {
            positions = positions.ToArray(),
            spacing = bestSpacing,
            lineCount = bestLineCount
        };
    }

    public void SpawnMultiNote(MultiNoteData multiNoteData)
    {
        Debug.Log($"����Multi����: ��{multiNoteData.rowId} λ��{multiNoteData.position} �ܳ�{multiNoteData.length}");

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

        // ����Multi����
        GameObject multiContainer = new GameObject(GetMultiContainerName(multiNoteData));
        multiContainer.transform.position = CalculateWorldPosition(multiNoteData);
        multiContainer.transform.SetParent(this.transform);
        allSpawnedObjects.Add(multiContainer);

        // ʹ�õ�ǰ�����������ͷ��β
        SpawnNoteParts(multiContainer, multiNoteData);

        multiNoteData.noteObject = multiContainer;

        // ���Multiָʾ��
        SpawnMultiIndicator(multiContainer, multiNoteData);
        SpawnStructureSymbols(multiNoteData, multiContainer);

        Debug.Log($"Multi�����������: ��ǰ��={currentLayer.type}({multiNoteData.currentLayerIndex + 1}/{multiNoteData.layers.Count})");
    }

    void SpawnMultiIndicator(GameObject multiObject, MultiNoteData multiNoteData)
    {
        if (multiIndicatorPrefab == null)
        {
            Debug.LogWarning("Multiָʾ��Ԥ����δ���ã�");
            return;
        }

        GameObject indicator = Instantiate(multiIndicatorPrefab, multiObject.transform);

        // ����λ�ã������Ҳ�
        float noteWidth = multiNoteData.length * parser.cellSize;
        indicator.transform.localPosition = new Vector3(
            noteWidth * 0.5f + 0.2f, // �Ҳ�ƫ��
            0f,
            -0.2f
        );

        indicator.name = "MultiIndicator";
        InitializeMultiIndicator(indicator, multiNoteData);
        allSpawnedObjects.Add(indicator);
    }

    void InitializeMultiIndicator(GameObject indicator, MultiNoteData multiNoteData)
    {
        TextMeshPro textMesh = indicator.GetComponentInChildren<TextMeshPro>();
        if (textMesh != null)
        {
            textMesh.text = multiNoteData.GetRemainingLayers().ToString();
            var updater = indicator.AddComponent<MultiIndicatorUpdater>();
            updater.multiNoteData = multiNoteData;
        }
        else
        {
            Debug.LogWarning("Multiָʾ��Ԥ������û���ҵ�TextMeshPro�����");
        }
    }

    public void SpawnStructureSymbols(NoteData noteData, GameObject parentNote)
    {
        if (noteData.structureSymbols.Count == 0) return;

        Debug.Log($"���ɽṹ����: {noteData.structureSymbols.Count}��");

        foreach (var symbol in noteData.structureSymbols)
        {
            Vector2 symbolPosition = CalculateSymbolPosition(noteData);

            switch (symbol.symbolType)
            {
                case "loop":
                    SpawnSymbol(loopSymbolPrefab, parentNote, symbolPosition, $"LoopSymbol_row{noteData.rowId}");
                    break;
                case "if":
                    SpawnSymbol(ifSymbolPrefab, parentNote, symbolPosition, $"IfSymbol_row{noteData.rowId}");
                    break;
                case "break":
                    SpawnSymbol(breakSymbolPrefab, parentNote, symbolPosition, $"BreakSymbol_row{noteData.rowId}");
                    break;
                case "continue":
                    SpawnSymbol(continueSymbolPrefab, parentNote, symbolPosition, $"ContinueSymbol_row{noteData.rowId}");
                    break;
                case "return":
                    SpawnSymbol(returnSymbolPrefab, parentNote, symbolPosition, $"ReturnSymbol_row{noteData.rowId}");
                    break;
                case "comment":
                    SpawnSymbol(commentSymbolPrefab, parentNote, symbolPosition, $"CommentSymbol_row{noteData.rowId}");
                    break;
                case "speed":
                    SpawnSpeedSymbol(noteData, parentNote, symbol as SpeedSymbol);
                    break;
                default:
                    Debug.LogWarning($"δ֪�Ľṹ��������: {symbol.symbolType}");
                    break;
            }
        }
    }

    void SpawnSymbol(GameObject symbolPrefab, GameObject parent, Vector2 position, string name)
    {
        if (symbolPrefab == null)
        {
            Debug.LogWarning($"����Ԥ����δ����: {name}");
            return;
        }

        GameObject symbol = Instantiate(symbolPrefab, parent.transform);
        symbol.transform.position = new Vector3(position.x, position.y, -0.1f);
        symbol.name = name;
        allSpawnedObjects.Add(symbol);

        Debug.Log($"���ɷ���: {name}");
    }

    void SpawnSpeedSymbol(NoteData noteData, GameObject parentNote, SpeedSymbol speedSymbol)
    {
        GameObject prefabToUse = speedSymbol.speedType == "F" ? speedSymbolF : speedSymbolL;
        if (prefabToUse == null)
        {
            Debug.LogWarning($"���ٷ���Ԥ����δ����: {speedSymbol.speedType}");
            return;
        }

        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        GameObject symbolObj = Instantiate(prefabToUse, parentNote.transform);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, -0.1f);
        symbolObj.name = $"SpeedSymbol_{speedSymbol.speedType}";
        allSpawnedObjects.Add(symbolObj);

        Debug.Log($"���ɱ��ٷ���: {speedSymbol.speedType}");
    }

    // ��������
    string GetNoteContainerName(NoteData noteData)
    {
        return $"{noteData.type}_row{noteData.rowId}_pos{noteData.position}_container";
    }

    string GetMultiContainerName(MultiNoteData multiNoteData)
    {
        return $"multi_row{multiNoteData.rowId}_pos{multiNoteData.position}_container";
    }

    Vector2 CalculateWorldPosition(NoteData noteData)
    {
        Vector2 basePos = parser.GridWorld(noteData.rowId, noteData.position);
        float indentOffset = noteData.indentLevel * parser.cellSize * parser.visualScale; // ����ҲҪ����
        return new Vector2(basePos.x + indentOffset, basePos.y);
    }

    Vector2 CalculateSymbolPosition(NoteData noteData)
    {
        Vector2 basePos = CalculateWorldPosition(noteData);
        // ����λ��ҲҪ����
        float symbolOffset = Mathf.Min(0.5f, noteData.length * 0.5f) * parser.cellSize * parser.visualScale;
        return new Vector2(basePos.x + symbolOffset, basePos.y);
    }

    NotePartPrefabs GetNotePartPrefabs(string noteType)
    {
        switch (noteType)
        {
            case "tap":
                return new NotePartPrefabs(tapHeadPrefab, tapBodyPrefab, tapTailPrefab);
            case "break":
                return new NotePartPrefabs(breakHeadPrefab, breakBodyPrefab, breakTailPrefab);
            case "hold":
                return new NotePartPrefabs(holdHeadPrefab, holdBodyPrefab, holdTailPrefab);
            case "track":
                return new NotePartPrefabs(trackHeadPrefab, trackBodyPrefab, trackTailPrefab);
            default:
                Debug.LogWarning($"δ֪����������: {noteType}");
                return null;
        }
    }

    void ClearAllSpawnedObjects()
    {
        foreach (GameObject obj in allSpawnedObjects)
        {
            if (obj != null)
                DestroyImmediate(obj);
        }
        allSpawnedObjects.Clear();
    }

    [ContextMenu("����������������")]
    void RegenerateAllNotes()
    {
        ClearAllSpawnedObjects();

        if (parser != null)
        {
            parser.ParseChart();
            SpawnAllNotes();
        }
    }

    // ������
    [System.Serializable]
    public class NotePartPrefabs
    {
        public GameObject headPrefab;
        public GameObject bodyPrefab;
        public GameObject tailPrefab;

        public NotePartPrefabs(GameObject head, GameObject body, GameObject tail)
        {
            headPrefab = head;
            bodyPrefab = body;
            tailPrefab = tail;
        }
    }

    public struct HoldLineData
    {
        public float[] positions;
        public float spacing;
        public int lineCount; // �������
    }

    // Multiָʾ�������������ֲ��䣩
    public class MultiIndicatorUpdater : MonoBehaviour
    {
        public MultiNoteData multiNoteData;
        private TextMeshPro textMesh;

        void Start()
        {
            textMesh = GetComponentInChildren<TextMeshPro>();
        }

        void Update()
        {
            if (textMesh != null && multiNoteData != null)
            {
                int remainingLayers = multiNoteData.GetRemainingLayers();
                textMesh.text = remainingLayers.ToString();

                if (remainingLayers <= 1)
                    textMesh.color = Color.red;
                else if (remainingLayers <= 3)
                    textMesh.color = Color.yellow;
                else
                    textMesh.color = Color.white;
            }
        }
    }
}