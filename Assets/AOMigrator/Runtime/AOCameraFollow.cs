using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AOCameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] AOGridMap map;
    [SerializeField] float smoothTime = 0.08f;
    [SerializeField] Vector2 visualOffset = new Vector2(0f, 0.45f);
    Vector3 velocity;
    Camera cam;

    public void Initialize(Transform newTarget, AOGridMap newMap, bool snap = false)
    {
        target = newTarget;
        map = newMap;
        if (snap) SnapNow();
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();
    }

    public void SnapNow()
    {
        if (target == null || map == null) return;
        if (cam == null) cam = GetComponent<Camera>();
        velocity = Vector3.zero;
        transform.position = ClampedTargetPosition();
    }

    void LateUpdate()
    {
        if (target == null || map == null) return;
        if (cam == null) cam = GetComponent<Camera>();

        Vector3 desired = ClampedTargetPosition();
        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref velocity, smoothTime,
            Mathf.Infinity, AOMainMenuV140.EntranceOpen
                ? Time.unscaledDeltaTime
                : Time.deltaTime);
        transform.position = new Vector3(
            transform.position.x, transform.position.y, -10f);
    }

    Vector3 ClampedTargetPosition()
    {
        if (cam == null) cam = GetComponent<Camera>();

        Vector3 desired = new Vector3(
            target.position.x + visualOffset.x,
            target.position.y + visualOffset.y,
            -10f);

        if (AOMainMenuV140.EntranceOpen)
        {
            float time = Time.unscaledTime;
            desired.x += Mathf.Sin(time * 0.17f) * 1.1f;
            desired.y += Mathf.Sin(time * 0.12f) * 0.55f;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        float left = map.XMin - 1f;
        float right = map.XMax;
        float bottom = -map.YMax;
        float top = -(map.YMin - 1f);

        desired.x = (right - left <= halfW * 2f)
            ? (left + right) * 0.5f
            : Mathf.Clamp(desired.x, left + halfW, right - halfW);

        desired.y = (top - bottom <= halfH * 2f)
            ? (bottom + top) * 0.5f
            : Mathf.Clamp(desired.y, bottom + halfH, top - halfH);

        return desired;
    }
}
