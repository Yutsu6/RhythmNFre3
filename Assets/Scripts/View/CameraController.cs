using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("相机设置")]
    public Transform cursor;                   // 光标引用
    public Vector3 initialCameraPosition = new Vector3(8f, -4f, -9f); // 初始位置
    public float triggerYPosition = -6f;       // 触发跟随的Y坐标

    [Header("跟随设置")]
    public float yOffset = 0f;                 // Y轴偏移

    [Header("状态")]
    public bool isFollowing = false;           // 是否正在跟随
    public bool debugMode = true;              // 调试模式

    private Vector3 targetPosition;
    private float initialX;                    // 初始X位置（固定）

    void Start()
    {
        // 设置相机初始位置
        transform.position = initialCameraPosition;
        initialX = initialCameraPosition.x;

        if (cursor == null)
        {
            Debug.LogError("CameraController: 没有设置光标引用！");
        }

        if (debugMode)
        {
            Debug.Log($"相机初始化: 位置={transform.position}, 触发Y={triggerYPosition}");
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

        // 检查光标是否到达触发位置
        if (cursor.position.y <= triggerYPosition)
        {
            isFollowing = true;
            if (debugMode)
            {
                Debug.Log($"开始相机跟随! 光标Y={cursor.position.y}, 触发Y={triggerYPosition}");
            }
        }
    }

    void UpdateCameraPosition()
    {
        if (!isFollowing)
        {
            // 跟随前：保持初始位置
            transform.position = initialCameraPosition;
        }
        else
        {
            // 跟随模式：立即跳转到光标位置
            float targetY = cursor.position.y + yOffset;
            transform.position = new Vector3(initialX, targetY, initialCameraPosition.z);
        }
    }

    // 公开方法
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

    // 在Scene视图中显示触发区域（调试用）
    void OnDrawGizmos()
    {
        if (!debugMode) return;

        // 绘制触发线
        Gizmos.color = Color.yellow;
        Vector3 triggerStart = new Vector3(-10f, triggerYPosition, 0f);
        Vector3 triggerEnd = new Vector3(10f, triggerYPosition, 0f);
        Gizmos.DrawLine(triggerStart, triggerEnd);

        // 绘制相机初始位置
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(initialCameraPosition, Vector3.one * 0.5f);

        // 绘制当前跟随状态
        if (Application.isPlaying)
        {
            Gizmos.color = isFollowing ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}