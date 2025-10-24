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
    // ���࿪ͷ����ֶ�
    private string targetDifficulty = ""; // ��������Ϊ "1", "2" ���ض��Ѷ�

    public List<NoteData> notes = new List<NoteData>();

    public TextAsset chartTextFile;

    public Vector2 gridOrigin = Vector2.zero; //����ԭ��
    public float cellSize = 1.0f;           //��λ��С
    public float rowHeight = 1.5f;          //��ֱ���

    public float visualScale = 0.8f;        // �������Ӿ�����ϵ��


    // ������Meta����
    [System.Serializable]
    public class MetaData
    {
        public string title = "";
        public string artist = "";
        public float bpm = 120f;           // Ĭ��BPM
        public float offset = 0f;          // Ĭ��ƫ��
        public int difficulty = 1;
        public Dictionary<string, string> levelDesigners = new Dictionary<string, string>();
    }

    public MetaData metaData = new MetaData();

    private void Start()
    {
        ParseChart();
    }

    public void ParseChart()
    {
        notes.Clear();
        metaData = new MetaData(); // ����Meta����

        if (chartTextFile == null)
        {
            Debug.LogError("û�������ļ�����");
            return;
        }

        string fileContent = chartTextFile.text;
        string[] lines = fileContent.Split('\n');

        int currentRowId = -1;  //��ǰ���ڴ������ID
        bool inMainSection = false;
        bool inMetaSection = false;
        int currentIndent = 0; // ��ǰ�е�������

        for (int i = 0; i < lines.Length; i++)
        {
            string originalLine = lines[i];
            string line = originalLine.Trim();
            Debug.Log($"��{i}��: '{line}'");

            // ��������
            if (string.IsNullOrEmpty(line))
            {
                Debug.Log($"  ��������");
                continue;
            }

            // ����ע�ͣ�������ע�ͺͽṹ����ע��
            line = ProcessComments(line, originalLine);

            // ������ж���ע�ͣ���������
            if (string.IsNullOrEmpty(line))
            {
                Debug.Log($"  ����ע����");
                continue;
            }

            // �޸�����ʶ�𲿷�
            if (line.StartsWith("#"))
            {
                // ����Ƿ���Main����
                if (line.StartsWith("# Main"))
                {
                    // ���Ѷ���Ϣ����ParseMetaData����
                    string metaLine = line.Substring(1).Trim(); // ȥ�� "#" ����
                    ParseMetaData(metaLine);

                    // ����Ƿ�Ӧ�ý�������Ѷ�
                    if (line.Contains("_"))
                    {
                        string[] parts = line.Split('_');
                        if (parts.Length > 1)
                        {
                            string difficulty = parts[1].Trim();
                            if (!string.IsNullOrEmpty(targetDifficulty) && difficulty != targetDifficulty)
                            {
                                inMainSection = false;
                                Debug.Log($"�����Ѷ�{difficulty}��Ŀ���Ѷ���{targetDifficulty}");
                                continue;
                            }
                        }
                    }

                    inMainSection = true;
                    inMetaSection = false;
                    Debug.Log("��������������");
                    continue;
                }
                else if (line.StartsWith("# Meta"))
                {
                    inMainSection = false;
                    inMetaSection = true;
                    Debug.Log("����Ԫ��Ϣ����");
                    continue;
                }
                else if (line.StartsWith("# Func"))
                {
                    inMainSection = false;
                    inMetaSection = false;
                    Debug.Log("���뺯������");
                    continue;
                }
                else
                {
                    inMainSection = false;
                    inMetaSection = false;
                    Debug.Log($"δ֪��������: {line}");
                    continue;
                }
            }

            // ����Meta��������
            if (inMetaSection)
            {
                ParseMetaData(line);
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

                    // ����Ƿ������û�������ţ�
                    if (IsCommandFormat(content))
                    {
                        NoteData newNote = new NoteData();
                        newNote.rowId = currentRowId;
                        newNote.position = notePosition;
                        newNote.indentLevel = currentIndent;

                        // ������������
                        ParseCommand(content, newNote);
                        notes.Add(newNote);
                    }
                    // ����Ƿ��ǵ�Ԫ��䣨�������ţ�
                    else if (content.StartsWith("[") && content.EndsWith("]"))
                    {
                        string innerContent = content.Substring(1, content.Length - 2).Trim();

                        NoteData newNote = new NoteData();
                        newNote.rowId = currentRowId;
                        newNote.position = notePosition;
                        newNote.indentLevel = currentIndent;

                        //��Ԫ���ͺͳ���
                        ParseNoteContent(innerContent, newNote);
                        notes.Add(newNote);
                    }
                    else
                    {
                        Debug.LogWarning($"�޷�ʶ��ĵ�Ԫ��ʽ: {content}");
                    }
                }
            }
        }
    }

    // �޸ģ�����Meta���ݣ�������ո���Ѷ���Ϣ�����
    void ParseMetaData(string line)
    {
        int colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
        {
            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();

            // ����������Ƴ����пո���Ʊ��
            string cleanKey = System.Text.RegularExpressions.Regex.Replace(key, @"\s+", "");

            switch (cleanKey.ToLower())
            {
                case "title":
                    metaData.title = value;
                    Debug.Log($"���ñ���: {value}");
                    break;

                case "artist":
                    metaData.artist = value;
                    Debug.Log($"����������: {value}");
                    break;

                case "bpm":
                    if (float.TryParse(value, out float bpm))
                    {
                        metaData.bpm = bpm;
                        Debug.Log($"����BPM: {bpm}");
                    }
                    else
                    {
                        Debug.LogError($"�޷�����BPM: {value}");
                    }
                    break;

                case "offset":
                    // �����+�ŵ�ƫ��ֵ
                    string cleanValue = value.Replace("+", "").Trim();
                    if (float.TryParse(cleanValue, out float offset))
                    {
                        metaData.offset = offset;
                        Debug.Log($"����ƫ��: {offset}");
                    }
                    else
                    {
                        Debug.LogError($"�޷�����ƫ��: {value}");
                    }
                    break;

                case "main":
                    // �����Ѷ���Ϣ���� "Main_1"
                    if (key.Contains("_"))
                    {
                        string[] parts = key.Split('_');
                        if (parts.Length > 1)
                        {
                            string difficulty = parts[1].Trim();
                            Debug.Log($"��⵽�Ѷ�: {difficulty}");

                            // ���������Ŀ���Ѷȣ����µ�ǰ����״̬
                            if (string.IsNullOrEmpty(targetDifficulty) || difficulty == targetDifficulty)
                            {
                                // �����������һЩ�Ѷ���ص�״̬
                                Debug.Log($"�����Ѷ�: {difficulty}");
                            }
                            else
                            {
                                Debug.Log($"�����Ѷ�: {difficulty}");
                            }
                        }
                    }
                    break;

                default:
                    // ���� level_x �� designer_x ��ʽ
                    if (cleanKey.StartsWith("level_"))
                    {
                        string levelNumber = cleanKey.Substring(6); // ȥ�� "level_"
                        metaData.levelDesigners[levelNumber] = value;
                        Debug.Log($"�����Ѷ�{levelNumber}���ʦ: {value}");
                    }
                    else if (cleanKey.StartsWith("designer_"))
                    {
                        string levelNumber = cleanKey.Substring(9); // ȥ�� "designer_"
                        metaData.levelDesigners[levelNumber] = value;
                        Debug.Log($"�����Ѷ�{levelNumber}���ʦ: {value}");
                    }
                    else
                    {
                        Debug.LogWarning($"δ֪��Meta��: {cleanKey}");
                    }
                    break;
            }
        }
        else
        {
            // ����û��ð�ŵ���������� "# Main_1"
            string cleanLine = line.Trim();
            if (cleanLine.StartsWith("Main_"))
            {
                string difficulty = cleanLine.Substring(5); // ȥ�� "Main_"
                Debug.Log($"��⵽�Ѷ�����: {difficulty}");

                // ���������Ŀ���Ѷȣ����µ�ǰ����״̬
                if (string.IsNullOrEmpty(targetDifficulty) || difficulty == targetDifficulty)
                {
                    Debug.Log($"�����Ѷ�: {difficulty}");
                }
                else
                {
                    Debug.Log($"�����Ѷ�: {difficulty}");
                }
            }
        }
    }

    //��ȡ�Ѷ�
    public int Difficulty()
    {
        return metaData.difficulty;
    }

    // ��������ȡBPM
    public float GetBPM()
    {
        return metaData.bpm;
    }

    // ��������ȡƫ��
    public float GetOffset()
    {
        return metaData.offset;
    }

    // ��������ȡ����
    public string GetTitle()
    {
        return metaData.title;
    }

    // ��������ȡ������
    public string GetArtist()
    {
        return metaData.artist;
    }

    // ��������ȡָ���Ѷȵ����ʦ
    public string GetDesigner(string level)
    {
        if (metaData.levelDesigners.ContainsKey(level))
            return metaData.levelDesigners[level];
        return "";
    }



    /// <summary>
    /// ����ע�ͣ�������ע�ͺͽṹ����ע��
    /// </summary>
    string ProcessComments(string line, string originalLine)
    {
        Debug.Log($"��ʼ����ע��: '{line}'");

        // ���1������ע�ͣ���//��ͷ��- ��ȫ����
        if (line.StartsWith("//"))
        {
            Debug.Log($"��⵽����ע�ͣ�����: {line}");
            return "";
        }

        // �������е�//λ��
        List<int> commentPositions = FindAllCommentPositions(line);

        if (commentPositions.Count == 0)
        {
            Debug.Log($"û�м�⵽ע��");
            return line;
        }

        // ���ÿ��//��λ��
        foreach (int commentPos in commentPositions)
        {
            bool isInBrackets = IsInsideBrackets(originalLine, commentPos);

            if (isInBrackets)
            {
                Debug.Log($"λ��{commentPos}��//�ڷ������ڣ�����Ϊ�ṹ����");
            }
            else
            {
                Debug.Log($"λ��{commentPos}��//������ע�ͣ��Ƴ���������");
                return line.Substring(0, commentPos).Trim();
            }
        }

        // ����//���ڷ������ڣ���������
        Debug.Log($"����//���ڷ������ڣ���������");
        return line;
    }

    List<int> FindAllCommentPositions(string line)
    {
        List<int> positions = new List<int>();
        int index = 0;

        while (index < line.Length)
        {
            int found = line.IndexOf("//", index);
            if (found < 0) break;

            positions.Add(found);
            index = found + 2; // ����"//"
        }

        Debug.Log($"�ҵ�{positions.Count}��//λ��: {string.Join(", ", positions)}");
        return positions;
    }

    /// <summary>
    /// ���ָ���ı��Ƿ��ڷ�������
    /// </summary>
    bool IsInsideBrackets(string line, int position)
    {
        if (position < 0 || position >= line.Length) return false;

        // ����position֮ǰδƥ���[����
        int unmatchedBrackets = 0;

        for (int i = 0; i < position; i++)
        {
            if (line[i] == '[')
            {
                unmatchedBrackets++;
            }
            else if (line[i] == ']')
            {
                if (unmatchedBrackets > 0)
                    unmatchedBrackets--;
            }
        }

        bool isInside = unmatchedBrackets > 0;
        Debug.Log($"λ��{position}: δƥ��[����={unmatchedBrackets}, �ڷ�������={isInside}");

        return isInside;
    }

    public Vector2 GridWorld(int rowId, float position)
    {
        // �޸����Ӧ���Ӿ�����
        float worldX = gridOrigin.x + position * cellSize * visualScale;
        float worldY = gridOrigin.y + rowId * rowHeight * visualScale;

        return new Vector2(worldX, worldY);
    }

    void ParseNoteContent(string content, NoteData noteData)
    {
        if (string.IsNullOrEmpty(content))
        {
            // �����ݣ�����Ϊ1������
            noteData.type = "track";
            noteData.length = 1f;
            return;
        }

        // ���ȼ���Ƿ��������ʽ
        if (IsCommandFormat(content))
        {
            ParseCommand(content, noteData);
            return;
        }


        Debug.Log($"������������: '{content}'");

        // ����Ĭ��ֵ
        noteData.type = "track";
        noteData.length = 1f;

        // ���ֺŷָ�����
        string[] parts = SplitNoteContent(content);

        Debug.Log($"�ָ�Ϊ {parts.Length} ����: {string.Join(" | ", parts)}");

        // ��˳�����ÿ������
        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            string trimmedPart = part.Trim();

            // 1. ����Ƿ��ǳ��ȣ����֣�
            if (IsLength(trimmedPart))
            {
                if (float.TryParse(trimmedPart, out float length) && length > 0)
                {
                    noteData.length = length;
                    Debug.Log($"���ó���: {length}");
                    continue;
                }
            }

            // 2. ����Ƿ����������ͣ�0, t, h, b��
            if (IsNoteType(trimmedPart))
            {
                noteData.type = MapTypeToFullName(trimmedPart);
                Debug.Log($"������������: {noteData.type}");
                continue;
            }

            // 3. ����Ƿ���Multi������{...}��
            if (IsMultiNoteDefinition(trimmedPart))
            {
                ParseMultiNoteDefinition(trimmedPart, noteData);
                continue;
            }

            // 4. ����Ƿ��ǽṹ����
            if (IsStructureSymbol(trimmedPart))
            {
                ParseStructureSymbols(trimmedPart, noteData);
                continue;
            }

            // 5. ����Ƿ��Ǳ��ٷ��ţ�F, L��
            if (IsSpeedSymbol(trimmedPart))
            {
                ParseSpeedSymbol(trimmedPart, noteData);
                continue;
            }

            // 6. ����Ƿ�����ʱ���ȣ�_���֣�
            if (IsDelayLength(trimmedPart))
            {
                ParseDelayLength(trimmedPart, noteData);
                continue;
            }

            Debug.LogWarning($"�޷�ʶ��Ĳ���: '{trimmedPart}'");
        }

        Debug.Log($"����������� - ����: {noteData.type}, ����: {noteData.length}, ����: {noteData.GetSymbolsDebugInfo()}");
    }

    // �޸ģ�����ȷ�������ʽ���
    bool IsCommandFormat(string content)
    {
        // �����ʽ������
        // 1. ���������� {}
        // 2. ������ǰ������Ҫô����������Ҫô��(ִ����)������
        // 3. �����ǽṹ���Ÿ�ʽ����loop{4}��

        if (!content.Contains("{") || !content.Contains("}"))
            return false;

        // ����Ƿ�����֪�Ľṹ���Ÿ�ʽ
        if (IsStructureSymbol(content))
        {
            Debug.Log($"��⵽�ṹ���ţ���������: {content}");
            return false;
        }

        int braceStart = content.IndexOf('{');
        int braceEnd = content.LastIndexOf('}');

        if (braceStart < 0 || braceEnd < braceStart)
            return false;

        string beforeBraces = content.Substring(0, braceStart).Trim();
        Debug.Log($"������ǰ������: '{beforeBraces}'");

        // ����Ƿ�����Ч�������ʽ
        // ���1: (ִ����)������{����}
        if (beforeBraces.Contains("(") && beforeBraces.Contains(")"))
        {
            int parenStart = beforeBraces.IndexOf('(');
            int parenEnd = beforeBraces.IndexOf(')');

            if (parenStart >= 0 && parenEnd > parenStart)
            {
                // ��ȡִ���벿��
                string codesStr = beforeBraces.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                Debug.Log($"��ȡִ����: '{codesStr}'");

                // ��ȡ���������֣����ź�����ݣ�
                string commandName = beforeBraces.Substring(parenEnd + 1).Trim();
                Debug.Log($"��ȡ������: '{commandName}'");

                if (IsValidExecutionCodes(codesStr) && IsValidCommandName(commandName))
                {
                    Debug.Log($"��⵽��ִ����������ʽ: {content}");
                    return true;
                }
                else
                {
                    Debug.Log($"ִ�������������Ч - ִ����: {IsValidExecutionCodes(codesStr)}, ������: {IsValidCommandName(commandName)}");
                }
            }
        }
        // ���2: ������{����}����ִ���룩
        else if (IsValidCommandName(beforeBraces))
        {
            Debug.Log($"��⵽��ִ����������ʽ: {content}");
            return true;
        }
        else
        {
            Debug.Log($"������Ч����������ִ�����ʽ: {beforeBraces}");
        }

        Debug.Log($"������Ч�������ʽ: {content}");
        return false;
    }

    // ����������Ƿ�����Ч��ִ�����ʽ
    bool IsValidExecutionCodes(string codesStr)
    {
        if (string.IsNullOrEmpty(codesStr))
        {
            Debug.Log("ִ����Ϊ��");
            return false;
        }

        string[] codeStrs = codesStr.Split(',');
        Debug.Log($"����ִ����: {codesStr} -> {codeStrs.Length}����");

        foreach (string codeStr in codeStrs)
        {
            string trimmedCode = codeStr.Trim();
            if (!int.TryParse(trimmedCode, out int code))
            {
                Debug.Log($"�޷�����ִ����: '{trimmedCode}'");
                return false;
            }
            if (code != 0 && code != 1)
            {
                Debug.Log($"ִ����ֵ��Ч(������0��1): {code}");
                return false;
            }
        }

        Debug.Log($"ִ������֤ͨ��: {codesStr}");
        return true;
    }

    // ����������Ƿ�����Ч��������
    bool IsValidCommandName(string name)
    {
        // ��֪���������б�
        string[] validCommands = { "speedChange", "bpmChange", "tagCreat", "tagDelete", "logCreate",
                              "colorChange", "loopColorChange", "caseColorChange" };

        bool isValid = validCommands.Contains(name);
        Debug.Log($"��������� '{name}': {(isValid ? "��Ч" : "��Ч")}");

        return isValid;
    }


    // �޸ģ����������
    void ParseCommand(string content, NoteData noteData)
    {
        noteData.hasCommand = true;
        noteData.type = "command"; // �������ͣ������ɿ�������

        Debug.Log($"=== ��ʼ�������� ===");
        Debug.Log($"��������: {content}");

        // ����ִ���������
        int braceStart = content.IndexOf('{');
        int braceEnd = content.LastIndexOf('}');

        if (braceStart < 0 || braceEnd < braceStart)
        {
            Debug.LogError($"�����ʽ����: {content}");
            return;
        }

        string beforeBraces = content.Substring(0, braceStart).Trim();
        string commandWithParams = content.Substring(braceStart, braceEnd - braceStart + 1);

        Debug.Log($"������ǰ������: '{beforeBraces}'");

        // ����ִ�����������
        if (beforeBraces.Contains("(") && beforeBraces.Contains(")"))
        {
            int parenStart = beforeBraces.IndexOf('(');
            int parenEnd = beforeBraces.IndexOf(')');

            if (parenStart >= 0 && parenEnd > parenStart)
            {
                // ��ȡִ���벿��
                string codesStr = beforeBraces.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                noteData.executionCodes = ParseExecutionCodes(codesStr);
                Debug.Log($"����ִ����: {string.Join(",", noteData.executionCodes)}");

                // ��ȡ���������֣����ź�����ݣ�
                string commandName = beforeBraces.Substring(parenEnd + 1).Trim();

                // �����������Ͳ���
                ParseCommandNameAndParams(commandName + commandWithParams, noteData);
            }
            else
            {
                Debug.LogError($"���Ÿ�ʽ����: {beforeBraces}");
                return;
            }
        }
        else
        {
            // û��ִ���룬ʹ��Ĭ����Ϊ���������ʾĬ��ִ��1�Σ�
            noteData.executionCodes = new int[0];
            Debug.Log($"��ִ���룬ʹ��Ĭ����Ϊ");

            // �����������Ͳ���
            ParseCommandNameAndParams(commandWithParams, noteData);
        }

        Debug.Log($"����������: {noteData.commandName}{{{noteData.commandParams}}}, ִ����: [{string.Join(",", noteData.executionCodes)}]");
        Debug.Log($"=== ���������� ===");
    }

    // ����������ִ����
    int[] ParseExecutionCodes(string codesStr)
    {
        if (string.IsNullOrEmpty(codesStr)) return new int[0];

        string[] codeStrs = codesStr.Split(',');
        List<int> codes = new List<int>();

        foreach (string codeStr in codeStrs)
        {
            if (int.TryParse(codeStr.Trim(), out int code))
            {
                codes.Add(code);
            }
            else
            {
                Debug.LogWarning($"�޷�����ִ����: {codeStr}��ʹ��Ĭ��ֵ0");
                codes.Add(0);
            }
        }

        return codes.ToArray();
    }

    // �޸ģ������������Ͳ�������
    void ParseCommandNameAndParams(string commandWithParams, NoteData noteData)
    {
        // ��ʽ: ������{����}
        int braceStart = commandWithParams.IndexOf('{');

        if (braceStart < 0)
        {
            Debug.LogError($"�����ʽ����ȱ�ٻ�����: {commandWithParams}");
            return;
        }

        string commandName = commandWithParams.Substring(0, braceStart).Trim();
        string parameters = commandWithParams.Substring(braceStart + 1, commandWithParams.Length - braceStart - 2).Trim();

        noteData.commandName = commandName;
        noteData.commandParams = parameters;

        Debug.Log($"������: '{commandName}', ����: '{parameters}'");
    }


    // �ָ��������ݣ����ǻ������ڵĶ��ţ�
    string[] SplitNoteContent(string content)
    {
        List<string> parts = new List<string>();
        int currentIndex = 0;
        int braceDepth = 0;

        while (currentIndex < content.Length)
        {
            int nextSemicolon = content.IndexOf(';', currentIndex);

            // ���û�зֺ��ˣ����ʣ�ಿ��
            if (nextSemicolon < 0)
            {
                string remaining = content.Substring(currentIndex).Trim();
                if (!string.IsNullOrEmpty(remaining))
                    parts.Add(remaining);
                break;
            }

            // ���ֺ��Ƿ��ڻ�������
            for (int i = currentIndex; i < nextSemicolon; i++)
            {
                if (content[i] == '{') braceDepth++;
                else if (content[i] == '}') braceDepth--;
            }

            if (braceDepth > 0)
            {
                // �ֺ��ڻ������ڣ�����
                currentIndex = nextSemicolon + 1;
            }
            else
            {
                // �����ֺţ��ָ�
                string part = content.Substring(currentIndex, nextSemicolon - currentIndex).Trim();
                if (!string.IsNullOrEmpty(part))
                    parts.Add(part);
                currentIndex = nextSemicolon + 1;
            }
        }

        return parts.ToArray();
    }

    // ����Ƿ��ǳ���
    bool IsLength(string part)
    {
        return float.TryParse(part, out _);
    }


    // ����Ƿ���Multi��������
    bool IsMultiNoteDefinition(string part)
    {
        return part.StartsWith("{") && part.EndsWith("}");
    }

    // ����Ƿ��ǽṹ����
    bool IsStructureSymbol(string part)
    {
        return part.StartsWith("loop{") || part.StartsWith("if{") ||
               part == "break" || part == "continue" || part == "return" ||
               part.StartsWith("//");
    }

    // ����Ƿ��Ǳ��ٷ���
    bool IsSpeedSymbol(string part)
    {
        return part == "F" || part == "L";
    }

    // ����Ƿ�����ʱ����
    bool IsDelayLength(string part)
    {
        return part.StartsWith("_") && part.Length > 1;
    }

    // ����Multi��������
    void ParseMultiNoteDefinition(string multiDefinition, NoteData noteData)
    {
        // ����MultiNoteDataʵ��
        MultiNoteData multiNote = new MultiNoteData();

        // ���ƻ�������
        multiNote.rowId = noteData.rowId;
        multiNote.position = noteData.position;
        multiNote.indentLevel = noteData.indentLevel;
        multiNote.isMultiNote = true;
        multiNote.length = noteData.length; // ʹ���ѽ����ĳ���

        // �����㶨��
        string layersPart = multiDefinition.Substring(1, multiDefinition.Length - 2).Trim();
        string[] layerValues = layersPart.Split(',');

        foreach (string layerValue in layerValues)
        {
            string trimmedValue = layerValue.Trim();
            MultiNoteLayer layer = CreateMultiLayerFromValue(trimmedValue);
            if (layer != null)
            {
                multiNote.layers.Add(layer);
            }
        }

        // �����ܹ���Ҫ����Ĵ���
        multiNote.totalHitsRequired = multiNote.GetRemainingHits();

        // �滻ԭ����noteData
        notes.Remove(noteData);
        notes.Add(multiNote);

        Debug.Log($"Multi�����������: {multiNote.layers.Count}��, ��Ҫ���{multiNote.GetRemainingHits()}��");
    }

    // �������ٷ���
    void ParseSpeedSymbol(string trimmedPart, NoteData noteData)
    {
        if (trimmedPart == "F" || trimmedPart == "L")
        {
            // �������ٷ��Ų���ӵ��ṹ�����б�
            SpeedSymbol speedSymbol = new SpeedSymbol();
            speedSymbol.Parse(trimmedPart, noteData);
            noteData.AddStructureSymbol(speedSymbol);

            Debug.Log($"�������ٷ���: {trimmedPart}");
        }
        else
        {
            Debug.LogWarning($"��Ч�ı��ٷ���: '{trimmedPart}'");
        }
    }

    // ������ʱ����
    void ParseDelayLength(string delayPart, NoteData noteData)
    {
        string delayValue = delayPart.Substring(1); // ȥ���»���
        if (float.TryParse(delayValue, out float delayLength))
        {
            // ��ʱֻ��¼����ʵ�ֹ���
            Debug.Log($"��⵽��ʱ����: {delayLength} (�ݲ�֧��)");
        }
    }

    // ��ֵ����Multi��
    MultiNoteLayer CreateMultiLayerFromValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        string layerType;
        float layerLength = 1f; // Ĭ�ϳ���

        // ����Ƿ��г�����Ϣ���� "4;h" ��ʽ��
        if (value.Contains(";"))
        {
            string[] parts = value.Split(';');
            if (parts.Length >= 2)
            {
                if (float.TryParse(parts[0].Trim(), out float length))
                {
                    layerLength = length;
                }
                layerType = MapMultiNoteType(parts[1].Trim());
            }
            else
            {
                layerType = MapMultiNoteType(value.Trim());
            }
        }
        else
        {
            layerType = MapMultiNoteType(value.Trim());
        }

        return new MultiNoteLayer(layerType, layerLength);
    }

    // ר������Multi����������ӳ��
    string MapMultiNoteType(string value)
    {
        switch (value.Trim())
        {
            case "0": return "track";
            case "1":
            case "t": return "tap";
            case "h": return "hold";
            case "b": return "break";
            default: return "track"; // Ĭ��ֵ
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
            case "0": return "track";
            default: return shortType;
        }
    }

    // �޸Ľ����ṹ���ŵķ���
    void ParseStructureSymbols(string symbolInfo, NoteData noteData)
    {
        Debug.Log($"=== ��ʼ�����ṹ���� ===");
        Debug.Log($"������Ϣ: '{symbolInfo}'");
        Debug.Log($"Ŀ������: ��{noteData.rowId} λ��{noteData.position}");

        StructureSymbol symbol = null;

        if (symbolInfo.StartsWith("loop{"))
        {
            Debug.Log("����Loop����");
            symbol = StructureSymbolFactory.CreateSymbol("loop");
        }
        else if (symbolInfo.StartsWith("if{"))
        {
            Debug.Log("����If����");
            symbol = StructureSymbolFactory.CreateSymbol("if");
        }
        else if (symbolInfo.StartsWith("break"))
        {
            Debug.Log("����Break����");
            symbol = StructureSymbolFactory.CreateSymbol("break");
        }
        else if (symbolInfo.StartsWith("continue"))
        {
            Debug.Log("����Continue����");
            symbol = StructureSymbolFactory.CreateSymbol("continue");
        }
        else if (symbolInfo.StartsWith("return"))
        {
            Debug.Log("����Return����");
            symbol = StructureSymbolFactory.CreateSymbol("return");
        }
        else if (symbolInfo.StartsWith("//"))
        {
            Debug.Log("����Comment����");
            symbol = StructureSymbolFactory.CreateSymbol("//");
        }

        if (symbol != null)
        {
            Debug.Log($"���Ŵ����ɹ�: {symbol.symbolType}");

            // ��������
            Debug.Log($"��ʼ������������: {symbolInfo}");
            symbol.Parse(symbolInfo, noteData);
            Debug.Log($"���Ž������");

            // ��ӵ�����
            Debug.Log($"��ӷ��ŵ�����");
            noteData.AddStructureSymbol(symbol);
            Debug.Log($"���������ɣ�����������{noteData.structureSymbols.Count}������");

            Debug.Log($"�ɹ�����{symbol.symbolType}���Ų���ӵ�����");
        }
        else
        {
            Debug.LogError($"���Ŵ���ʧ��: {symbolInfo}");
            Debug.LogWarning($"δ֪�Ľṹ��������: {symbolInfo}");
        }

        Debug.Log($"=== �ṹ���Ž������ ===");
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

    public int IndexOfRowId(int rowId)
    {
        var sortedRowIds = GetSortedRowIds();
        return sortedRowIds.IndexOf(rowId);
    }
}