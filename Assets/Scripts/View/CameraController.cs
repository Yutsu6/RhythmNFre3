using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("�������")]
    public Transform cursor;                   // �������
    public Vector3 initialCameraPosition = new Vector3(8f, -4f, -9f); // ��ʼλ��
    public float triggerYPosition = -6f;       // ���������Y����

    [Header("��������")]
    public float yOffset = 0f;                 // Y��ƫ��

    [Header("״̬")]
    public bool isFollowing = false;           // �Ƿ����ڸ���
    public bool debugMode = true;              // ����ģʽ

    private Vector3 targetPosition;
    private float initialX;                    // ��ʼXλ�ã��̶���

    void Start()
    {
        // ���������ʼλ��
        transform.position = initialCameraPosition;
        initialX = initialCameraPosition.x;

        if (cursor == null)
        {
            Debug.LogError("CameraController: û�����ù�����ã�");
        }

        if (debugMode)
        {
            Debug.Log($"�����ʼ��: λ��={transform.position}, ����Y={triggerYPosition}");
        }
    }

    void LateUpdate()
    {
        if (cursor == null) return;

        CheckFollowTrigger();
        UpdateCameraPosition();
    }

    void CheckFollowTrigger()
    {
        if (isFollowing) return;

        // ������Ƿ񵽴ﴥ��λ��
        if (cursor.position.y <= triggerYPosition)
        {
            isFollowing = true;
            if (debugMode)
            {
                Debug.Log($"��ʼ�������! ���Y={cursor.position.y}, ����Y={triggerYPosition}");
            }
        }
    }

    void UpdateCameraPosition()
    {
        if (!isFollowing)
        {
            // ����ǰ�����ֳ�ʼλ��
            transform.position = initialCameraPosition;
        }
        else
        {
            // ����ģʽ��������ת�����λ��
            float targetY = cursor.position.y + yOffset;
            transform.position = new Vector3(initialX, targetY, initialCameraPosition.z);
        }
    }

    // ��������
    public void StartFollowing()
    {
        isFollowing = true;
    }

    public void StopFollowing()
    {
        isFollowing = false;
    }

    public void ResetCamera()
    {
        isFollowing = false;
        transform.position = initialCameraPosition;
    }

    // ��Scene��ͼ����ʾ�������򣨵����ã�
    void OnDrawGizmos()
    {
        if (!debugMode) return;

        // ���ƴ�����
        Gizmos.color = Color.yellow;
        Vector3 triggerStart = new Vector3(-10f, triggerYPosition, 0f);
        Vector3 triggerEnd = new Vector3(10f, triggerYPosition, 0f);
        Gizmos.DrawLine(triggerStart, triggerEnd);

        // ���������ʼλ��
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(initialCameraPosition, Vector3.one * 0.5f);

        // ���Ƶ�ǰ����״̬
        if (Application.isPlaying)
        {
            Gizmos.color = isFollowing ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}