using UnityEngine;
using System.Collections;

public class MusicPlayer : MonoBehaviour
{
    [Header("��������")]
    public AudioClip musicClip;
    public AudioSource audioSource;

    [Header("��������")]
    [Range(0f, 1f)]
    public float volume = 1.0f;
    [Range(0f, 1f)]
    public float initialVolume = 1.0f; // ��ʼ����

    [Header("����")]
    public CursorController cursor;

    private bool hasStarted = false;
    private float startDelay = 0.1f;

    void Start()
    {
        // ȷ����AudioSource���
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // ����AudioSource����
        audioSource.clip = musicClip;
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        // ���ó�ʼ����
        SetVolume(initialVolume);
    }

    void Update()
    {
        if (!hasStarted && cursor != null && cursor.isActive)
        {
            StartCoroutine(StartMusicWithDelay());
        }

        // ʵʱ��������������������ⲿ�޸ģ�
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
            Debug.Log($"��ʼ��������: {musicClip.name}, ����: {musicClip.length:F2}��, ����: {volume}");
        }
        else
        {
            Debug.LogWarning("û�����������ļ���");
        }
    }

    // ========== �������Ʒ��� ==========

    /// <summary>
    /// ��������
    /// </summary>
    /// <param name="newVolume">����ֵ (0-1)</param>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
        Debug.Log($"������������Ϊ: {volume}");
    }

    /// <summary>
    /// ��������
    /// </summary>
    /// <param name="targetVolume">Ŀ������</param>
    /// <param name="duration">����ʱ��</param>
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
    /// ����
    /// </summary>
    public void Mute()
    {
        audioSource.mute = true;
        Debug.Log("�����Ѿ���");
    }

    /// <summary>
    /// ȡ������
    /// </summary>
    public void Unmute()
    {
        audioSource.mute = false;
        Debug.Log("����ȡ������");
    }

    /// <summary>
    /// �л�����״̬
    /// </summary>
    public void ToggleMute()
    {
        audioSource.mute = !audioSource.mute;
        Debug.Log($"���־���״̬: {audioSource.mute}");
    }

    /// <summary>
    /// ��ȡ��ǰ����
    /// </summary>
    public float GetVolume()
    {
        return volume;
    }

    /// <summary>
    /// ��������
    /// </summary>
    /// <param name="increment">����</param>
    public void IncreaseVolume(float increment = 0.1f)
    {
        SetVolume(volume + increment);
    }

    /// <summary>
    /// ��������
    /// </summary>
    /// <param name="decrement">����</param>
    public void DecreaseVolume(float decrement = 0.1f)
    {
        SetVolume(volume - decrement);
    }

    // ========== ���ſ��Ʒ��� ==========

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

    // ��ȡ���ֲ��Ž��ȣ�0-1��
    public float GetPlaybackProgress()
    {
        if (musicClip != null && audioSource.isPlaying)
        {
            return audioSource.time / musicClip.length;
        }
        return 0f;
    }

    // ��������Ƿ����ڲ���
    public bool IsPlaying()
    {
        return audioSource.isPlaying;
    }
}