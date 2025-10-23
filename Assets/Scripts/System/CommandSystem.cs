using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;

public class CommandManager : MonoBehaviour
{
    [Header("����")]
    public CursorController cursor;
    public ChartParser parser;

    [Header("��������")]
    public float baseBPM = 120;
    public float baseSpeed = 4.0f;

    private Dictionary<string, System.Action<string>> commandHandlers;

    void Start()
    {
        InitializeCommandHandlers();

        // ��ȡ����BPM���ٶ�
        if (parser != null)
        {
            baseBPM = parser.GetBPM();
            baseSpeed = cursor.speed;
        }
    }

    void InitializeCommandHandlers()
    {
        commandHandlers = new Dictionary<string, System.Action<string>>()
        {
            { "speedChange", HandleSpeedChange },
            { "bpmChange", HandleBPMChange }
            // ���Լ��������������
        };
    }

    public void ExecuteCommand(NoteData note)
    {
        if (!note.hasCommand || note.isCommandExecuted) return;

        // ����Ƿ�Ӧ��ִ�У�����ִ����͵�ǰ��ȡ������
        if (note.ShouldExecuteCommand())
        {
            string commandName = note.commandName;
            string parameters = note.commandParams;

            if (commandHandlers.ContainsKey(commandName))
            {
                Debug.Log($"ִ������: {commandName}{{{parameters}}}, ��ȡ����: {note.currentReadCount}");
                commandHandlers[commandName](parameters);
            }
            else
            {
                Debug.LogWarning($"δ֪����: {commandName}");
            }
        }

        // ִ�к��������Ϊ��ִ�У���ֹͬһ�����ظ�ִ�У�
        note.isCommandExecuted = true;
    }

    public void OnLeaveRow(int rowId)
    {
        foreach (var note in parser.notes)
        {
            if (note.rowId == rowId && note.hasCommand && note.isCommandExecuted)
            {
                // ����ִ��״̬
                note.isCommandExecuted = false;
                // ���Ӷ�ȡ����
                note.IncrementReadCount();
                Debug.Log($"�뿪��{rowId}������{note.commandName}��ȡ�������ӵ�: {note.currentReadCount}");
            }
        }
    }



    void HandleSpeedChange(string parameters)
    {
        if (string.IsNullOrEmpty(parameters)) return;

        // �ж��Ǿ���ֵ�������ֵ
        if (parameters.StartsWith("+") || parameters.StartsWith("-"))
        {
            // ��Ե���
            if (float.TryParse(parameters, out float adjustment))
            {
                cursor.speed += adjustment;
                Debug.Log($"�ٶȵ���: {adjustment}, ���ٶ�: {cursor.speed}");
            }
        }
        else
        {
            // ����ֵ
            if (float.TryParse(parameters, out float newSpeed))
            {
                cursor.speed = newSpeed;
                Debug.Log($"�ٶ�����Ϊ: {newSpeed}");
            }
        }
    }

    void HandleBPMChange(string parameters)
    {
        if (string.IsNullOrEmpty(parameters)) return;

        if (float.TryParse(parameters, out float newBPM))
        {
            baseBPM = newBPM;
            Debug.Log($"BPM����Ϊ: {newBPM}");

        }
    }

    // ������������״̬�����¿�ʼ��Ϸʱ���ã�
    public void ResetAllCommands()
    {
        foreach (var note in parser.notes)
        {
            note.currentReadCount = 0;
            note.isCommandExecuted = false;
        }
    }
}