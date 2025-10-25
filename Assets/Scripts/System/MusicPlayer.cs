using UnityEngine;
using System.Collections;

public class MusicPlayer : MonoBehaviour
{
    [Header("音乐设置")]
    public AudioClip musicClip;
    public AudioSource audioSource;

    [Header("音量控制")]
    [Range(0f, 1f)]
    public float volume = 1.0f;
    [Range(0f, 1f)]
    public float initialVolume = 1.0f; // 初始音量

    [Header("引用")]
    public CursorController cursor;

    private bool hasStarted = false;
    private float startDelay = 0.1f;

    void Start()
    {
        // 确保有AudioSource组件
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // 设置AudioSource属性
        audioSource.clip = musicClip;
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // 设置初始音量
        SetVolume(initialVolume);
    }

    void Update()
    {
        if (!hasStarted && cursor != null && cursor.isActive)
        {
            StartCoroutine(StartMusicWithDelay());
        }

        // 实时更新音量（如果音量被外部修改）
        if (audioSource.volume != volume)
        {
            audioSource.volume = volume;
        }
    }

    IEnumerator StartMusicWithDelay()
    {
        hasStarted = true;
        yield return new WaitForSeconds(startDelay);

        if (musicClip != null)
        {
            audioSource.Play();
            Debug.Log($"开始播放音乐: {musicClip.name}, 长度: {musicClip.length:F2}秒, 音量: {volume}");
        }
        else
        {
            Debug.LogWarning("没有设置音乐文件！");
        }
    }

    // ========== 音量控制方法 ==========

    /// <summary>
    /// 设置音量
    /// </summary>
    /// <param name="newVolume">音量值 (0-1)</param>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
        Debug.Log($"音乐音量设置为: {volume}");
    }

    /// <summary>
    /// 渐变音量
    /// </summary>
    /// <param name="targetVolume">目标音量</param>
    /// <param name="duration">渐变时间</param>
    public void FadeVolume(float targetVolume, float duration)
    {
        StartCoroutine(FadeVolumeCoroutine(targetVolume, duration));
    }

    private IEnumerator FadeVolumeCoroutine(float targetVolume, float duration)
    {
        float startVolume = volume;
        float startTime = Time.time;

        while (Time.time - startTime < duration)
        {
            float progress = (Time.time - startTime) / duration;
            SetVolume(Mathf.Lerp(startVolume, targetVolume, progress));
            yield return null;
        }

        SetVolume(targetVolume);
    }

    /// <summary>
    /// 静音
    /// </summary>
    public void Mute()
    {
        audioSource.mute = true;
        Debug.Log("音乐已静音");
    }

    /// <summary>
    /// 取消静音
    /// </summary>
    public void Unmute()
    {
        audioSource.mute = false;
        Debug.Log("音乐取消静音");
    }

    /// <summary>
    /// 切换静音状态
    /// </summary>
    public void ToggleMute()
    {
        audioSource.mute = !audioSource.mute;
        Debug.Log($"音乐静音状态: {audioSource.mute}");
    }

    /// <summary>
    /// 获取当前音量
    /// </summary>
    public float GetVolume()
    {
        return volume;
    }

    /// <summary>
    /// 增加音量
    /// </summary>
    /// <param name="increment">增量</param>
    public void IncreaseVolume(float increment = 0.1f)
    {
        SetVolume(volume + increment);
    }

    /// <summary>
    /// 减少音量
    /// </summary>
    /// <param name="decrement">减量</param>
    public void DecreaseVolume(float decrement = 0.1f)
    {
        SetVolume(volume - decrement);
    }

    // ========== 播放控制方法 ==========

    public void PlayMusic()
    {
        if (musicClip != null && !audioSource.isPlaying)
        {
            audioSource.Play();
            hasStarted = true;
        }
    }

    public void StopMusic()
    {
        audioSource.Stop();
        hasStarted = false;
    }

    public void PauseMusic()
    {
        audioSource.Pause();
    }

    public void ResumeMusic()
    {
        audioSource.UnPause();
    }

    // 获取音乐播放进度（0-1）
    public float GetPlaybackProgress()
    {
        if (musicClip != null && audioSource.isPlaying)
        {
            return audioSource.time / musicClip.length;
        }
        return 0f;
    }

    // 检查音乐是否正在播放
    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }
}