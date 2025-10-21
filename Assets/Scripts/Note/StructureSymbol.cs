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
public static class StructureSymbolFactory
{
    private static Dictionary<string, System.Func<StructureSymbol>> symbolConstructors =
        new Dictionary<string, System.Func<StructureSymbol>>()
    {
        { "loop", () => new LoopSymbol() },
        // δ����������������������ţ�
        // { "if", () => new IfSymbol() },
        // { "switch", () => new SwitchSymbol() }
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
        // ����Ƿ�����֪�Ľṹ���ſ�ͷ
        foreach (string symbolType in symbolConstructors.Keys)
        {
            if (content.Contains(symbolType + "{"))
            {
                Debug.Log($"��⵽�ṹ��������: {symbolType}");
                return true;
            }
        }
        Debug.Log($"δ��⵽�ṹ����: {content}");
        return false;
    }
}