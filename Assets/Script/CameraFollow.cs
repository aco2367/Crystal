using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public string playerTag = "Player";

    [Header("Follow")]
    [Tooltip("값이 클수록 카메라가 플레이어를 더 늦게 따라갑니다. 0.25~0.35 추천")]
    public float smoothTime = 0.28f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public bool snapToTargetOnSceneStart = true;

    [Header("Pixel Snapping")]
    public bool snapToPixelGrid = false;
    public int pixelsPerUnit = 32;

    [Header("Bounds")]
    public bool useBounds;
    public Vector2 minPosition;
    public Vector2 maxPosition;

    private Vector3 velocity;
    private bool hasSnappedToInitialTarget;

    private void Start()
    {
        if (target == null)
        {
            FindPlayer();
        }

        SnapToTargetIfNeeded();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindPlayer();

            if (target == null)
                return;

            SnapToTargetIfNeeded();
        }

        Vector3 targetPosition = target.position + offset;
        targetPosition = ClampToBounds(targetPosition);

        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothTime);

        if (snapToPixelGrid && pixelsPerUnit > 0)
        {
            nextPosition.x = Mathf.Round(nextPosition.x * pixelsPerUnit) / pixelsPerUnit;
            nextPosition.y = Mathf.Round(nextPosition.y * pixelsPerUnit) / pixelsPerUnit;
        }

        transform.position = nextPosition;
    }

    private void SnapToTargetIfNeeded()
    {
        if (!snapToTargetOnSceneStart || hasSnappedToInitialTarget || target == null)
            return;

        transform.position = ClampToBounds(target.position + offset);
        velocity = Vector3.zero;
        hasSnappedToInitialTarget = true;
    }

    private Vector3 ClampToBounds(Vector3 position)
    {
        if (!useBounds)
            return position;

        position.x = Mathf.Clamp(position.x, minPosition.x, maxPosition.x);
        position.y = Mathf.Clamp(position.y, minPosition.y, maxPosition.y);
        return position;
    }

    private void FindPlayer()
    {
        PlayerController playerController = FindFirstObjectByType<PlayerController>();

        if (playerController != null)
        {
            target = playerController.transform;
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObject != null)
        {
            PlayerController controller = playerObject.GetComponentInParent<PlayerController>();
            target = controller != null ? controller.transform : playerObject.transform;
        }
    }
}

