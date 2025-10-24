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
    // 在类开头添加字段
    private string targetDifficulty = ""; // 可以设置为 "1", "2" 等特定难度

    public List<NoteData> notes = new List<NoteData>();

    public TextAsset chartTextFile;

    public Vector2 gridOrigin = Vector2.zero; //坐标原点
    public float cellSize = 1.0f;           //单位大小
    public float rowHeight = 1.5f;          //垂直间距

    public float visualScale = 0.8f;        // 新增：视觉缩放系数


    // 新增：Meta数据
    [System.Serializable]
    public class MetaData
    {
        public string title = "";
        public string artist = "";
        public float bpm = 120f;           // 默认BPM
        public float offset = 0f;          // 默认偏移
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
        metaData = new MetaData(); // 重置Meta数据

        if (chartTextFile == null)
        {
            Debug.LogError("没有谱面文件喵！");
            return;
        }

        string fileContent = chartTextFile.text;
        string[] lines = fileContent.Split('\n');

        int currentRowId = -1;  //当前正在处理的行ID
        bool inMainSection = false;
        bool inMetaSection = false;
        int currentIndent = 0; // 当前行的缩进量

        for (int i = 0; i < lines.Length; i++)
        {
            string originalLine = lines[i];
            string line = originalLine.Trim();
            Debug.Log($"第{i}行: '{line}'");

            // 跳过空行
            if (string.IsNullOrEmpty(line))
            {
                Debug.Log($"  跳过空行");
                continue;
            }

            // 处理注释：区分行注释和结构符号注释
            line = ProcessComments(line, originalLine);

            // 如果整行都是注释，跳过该行
            if (string.IsNullOrEmpty(line))
            {
                Debug.Log($"  跳过注释行");
                continue;
            }

            // 修改区域识别部分
            if (line.StartsWith("#"))
            {
                // 检查是否是Main区域
                if (line.StartsWith("# Main"))
                {
                    // 将难度信息交给ParseMetaData处理
                    string metaLine = line.Substring(1).Trim(); // 去掉 "#" 符号
                    ParseMetaData(metaLine);

                    // 检查是否应该进入这个难度
                    if (line.Contains("_"))
                    {
                        string[] parts = line.Split('_');
                        if (parts.Length > 1)
                        {
                            string difficulty = parts[1].Trim();
                            if (!string.IsNullOrEmpty(targetDifficulty) && difficulty != targetDifficulty)
                            {
                                inMainSection = false;
                                Debug.Log($"跳过难度{difficulty}，目标难度是{targetDifficulty}");
                                continue;
                            }
                        }
                    }

                    inMainSection = true;
                    inMetaSection = false;
                    Debug.Log("进入主程序区域");
                    continue;
                }
                else if (line.StartsWith("# Meta"))
                {
                    inMainSection = false;
                    inMetaSection = true;
                    Debug.Log("进入元信息区域");
                    continue;
                }
                else if (line.StartsWith("# Func"))
                {
                    inMainSection = false;
                    inMetaSection = false;
                    Debug.Log("进入函数区域");
                    continue;
                }
                else
                {
                    inMainSection = false;
                    inMetaSection = false;
                    Debug.Log($"未知区域，跳过: {line}");
                    continue;
                }
            }

            // 处理Meta区域数据
            if (inMetaSection)
            {
                ParseMetaData(line);
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

                    // 检查是否是命令（没有中括号）
                    if (IsCommandFormat(content))
                    {
                        NoteData newNote = new NoteData();
                        newNote.rowId = currentRowId;
                        newNote.position = notePosition;
                        newNote.indentLevel = currentIndent;

                        // 解析命令内容
                        ParseCommand(content, newNote);
                        notes.Add(newNote);
                    }
                    // 检查是否是单元语句（有中括号）
                    else if (content.StartsWith("[") && content.EndsWith("]"))
                    {
                        string innerContent = content.Substring(1, content.Length - 2).Trim();

                        NoteData newNote = new NoteData();
                        newNote.rowId = currentRowId;
                        newNote.position = notePosition;
                        newNote.indentLevel = currentIndent;

                        //单元类型和长度
                        ParseNoteContent(innerContent, newNote);
                        notes.Add(newNote);
                    }
                    else
                    {
                        Debug.LogWarning($"无法识别的单元格式: {content}");
                    }
                }
            }
        }
    }

    // 修改：解析Meta数据，处理带空格和难度信息的情况
    void ParseMetaData(string line)
    {
        int colonIndex = line.IndexOf(':');
        if (colonIndex > 0)
        {
            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();

            // 清理键名：移除所有空格和制表符
            string cleanKey = System.Text.RegularExpressions.Regex.Replace(key, @"\s+", "");

            switch (cleanKey.ToLower())
            {
                case "title":
                    metaData.title = value;
                    Debug.Log($"设置标题: {value}");
                    break;

                case "artist":
                    metaData.artist = value;
                    Debug.Log($"设置艺术家: {value}");
                    break;

                case "bpm":
                    if (float.TryParse(value, out float bpm))
                    {
                        metaData.bpm = bpm;
                        Debug.Log($"设置BPM: {bpm}");
                    }
                    else
                    {
                        Debug.LogError($"无法解析BPM: {value}");
                    }
                    break;

                case "offset":
                    // 处理带+号的偏移值
                    string cleanValue = value.Replace("+", "").Trim();
                    if (float.TryParse(cleanValue, out float offset))
                    {
                        metaData.offset = offset;
                        Debug.Log($"设置偏移: {offset}");
                    }
                    else
                    {
                        Debug.LogError($"无法解析偏移: {value}");
                    }
                    break;

                case "main":
                    // 处理难度信息，如 "Main_1"
                    if (key.Contains("_"))
                    {
                        string[] parts = key.Split('_');
                        if (parts.Length > 1)
                        {
                            string difficulty = parts[1].Trim();
                            Debug.Log($"检测到难度: {difficulty}");

                            // 如果设置了目标难度，更新当前处理状态
                            if (string.IsNullOrEmpty(targetDifficulty) || difficulty == targetDifficulty)
                            {
                                // 这里可以设置一些难度相关的状态
                                Debug.Log($"处理难度: {difficulty}");
                            }
                            else
                            {
                                Debug.Log($"跳过难度: {difficulty}");
                            }
                        }
                    }
                    break;

                default:
                    // 处理 level_x 和 designer_x 格式
                    if (cleanKey.StartsWith("level_"))
                    {
                        string levelNumber = cleanKey.Substring(6); // 去掉 "level_"
                        metaData.levelDesigners[levelNumber] = value;
                        Debug.Log($"设置难度{levelNumber}设计师: {value}");
                    }
                    else if (cleanKey.StartsWith("designer_"))
                    {
                        string levelNumber = cleanKey.Substring(9); // 去掉 "designer_"
                        metaData.levelDesigners[levelNumber] = value;
                        Debug.Log($"设置难度{levelNumber}设计师: {value}");
                    }
                    else
                    {
                        Debug.LogWarning($"未知的Meta键: {cleanKey}");
                    }
                    break;
            }
        }
        else
        {
            // 处理没有冒号的情况，比如 "# Main_1"
            string cleanLine = line.Trim();
            if (cleanLine.StartsWith("Main_"))
            {
                string difficulty = cleanLine.Substring(5); // 去掉 "Main_"
                Debug.Log($"检测到难度区域: {difficulty}");

                // 如果设置了目标难度，更新当前处理状态
                if (string.IsNullOrEmpty(targetDifficulty) || difficulty == targetDifficulty)
                {
                    Debug.Log($"处理难度: {difficulty}");
                }
                else
                {
                    Debug.Log($"跳过难度: {difficulty}");
                }
            }
        }
    }

    //获取难度
    public int Difficulty()
    {
        return metaData.difficulty;
    }

    // 新增：获取BPM
    public float GetBPM()
    {
        return metaData.bpm;
    }

    // 新增：获取偏移
    public float GetOffset()
    {
        return metaData.offset;
    }

    // 新增：获取标题
    public string GetTitle()
    {
        return metaData.title;
    }

    // 新增：获取艺术家
    public string GetArtist()
    {
        return metaData.artist;
    }

    // 新增：获取指定难度的设计师
    public string GetDesigner(string level)
    {
        if (metaData.levelDesigners.ContainsKey(level))
            return metaData.levelDesigners[level];
        return "";
    }



    /// <summary>
    /// 处理注释：区分行注释和结构符号注释
    /// </summary>
    string ProcessComments(string line, string originalLine)
    {
        Debug.Log($"开始处理注释: '{line}'");

        // 情况1：整行注释（以//开头）- 完全跳过
        if (line.StartsWith("//"))
        {
            Debug.Log($"检测到整行注释，跳过: {line}");
            return "";
        }

        // 查找所有的//位置
        List<int> commentPositions = FindAllCommentPositions(line);

        if (commentPositions.Count == 0)
        {
            Debug.Log($"没有检测到注释");
            return line;
        }

        // 检查每个//的位置
        foreach (int commentPos in commentPositions)
        {
            bool isInBrackets = IsInsideBrackets(originalLine, commentPos);

            if (isInBrackets)
            {
                Debug.Log($"位置{commentPos}的//在方括号内，保留为结构符号");
            }
            else
            {
                Debug.Log($"位置{commentPos}的//是行内注释，移除后面内容");
                return line.Substring(0, commentPos).Trim();
            }
        }

        // 所有//都在方括号内，保留整行
        Debug.Log($"所有//都在方括号内，保留整行");
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
            index = found + 2; // 跳过"//"
        }

        Debug.Log($"找到{positions.Count}个//位置: {string.Join(", ", positions)}");
        return positions;
    }

    /// <summary>
    /// 检查指定文本是否在方括号内
    /// </summary>
    bool IsInsideBrackets(string line, int position)
    {
        if (position < 0 || position >= line.Length) return false;

        // 计算position之前未匹配的[数量
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
        Debug.Log($"位置{position}: 未匹配[数量={unmatchedBrackets}, 在方括号内={isInside}");

        return isInside;
    }

    public Vector2 GridWorld(int rowId, float position)
    {
        // 修改这里：应用视觉缩放
        float worldX = gridOrigin.x + position * cellSize * visualScale;
        float worldY = gridOrigin.y + rowId * rowHeight * visualScale;

        return new Vector2(worldX, worldY);
    }

    void ParseNoteContent(string content, NoteData noteData)
    {
        if (string.IsNullOrEmpty(content))
        {
            // 空内容：长度为1的音轨
            noteData.type = "track";
            noteData.length = 1f;
            return;
        }

        // 首先检查是否是命令格式
        if (IsCommandFormat(content))
        {
            ParseCommand(content, noteData);
            return;
        }


        Debug.Log($"解析音符内容: '{content}'");

        // 设置默认值
        noteData.type = "track";
        noteData.length = 1f;

        // 按分号分割内容
        string[] parts = SplitNoteContent(content);

        Debug.Log($"分割为 {parts.Length} 部分: {string.Join(" | ", parts)}");

        // 按顺序解析每个部分
        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            string trimmedPart = part.Trim();

            // 1. 检查是否是长度（数字）
            if (IsLength(trimmedPart))
            {
                if (float.TryParse(trimmedPart, out float length) && length > 0)
                {
                    noteData.length = length;
                    Debug.Log($"设置长度: {length}");
                    continue;
                }
            }

            // 2. 检查是否是音符类型（0, t, h, b）
            if (IsNoteType(trimmedPart))
            {
                noteData.type = MapTypeToFullName(trimmedPart);
                Debug.Log($"设置音符类型: {noteData.type}");
                continue;
            }

            // 3. 检查是否是Multi音符（{...}）
            if (IsMultiNoteDefinition(trimmedPart))
            {
                ParseMultiNoteDefinition(trimmedPart, noteData);
                continue;
            }

            // 4. 检查是否是结构符号
            if (IsStructureSymbol(trimmedPart))
            {
                ParseStructureSymbols(trimmedPart, noteData);
                continue;
            }

            // 5. 检查是否是变速符号（F, L）
            if (IsSpeedSymbol(trimmedPart))
            {
                ParseSpeedSymbol(trimmedPart, noteData);
                continue;
            }

            // 6. 检查是否是延时长度（_数字）
            if (IsDelayLength(trimmedPart))
            {
                ParseDelayLength(trimmedPart, noteData);
                continue;
            }

            Debug.LogWarning($"无法识别的部分: '{trimmedPart}'");
        }

        Debug.Log($"音符解析完成 - 类型: {noteData.type}, 长度: {noteData.length}, 符号: {noteData.GetSymbolsDebugInfo()}");
    }

    // 修改：更精确的命令格式检测
    bool IsCommandFormat(string content)
    {
        // 命令格式特征：
        // 1. 包含花括号 {}
        // 2. 花括号前的内容要么是命令名，要么是(执行码)命令名
        // 3. 不能是结构符号格式（如loop{4}）

        if (!content.Contains("{") || !content.Contains("}"))
            return false;

        // 检查是否是已知的结构符号格式
        if (IsStructureSymbol(content))
        {
            Debug.Log($"检测到结构符号，不是命令: {content}");
            return false;
        }

        int braceStart = content.IndexOf('{');
        int braceEnd = content.LastIndexOf('}');

        if (braceStart < 0 || braceEnd < braceStart)
            return false;

        string beforeBraces = content.Substring(0, braceStart).Trim();
        Debug.Log($"花括号前的内容: '{beforeBraces}'");

        // 检查是否是有效的命令格式
        // 情况1: (执行码)命令名{参数}
        if (beforeBraces.Contains("(") && beforeBraces.Contains(")"))
        {
            int parenStart = beforeBraces.IndexOf('(');
            int parenEnd = beforeBraces.IndexOf(')');

            if (parenStart >= 0 && parenEnd > parenStart)
            {
                // 提取执行码部分
                string codesStr = beforeBraces.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                Debug.Log($"提取执行码: '{codesStr}'");

                // 提取命令名部分（括号后的内容）
                string commandName = beforeBraces.Substring(parenEnd + 1).Trim();
                Debug.Log($"提取命令名: '{commandName}'");

                if (IsValidExecutionCodes(codesStr) && IsValidCommandName(commandName))
                {
                    Debug.Log($"检测到带执行码的命令格式: {content}");
                    return true;
                }
                else
                {
                    Debug.Log($"执行码或命令名无效 - 执行码: {IsValidExecutionCodes(codesStr)}, 命令名: {IsValidCommandName(commandName)}");
                }
            }
        }
        // 情况2: 命令名{参数}（无执行码）
        else if (IsValidCommandName(beforeBraces))
        {
            Debug.Log($"检测到无执行码的命令格式: {content}");
            return true;
        }
        else
        {
            Debug.Log($"不是有效的命令名或执行码格式: {beforeBraces}");
        }

        Debug.Log($"不是有效的命令格式: {content}");
        return false;
    }

    // 新增：检查是否是有效的执行码格式
    bool IsValidExecutionCodes(string codesStr)
    {
        if (string.IsNullOrEmpty(codesStr))
        {
            Debug.Log("执行码为空");
            return false;
        }

        string[] codeStrs = codesStr.Split(',');
        Debug.Log($"解析执行码: {codesStr} -> {codeStrs.Length}个码");

        foreach (string codeStr in codeStrs)
        {
            string trimmedCode = codeStr.Trim();
            if (!int.TryParse(trimmedCode, out int code))
            {
                Debug.Log($"无法解析执行码: '{trimmedCode}'");
                return false;
            }
            if (code != 0 && code != 1)
            {
                Debug.Log($"执行码值无效(必须是0或1): {code}");
                return false;
            }
        }

        Debug.Log($"执行码验证通过: {codesStr}");
        return true;
    }

    // 新增：检查是否是有效的命令名
    bool IsValidCommandName(string name)
    {
        // 已知的命令名列表
        string[] validCommands = { "speedChange", "bpmChange", "tagCreat", "tagDelete", "logCreate",
                              "colorChange", "loopColorChange", "caseColorChange" };

        bool isValid = validCommands.Contains(name);
        Debug.Log($"检查命令名 '{name}': {(isValid ? "有效" : "无效")}");

        return isValid;
    }


    // 修改：解析命令方法
    void ParseCommand(string content, NoteData noteData)
    {
        noteData.hasCommand = true;
        noteData.type = "command"; // 特殊类型，不生成可视音符

        Debug.Log($"=== 开始解析命令 ===");
        Debug.Log($"命令内容: {content}");

        // 分离执行码和命令
        int braceStart = content.IndexOf('{');
        int braceEnd = content.LastIndexOf('}');

        if (braceStart < 0 || braceEnd < braceStart)
        {
            Debug.LogError($"命令格式错误: {content}");
            return;
        }

        string beforeBraces = content.Substring(0, braceStart).Trim();
        string commandWithParams = content.Substring(braceStart, braceEnd - braceStart + 1);

        Debug.Log($"花括号前的内容: '{beforeBraces}'");

        // 解析执行码和命令名
        if (beforeBraces.Contains("(") && beforeBraces.Contains(")"))
        {
            int parenStart = beforeBraces.IndexOf('(');
            int parenEnd = beforeBraces.IndexOf(')');

            if (parenStart >= 0 && parenEnd > parenStart)
            {
                // 提取执行码部分
                string codesStr = beforeBraces.Substring(parenStart + 1, parenEnd - parenStart - 1).Trim();
                noteData.executionCodes = ParseExecutionCodes(codesStr);
                Debug.Log($"解析执行码: {string.Join(",", noteData.executionCodes)}");

                // 提取命令名部分（括号后的内容）
                string commandName = beforeBraces.Substring(parenEnd + 1).Trim();

                // 解析命令名和参数
                ParseCommandNameAndParams(commandName + commandWithParams, noteData);
            }
            else
            {
                Debug.LogError($"括号格式错误: {beforeBraces}");
                return;
            }
        }
        else
        {
            // 没有执行码，使用默认行为（空数组表示默认执行1次）
            noteData.executionCodes = new int[0];
            Debug.Log($"无执行码，使用默认行为");

            // 解析命令名和参数
            ParseCommandNameAndParams(commandWithParams, noteData);
        }

        Debug.Log($"命令解析完成: {noteData.commandName}{{{noteData.commandParams}}}, 执行码: [{string.Join(",", noteData.executionCodes)}]");
        Debug.Log($"=== 命令解析完成 ===");
    }

    // 新增：解析执行码
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
                Debug.LogWarning($"无法解析执行码: {codeStr}，使用默认值0");
                codes.Add(0);
            }
        }

        return codes.ToArray();
    }

    // 修改：解析命令名和参数方法
    void ParseCommandNameAndParams(string commandWithParams, NoteData noteData)
    {
        // 格式: 命令名{参数}
        int braceStart = commandWithParams.IndexOf('{');

        if (braceStart < 0)
        {
            Debug.LogError($"命令格式错误，缺少花括号: {commandWithParams}");
            return;
        }

        string commandName = commandWithParams.Substring(0, braceStart).Trim();
        string parameters = commandWithParams.Substring(braceStart + 1, commandWithParams.Length - braceStart - 2).Trim();

        noteData.commandName = commandName;
        noteData.commandParams = parameters;

        Debug.Log($"命令名: '{commandName}', 参数: '{parameters}'");
    }


    // 分割音符内容（考虑花括号内的逗号）
    string[] SplitNoteContent(string content)
    {
        List<string> parts = new List<string>();
        int currentIndex = 0;
        int braceDepth = 0;

        while (currentIndex < content.Length)
        {
            int nextSemicolon = content.IndexOf(';', currentIndex);

            // 如果没有分号了，添加剩余部分
            if (nextSemicolon < 0)
            {
                string remaining = content.Substring(currentIndex).Trim();
                if (!string.IsNullOrEmpty(remaining))
                    parts.Add(remaining);
                break;
            }

            // 检查分号是否在花括号内
            for (int i = currentIndex; i < nextSemicolon; i++)
            {
                if (content[i] == '{') braceDepth++;
                else if (content[i] == '}') braceDepth--;
            }

            if (braceDepth > 0)
            {
                // 分号在花括号内，跳过
                currentIndex = nextSemicolon + 1;
            }
            else
            {
                // 正常分号，分割
                string part = content.Substring(currentIndex, nextSemicolon - currentIndex).Trim();
                if (!string.IsNullOrEmpty(part))
                    parts.Add(part);
                currentIndex = nextSemicolon + 1;
            }
        }

        return parts.ToArray();
    }

    // 检查是否是长度
    bool IsLength(string part)
    {
        return float.TryParse(part, out _);
    }


    // 检查是否是Multi音符定义
    bool IsMultiNoteDefinition(string part)
    {
        return part.StartsWith("{") && part.EndsWith("}");
    }

    // 检查是否是结构符号
    bool IsStructureSymbol(string part)
    {
        return part.StartsWith("loop{") || part.StartsWith("if{") ||
               part == "break" || part == "continue" || part == "return" ||
               part.StartsWith("//");
    }

    // 检查是否是变速符号
    bool IsSpeedSymbol(string part)
    {
        return part == "F" || part == "L";
    }

    // 检查是否是延时长度
    bool IsDelayLength(string part)
    {
        return part.StartsWith("_") && part.Length > 1;
    }

    // 解析Multi音符定义
    void ParseMultiNoteDefinition(string multiDefinition, NoteData noteData)
    {
        // 创建MultiNoteData实例
        MultiNoteData multiNote = new MultiNoteData();

        // 复制基础属性
        multiNote.rowId = noteData.rowId;
        multiNote.position = noteData.position;
        multiNote.indentLevel = noteData.indentLevel;
        multiNote.isMultiNote = true;
        multiNote.length = noteData.length; // 使用已解析的长度

        // 解析层定义
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

        // 计算总共需要打击的次数
        multiNote.totalHitsRequired = multiNote.GetRemainingHits();

        // 替换原来的noteData
        notes.Remove(noteData);
        notes.Add(multiNote);

        Debug.Log($"Multi音符解析完成: {multiNote.layers.Count}层, 需要打击{multiNote.GetRemainingHits()}次");
    }

    // 解析变速符号
    void ParseSpeedSymbol(string trimmedPart, NoteData noteData)
    {
        if (trimmedPart == "F" || trimmedPart == "L")
        {
            // 创建变速符号并添加到结构符号列表
            SpeedSymbol speedSymbol = new SpeedSymbol();
            speedSymbol.Parse(trimmedPart, noteData);
            noteData.AddStructureSymbol(speedSymbol);

            Debug.Log($"解析变速符号: {trimmedPart}");
        }
        else
        {
            Debug.LogWarning($"无效的变速符号: '{trimmedPart}'");
        }
    }

    // 解析延时长度
    void ParseDelayLength(string delayPart, NoteData noteData)
    {
        string delayValue = delayPart.Substring(1); // 去掉下划线
        if (float.TryParse(delayValue, out float delayLength))
        {
            // 暂时只记录，不实现功能
            Debug.Log($"检测到延时长度: {delayLength} (暂不支持)");
        }
    }

    // 从值创建Multi层
    MultiNoteLayer CreateMultiLayerFromValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        string layerType;
        float layerLength = 1f; // 默认长度

        // 检查是否有长度信息（如 "4;h" 格式）
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

    // 专门用于Multi音符的类型映射
    string MapMultiNoteType(string value)
    {
        switch (value.Trim())
        {
            case "0": return "track";
            case "1":
            case "t": return "tap";
            case "h": return "hold";
            case "b": return "break";
            default: return "track"; // 默认值
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
            case "0": return "track";
            default: return shortType;
        }
    }

    // 修改解析结构符号的方法
    void ParseStructureSymbols(string symbolInfo, NoteData noteData)
    {
        Debug.Log($"=== 开始解析结构符号 ===");
        Debug.Log($"符号信息: '{symbolInfo}'");
        Debug.Log($"目标音符: 行{noteData.rowId} 位置{noteData.position}");

        StructureSymbol symbol = null;

        if (symbolInfo.StartsWith("loop{"))
        {
            Debug.Log("创建Loop符号");
            symbol = StructureSymbolFactory.CreateSymbol("loop");
        }
        else if (symbolInfo.StartsWith("if{"))
        {
            Debug.Log("创建If符号");
            symbol = StructureSymbolFactory.CreateSymbol("if");
        }
        else if (symbolInfo.StartsWith("break"))
        {
            Debug.Log("创建Break符号");
            symbol = StructureSymbolFactory.CreateSymbol("break");
        }
        else if (symbolInfo.StartsWith("continue"))
        {
            Debug.Log("创建Continue符号");
            symbol = StructureSymbolFactory.CreateSymbol("continue");
        }
        else if (symbolInfo.StartsWith("return"))
        {
            Debug.Log("创建Return符号");
            symbol = StructureSymbolFactory.CreateSymbol("return");
        }
        else if (symbolInfo.StartsWith("//"))
        {
            Debug.Log("创建Comment符号");
            symbol = StructureSymbolFactory.CreateSymbol("//");
        }

        if (symbol != null)
        {
            Debug.Log($"符号创建成功: {symbol.symbolType}");

            // 解析符号
            Debug.Log($"开始解析符号内容: {symbolInfo}");
            symbol.Parse(symbolInfo, noteData);
            Debug.Log($"符号解析完成");

            // 添加到音符
            Debug.Log($"添加符号到音符");
            noteData.AddStructureSymbol(symbol);
            Debug.Log($"符号添加完成，音符现在有{noteData.structureSymbols.Count}个符号");

            Debug.Log($"成功解析{symbol.symbolType}符号并添加到音符");
        }
        else
        {
            Debug.LogError($"符号创建失败: {symbolInfo}");
            Debug.LogWarning($"未知的结构符号类型: {symbolInfo}");
        }

        Debug.Log($"=== 结构符号解析完成 ===");
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

    public int IndexOfRowId(int rowId)
    {
        var sortedRowIds = GetSortedRowIds();
        return sortedRowIds.IndexOf(rowId);
    }
}