using System.Collections.Generic;
using UnityEngine;

// �ṹ���Ż���
public abstract class StructureSymbol
{
    public string symbolType { get; protected set; }
    public string rawData { get; protected set; }

    public abstract void Parse(string symbolData, NoteData noteData);
}

// ѭ������ʵ��
public class LoopSymbol : StructureSymbol
{
    public int[] loopCodes;

    public LoopSymbol()
    {
        symbolType = "loop";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;

        int startBrace = symbolData.IndexOf('{');
        int endBrace = symbolData.IndexOf('}');

        if (startBrace >= 0 && endBrace > startBrace)
        {
            string innerContent = symbolData.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();
            string[] codeStrings = innerContent.Split(',');
            List<int> codes = new List<int>();

            foreach (string codeStr in codeStrings)
            {
                string trimmedCode = codeStr.Trim();
                if (int.TryParse(trimmedCode, out int code))
                {
                    codes.Add(code);
                }
                else
                {
                    Debug.LogWarning($"�޷�����ѭ����: '{trimmedCode}'��ʹ��Ĭ��ֵ0");
                    codes.Add(0);
                }
            }

            loopCodes = codes.ToArray();
            Debug.Log($"����ѭ������: {string.Join(",", loopCodes)}");
        }
        else
        {
            Debug.LogError($"ѭ�����Ÿ�ʽ����: {symbolData}");
            loopCodes = new int[0];
        }
    }
}
// �ṹ���Ź���

// �˳�����ʵ��
public class BreakSymbol : StructureSymbol
{
    public BreakSymbol()
    {
        symbolType = "break";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"�����˳�����");
    }
}

// ��������ʵ��
public class ContinueSymbol : StructureSymbol
{
    public ContinueSymbol()
    {
        symbolType = "continue";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"������������");
    }
}

// ��ֹ����ʵ��
public class ReturnSymbol : StructureSymbol
{
    public ReturnSymbol()
    {
        symbolType = "return";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"������ֹ����");
    }
}

// ע�ͷ���ʵ��
public class CommentSymbol : StructureSymbol
{
    public CommentSymbol()
    {
        symbolType = "comment";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;
        Debug.Log($"����ע�ͷ���");
        // ע�ͷ���ֻ��һ����ʶ������Ҫ������������
    }
}

// ���ٷ���ʵ��
public class SpeedSymbol : StructureSymbol
{
    public string speedType; // "F" �� "L"

    public SpeedSymbol()
    {
        symbolType = "speed";
    }

    public override void Parse(string symbolData, NoteData noteData)
    {
        rawData = symbolData;

        if (symbolData == "F" || symbolData == "L")
        {
            speedType = symbolData;
            Debug.Log($"�������ٷ���: {speedType}");
        }
        else
        {
            Debug.LogWarning($"��Ч�ı��ٷ���: '{symbolData}'");
            speedType = "0";
        }
    }
}


public class IfSymbol : StructureSymbol
{
    public IfSymbol()
    {
        symbolType = "if";
    }
    public int[] conditionCodes;

    public override void Parse(string symbolData, NoteData noteData)
    {
        // ����if{1,0,1}��ʽ
        int startBrace = symbolData.IndexOf('{');
        int endBrace = symbolData.IndexOf('}');

        if (startBrace >= 0 && endBrace > startBrace)
        {
            string innerContent = symbolData.Substring(startBrace + 1, endBrace - startBrace - 1).Trim();
            string[] codeStrings = innerContent.Split(',');
            List<int> codes = new List<int>();

            foreach (string codeStr in codeStrings)
            {
                string trimmedCode = codeStr.Trim();
                if (int.TryParse(trimmedCode, out int code) && (code == 0 || code == 1))
                {
                    codes.Add(code);
                }
                else
                {
                    Debug.LogWarning($"��Ч���ж���: '{trimmedCode}'��ʹ��Ĭ��ֵ0");
                    codes.Add(0);
                }
            }

            conditionCodes = codes.ToArray();
        }
        else
        {
            Debug.LogError($"�жϷ��Ÿ�ʽ����: {symbolData}");
            conditionCodes = new int[0];
        }
    }
}

public static class StructureSymbolFactory
{
    private static Dictionary<string, System.Func<StructureSymbol>> symbolConstructors =
        new Dictionary<string, System.Func<StructureSymbol>>()
    {
        { "loop", () => new LoopSymbol() },
        { "break", () => new BreakSymbol() },
        { "continue", () => new ContinueSymbol() },
        { "return", () => new ReturnSymbol() },
        { "//", () => new CommentSymbol() },
        { "if", () => new IfSymbol() },
        { "F", () => new SpeedSymbol() },  // ����
        { "L", () => new SpeedSymbol() }   // ����
    };

    public static StructureSymbol CreateSymbol(string symbolType)
    {
        if (symbolConstructors.ContainsKey(symbolType))
        {
            return symbolConstructors[symbolType]();
        }
        return null;
    }

    public static bool IsStructureSymbol(string content)
    {
        return content.Contains("loop{") ||
               content.Contains("break") ||
               content.Contains("continue") ||
               content.Contains("return") ||
               content.Contains("//") ||
               content.Contains("if{") ||
               content.Contains("F") ||
               content.Contains("L");
    }
}