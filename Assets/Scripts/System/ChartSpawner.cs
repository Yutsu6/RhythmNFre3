using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;

public class ChartSpawner : MonoBehaviour
{
    [Header("解析器引用")]
    public ChartParser parser;

    [Header("音符部件预制体 - 头身尾分离")]
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

    [Header("符号预制体")]
    public GameObject loopSymbolPrefab;
    public GameObject ifSymbolPrefab;
    public GameObject breakSymbolPrefab;
    public GameObject continueSymbolPrefab;
    public GameObject returnSymbolPrefab;
    public GameObject commentSymbolPrefab;
    public GameObject speedSymbolF;
    public GameObject speedSymbolL;

    [Header("Multi相关")]
    public GameObject multiIndicatorPrefab;
    public GameObject holdLinePrefab; // Hold斜线预制体

    // 常量定义
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
        Debug.Log("等待解析器准备...");
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (parser == null)
        {
            parser = FindObjectOfType<ChartParser>();
            Debug.Log($"自动查找解析器: {parser != null}");
        }

        float timeout = 5f;
        float startTime = Time.time;

        while (parser == null || parser.notes == null || parser.notes.Count == 0)
        {
            if (Time.time - startTime > timeout)
            {
                Debug.LogError("等待解析器超时！");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log("解析器准备就绪，开始生成音符");
        SpawnAllNotes();
    }

    void SpawnAllNotes()
    {
        if (parser == null || parser.notes.Count == 0)
        {
            Debug.LogError("没有谱面数据！");
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

        Debug.Log($"音符生成完成！共生成{allSpawnedObjects.Count}个对象");
    }

    void SpawnSingleNote(NoteData noteData)
    {
        Debug.Log($"生成单音符: 行{noteData.rowId} 位置{noteData.position} 类型{noteData.type} 长度{noteData.length}");

        // 创建音符容器
        GameObject noteContainer = new GameObject(GetNoteContainerName(noteData));
        noteContainer.transform.position = CalculateWorldPosition(noteData);
        noteContainer.transform.SetParent(this.transform);
        allSpawnedObjects.Add(noteContainer);

        // 生成头身尾部件
        SpawnNoteParts(noteContainer, noteData);

        // 设置音符对象引用
        noteData.noteObject = noteContainer;

        // 生成符号
        SpawnStructureSymbols(noteData, noteContainer);
    }

    void SpawnNoteParts(GameObject container, NoteData noteData)
    {
        float totalLength = noteData.length;
        float bodyLength = Mathf.Max(MIN_BODY_LENGTH, totalLength - HEAD_LENGTH - TAIL_LENGTH);

        Debug.Log($"音符分段: 总长={totalLength}, 头={HEAD_LENGTH}, 身={bodyLength}, 尾={TAIL_LENGTH}");

        // 获取对应类型的预制体
        var prefabs = GetNotePartPrefabs(noteData.type);
        if (prefabs == null)
        {
            Debug.LogError($"无法获取音符类型 '{noteData.type}' 的预制体");
            return;
        }

        // 生成头部
        GameObject headPart = SpawnNotePart(container, prefabs.headPrefab, HEAD_LENGTH, 0f, "Head");
        if (headPart != null)
        {
            SetupPartVisual(headPart, "head", noteData.type);
        }

        // 生成身体（如果长度足够）
        if (bodyLength > MIN_BODY_LENGTH)
        {
            GameObject bodyPart = SpawnNotePart(container, prefabs.bodyPrefab, bodyLength, HEAD_LENGTH, "Body");
            if (bodyPart != null)
            {
                SetupPartVisual(bodyPart, "body", noteData.type);

                // 特殊处理：Hold音符的斜线
                if (noteData.type == "hold")
                {
                    SpawnHoldLines(bodyPart, bodyLength);
                }
            }
        }

        // 生成尾部
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
            Debug.LogWarning($"音符部件预制体为空: {partName}");
            return null;
        }

        GameObject part = Instantiate(prefab, container.transform);
        part.name = partName;

        // 修改这里：应用视觉缩放
        Vector3 originalScale = part.transform.localScale;
        part.transform.localScale = new Vector3(
            length * parser.cellSize * parser.visualScale,
            originalScale.y * parser.visualScale,
            originalScale.z
        );

        // 修改这里：偏移位置也要缩放
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
                renderer.color = new Color(baseColor.r * 1.2f, baseColor.g * 1.2f, baseColor.b * 1.2f); // 更亮
                break;
            case "body":
                renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.6f); // 半透明
                break;
            case "tail":
                renderer.color = new Color(baseColor.r * 0.8f, baseColor.g * 0.8f, baseColor.b * 0.8f); // 更暗
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
        // 计算整个hold音符的总长度
        float totalHoldLength = bodyLength + HEAD_LENGTH + TAIL_LENGTH;

        HoldLineData lineData = CalculateHoldLinePositions(totalHoldLength);

        Debug.Log($"生成Hold斜线: 数量={lineData.positions.Length}, 世界间距={lineData.spacing:F3}");

        for (int i = 0; i < lineData.positions.Length; i++)
        {
            // 世界坐标位置转换为body局部坐标
            // body的局部坐标系中，0对应世界坐标的HEAD_LENGTH，1对应世界坐标的HEAD_LENGTH + bodyLength
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

        // 设置局部位置（body的局部坐标系是0~1）
        line.transform.localPosition = new Vector3(
            localPosition,
            0.5f * parser.visualScale,  // Y位置也要缩放
            -0.1f
        );

        // 设置缩放：标准宽度1/6，需要抵消body的缩放
        float standardLineWidth = 1f / 6f;
        float localScaleX = standardLineWidth / bodyLength;

        // 修正：Y缩放设置为0.68
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

        // 特殊情况：长度为1时的固定方案
        if (Mathf.Abs(totalHoldLength - 1.0f) < 0.01f)
        {
            return new HoldLineData
            {
                positions = new float[] { 0.3f, 0.5f, 0.7f },
                spacing = 0.2f,
                lineCount = 3
            };
        }

        // 计算可用空间：从0.3到totalHoldLength-0.3
        float startPos = 0.3f;
        float endPos = totalHoldLength - 0.3f;
        float availableSpace = endPos - startPos;

        if (availableSpace <= 0f)
        {
            Debug.LogWarning($"Hold长度{totalHoldLength}太小，无法放置竖线");
            return new HoldLineData { positions = new float[0], spacing = 0f, lineCount = 0 };
        }

        Debug.Log($"Hold总长度: {totalHoldLength}, 可用空间: {availableSpace} (从{startPos}到{endPos})");

        // 计算可以放置多少根HoldLine（间距数量 = HoldLine数量 - 1）
        // 每根HoldLine占用空间：间距 + HoldLine宽度(0.167)
        float lineWidth = 1f / 6f;

        // 计算最大和最小可能的HoldLine数量
        int maxLines = Mathf.FloorToInt(availableSpace / (HOLD_LINE_SPACING_MIN + lineWidth)) + 1;
        int minLines = Mathf.CeilToInt(availableSpace / (HOLD_LINE_SPACING_MAX + lineWidth)) + 1;

        // 确保至少3根（如果空间足够）
        minLines = Mathf.Max(3, minLines);
        maxLines = Mathf.Max(minLines, maxLines);

        Debug.Log($"HoldLine数量范围: {minLines} ~ {maxLines}");

        // 寻找最优解
        int bestLineCount = -1;
        float bestSpacing = 0f;
        float minSpacingDiff = float.MaxValue;

        for (int lineCount = minLines; lineCount <= maxLines; lineCount++)
        {
            if (lineCount <= 1) continue;

            // 计算间距：总可用空间减去所有HoldLine的宽度，然后除以间距数量
            float totalLineWidth = lineWidth * lineCount;
            float totalSpacingSpace = availableSpace - totalLineWidth;
            float spacing = totalSpacingSpace / (lineCount - 1);

            float spacingDiff = Mathf.Abs(spacing - HOLD_LINE_SPACING_OPTIMAL);

            Debug.Log($"测试 {lineCount}根HoldLine: 间距={spacing:F3}, 差值={spacingDiff:F3}");

            if (spacingDiff < minSpacingDiff &&
                spacing >= HOLD_LINE_SPACING_MIN &&
                spacing <= HOLD_LINE_SPACING_MAX)
            {
                minSpacingDiff = spacingDiff;
                bestLineCount = lineCount;
                bestSpacing = spacing;
            }
        }

        // 如果没有找到合适解，使用固定3根
        if (bestLineCount == -1)
        {
            bestLineCount = 3;
            bestSpacing = (availableSpace - (lineWidth * bestLineCount)) / (bestLineCount - 1);
            Debug.LogWarning($"使用固定方案: {bestLineCount}根HoldLine, 间距={bestSpacing:F3}");
        }

        // 生成HoldLine中心点位置
        float currentPos = startPos + lineWidth / 2f; // 第一个HoldLine中心

        for (int i = 0; i < bestLineCount; i++)
        {
            positions.Add(currentPos);

            // 下一个HoldLine中心位置 = 当前中心 + HoldLine宽度 + 间距
            if (i < bestLineCount - 1)
            {
                currentPos += lineWidth + bestSpacing;
            }
        }

        Debug.Log($"最终方案: {bestLineCount}根HoldLine, 间距={bestSpacing:F3}");
        Debug.Log($"HoldLine中心位置: {string.Join(", ", positions.Select(p => p.ToString("F3")))}");

        // 验证最后一个HoldLine位置是否正确
        float lastLineCenter = positions[positions.Count - 1];
        float lastLineEnd = lastLineCenter + lineWidth / 2f;
        Debug.Log($"验证: 最后一个HoldLine结束位置={lastLineEnd:F3}, 要求位置={endPos:F3}, 差值={Mathf.Abs(lastLineEnd - endPos):F3}");

        return new HoldLineData
        {
            positions = positions.ToArray(),
            spacing = bestSpacing,
            lineCount = bestLineCount
        };
    }

    public void SpawnMultiNote(MultiNoteData multiNoteData)
    {
        Debug.Log($"生成Multi音符: 行{multiNoteData.rowId} 位置{multiNoteData.position} 总长{multiNoteData.length}");

        multiNoteData.hasEnteredJudgmentQueue = false;

        if (multiNoteData.IsComplete())
        {
            Debug.LogWarning("Multi音符在生成时已经完成，跳过生成");
            return;
        }

        var currentLayer = multiNoteData.GetCurrentLayer();
        if (currentLayer == null)
        {
            Debug.LogWarning("Multi音符没有有效的层！");
            return;
        }

        // 创建Multi容器
        GameObject multiContainer = new GameObject(GetMultiContainerName(multiNoteData));
        multiContainer.transform.position = CalculateWorldPosition(multiNoteData);
        multiContainer.transform.SetParent(this.transform);
        allSpawnedObjects.Add(multiContainer);

        // 使用当前层的类型生成头身尾
        SpawnNoteParts(multiContainer, multiNoteData);

        multiNoteData.noteObject = multiContainer;

        // 添加Multi指示器
        SpawnMultiIndicator(multiContainer, multiNoteData);
        SpawnStructureSymbols(multiNoteData, multiContainer);

        Debug.Log($"Multi音符生成完成: 当前层={currentLayer.type}({multiNoteData.currentLayerIndex + 1}/{multiNoteData.layers.Count})");
    }

    void SpawnMultiIndicator(GameObject multiObject, MultiNoteData multiNoteData)
    {
        if (multiIndicatorPrefab == null)
        {
            Debug.LogWarning("Multi指示器预制体未设置！");
            return;
        }

        GameObject indicator = Instantiate(multiIndicatorPrefab, multiObject.transform);

        // 设置位置：音符右侧
        float noteWidth = multiNoteData.length * parser.cellSize;
        indicator.transform.localPosition = new Vector3(
            noteWidth * 0.5f + 0.2f, // 右侧偏移
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
            Debug.LogWarning("Multi指示器预制体中没有找到TextMeshPro组件！");
        }
    }

    public void SpawnStructureSymbols(NoteData noteData, GameObject parentNote)
    {
        if (noteData.structureSymbols.Count == 0) return;

        Debug.Log($"生成结构符号: {noteData.structureSymbols.Count}个");

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
                    Debug.LogWarning($"未知的结构符号类型: {symbol.symbolType}");
                    break;
            }
        }
    }

    void SpawnSymbol(GameObject symbolPrefab, GameObject parent, Vector2 position, string name)
    {
        if (symbolPrefab == null)
        {
            Debug.LogWarning($"符号预制体未设置: {name}");
            return;
        }

        GameObject symbol = Instantiate(symbolPrefab, parent.transform);
        symbol.transform.position = new Vector3(position.x, position.y, -0.1f);
        symbol.name = name;
        allSpawnedObjects.Add(symbol);

        Debug.Log($"生成符号: {name}");
    }

    void SpawnSpeedSymbol(NoteData noteData, GameObject parentNote, SpeedSymbol speedSymbol)
    {
        GameObject prefabToUse = speedSymbol.speedType == "F" ? speedSymbolF : speedSymbolL;
        if (prefabToUse == null)
        {
            Debug.LogWarning($"变速符号预制体未设置: {speedSymbol.speedType}");
            return;
        }

        Vector2 symbolPosition = CalculateSymbolPosition(noteData);
        GameObject symbolObj = Instantiate(prefabToUse, parentNote.transform);
        symbolObj.transform.position = new Vector3(symbolPosition.x, symbolPosition.y, -0.1f);
        symbolObj.name = $"SpeedSymbol_{speedSymbol.speedType}";
        allSpawnedObjects.Add(symbolObj);

        Debug.Log($"生成变速符号: {speedSymbol.speedType}");
    }

    // 辅助方法
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
        float indentOffset = noteData.indentLevel * parser.cellSize * parser.visualScale; // 缩进也要缩放
        return new Vector2(basePos.x + indentOffset, basePos.y);
    }

    Vector2 CalculateSymbolPosition(NoteData noteData)
    {
        Vector2 basePos = CalculateWorldPosition(noteData);
        // 符号位置也要缩放
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
                Debug.LogWarning($"未知的音符类型: {noteType}");
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

    [ContextMenu("重新生成所有音符")]
    void RegenerateAllNotes()
    {
        ClearAllSpawnedObjects();

        if (parser != null)
        {
            parser.ParseChart();
            SpawnAllNotes();
        }
    }

    // 辅助类
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
        public int lineCount; // 添加这行
    }

    // Multi指示器更新器（保持不变）
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