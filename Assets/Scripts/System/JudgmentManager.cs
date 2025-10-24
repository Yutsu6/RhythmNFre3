using System.Collections.Generic;
using UnityEngine;

public class JudgmentManager : MonoBehaviour
{
    [Header("引用")]
    public CursorController cursor;
    public ChartParser chartParser;

    public List<NoteData> activeJudgmentQueue = new List<NoteData>();
    public List<NoteData> allNotes;

    public const float JUDGMENT_WINDOW_ENTER = 0.16f;
    public const float JUDGMENT_WINDOW_EXIT = 0.10f;
    public const float PATCHED_THRESHOLD = 0.06f;
    public const float FIXED_THRESHOLD = 0.03f;

    public int combo = 0;
    public int errors = 0;
    public int totalNotesJudged = 0;

    private bool inputProcessedThisFrame = false;
    private float emptyPressSafetyWindow = 0.2f;

    private KeyCode[] fixKeys = new KeyCode[] {
        KeyCode.S, KeyCode.D, KeyCode.F,
        KeyCode.J, KeyCode.K, KeyCode.L
    };

    private List<KeyCode> currentlyPressedKeys = new List<KeyCode>();
    private Dictionary<KeyCode, float> lastErrorTimeByKey = new Dictionary<KeyCode, float>();

    void Start()
    {
        allNotes = chartParser.notes;
    }

    void Update()
    {
        if (!cursor.isActive) return;

        inputProcessedThisFrame = false;
        UpdateKeyStates();
        UpdateJudgmentQueue();
        ProcessJudgmentQueue();

        if (!inputProcessedThisFrame)
            CheckEmptyPress();
    }

    void UpdateKeyStates()
    {
        currentlyPressedKeys.Clear();
        foreach (KeyCode key in fixKeys)
        {
            if (Input.GetKey(key))
                currentlyPressedKeys.Add(key);
        }
    }

    void UpdateJudgmentQueue()
    {
        AddNotesToQueue();
        RemoveMissedNotes();
    }

    void AddNotesToQueue()
    {
        foreach (var note in allNotes)
        {
            if (note.isJudged || activeJudgmentQueue.Contains(note) || note.type == "track")
                continue;

            float timeDiff = note.triggerTime - cursor.cursorTime;
            if (Mathf.Abs(timeDiff) <= JUDGMENT_WINDOW_ENTER)
                activeJudgmentQueue.Add(note);
        }
    }

    void RemoveMissedNotes()
    {
        int loopCount = 0;
        for (int i = activeJudgmentQueue.Count - 1; i >= 0; i--)
        {
            loopCount++;
            if (loopCount > 1000) // 安全限制
            {
                Debug.LogError("RemoveMissedNotes 可能进入死循环！");
                break;
            }
            var note = activeJudgmentQueue[i];

            if (note.type == "hold")
            {
                if (note.isJudged)
                    activeJudgmentQueue.RemoveAt(i);
                continue;
            }

            float timeDiff = note.triggerTime - cursor.cursorTime;
            if (timeDiff < -JUDGMENT_WINDOW_EXIT)
            {
                activeJudgmentQueue.RemoveAt(i);
                JudgeNote(note, timeDiff, "Too Late");
            }
        }
    }

    void ProcessJudgmentQueue()
    {
        for (int i = activeJudgmentQueue.Count - 1; i >= 0; i--)
        {
            var note = activeJudgmentQueue[i];
            if (note.type == "hold") continue;

            float timeDiff = note.triggerTime - cursor.cursorTime;
            if (Mathf.Abs(timeDiff) <= JUDGMENT_WINDOW_EXIT && CheckInput())
            {
                activeJudgmentQueue.RemoveAt(i);
                JudgeNote(note, timeDiff, GetJudgmentResult(timeDiff));
                inputProcessedThisFrame = true;
            }
        }
    }

    void CheckEmptyPress()
    {
        if (IsHoldActiveInJudgmentQueue) return;

        foreach (KeyCode key in fixKeys)
        {
            if (Input.GetKeyDown(key) && !inputProcessedThisFrame)
            {
                if (IsInSafetyPeriod(key)) continue;
                if (!HasNoteInCoreWindow() && CurrentRowHasJudgableNotes())
                    RegisterEmptyPress(key);
            }
        }
    }

    bool IsInSafetyPeriod(KeyCode key)
    {
        if (lastErrorTimeByKey.ContainsKey(key) &&
            (cursor.cursorTime - lastErrorTimeByKey[key]) < emptyPressSafetyWindow)
            return true;
        return false;
    }

    bool HasNoteInCoreWindow()
    {
        foreach (var note in activeJudgmentQueue)
        {
            if (note.type == "track") continue;

            float timeDiff = note.triggerTime - cursor.cursorTime;
            if (Mathf.Abs(timeDiff) <= JUDGMENT_WINDOW_EXIT)
                return true;
        }
        return false;
    }

    void RegisterEmptyPress(KeyCode key)
    {
        errors++;
        combo = 0;
        lastErrorTimeByKey[key] = cursor.cursorTime;
    }

    void JudgeNote(NoteData note, float timeDiff, string judgment)
    {
        note.isJudged = true;
        note.judgmentResult = judgment;

        if (judgment == "Too Fast" || judgment == "Too Late")
        {
            combo = 0;
            errors++;
            RecordErrorTime();
        }
        else
        {
            combo++;
        }

        totalNotesJudged++;


    }

    void RecordErrorTime()
    {
        foreach (KeyCode key in fixKeys)
            lastErrorTimeByKey[key] = cursor.cursorTime;
    }

    public string GetJudgmentResult(float timeDiff)
    {
        float absTime = Mathf.Abs(timeDiff);

        if (absTime <= FIXED_THRESHOLD)
            return "Critical Fixed";
        if (absTime <= PATCHED_THRESHOLD)
            return timeDiff > 0 ? "Fast Fixed" : "Late Fixed";
        if (absTime <= JUDGMENT_WINDOW_EXIT)
            return timeDiff > 0 ? "Fast Patched" : "Late Patched";
        if (timeDiff > JUDGMENT_WINDOW_EXIT)
            return "Too Fast";

        return "Too Late";
    }

    bool CheckInput()
    {
        foreach (KeyCode key in fixKeys)
        {
            if (Input.GetKeyDown(key))
                return true;
        }
        return false;
    }

    bool CurrentRowHasJudgableNotes()
    {
        int currentRow = cursor.GetCurrentRowId();
        foreach (var note in allNotes)
        {
            if (note.rowId == currentRow && !note.isJudged && note.type != "track")
                return true;
        }
        return false;
    }

    public bool IsHoldActiveInJudgmentQueue
    {
        get
        {
            foreach (var note in activeJudgmentQueue)
            {
                if (note.type == "hold" && !note.isJudged)
                    return true;
            }
            return false;
        }
    }

    public void MarkInputProcessed()
    {
        inputProcessedThisFrame = true;
    }

    public KeyCode GetPressedFixKey()
    {
        foreach (KeyCode key in fixKeys)
        {
            if (Input.GetKeyDown(key))
                return key;
        }
        return KeyCode.None;
    }

    public bool IsKeyPressed(KeyCode key)
    {
        return Input.GetKey(key);
    }

    public bool IsKeyReleased(KeyCode key)
    {
        return Input.GetKeyUp(key);
    }
}