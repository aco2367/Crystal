using UnityEngine;

public class ExperiencePickup : MonoBehaviour
{
    [Min(0)] public int value = 1;

    [Header("Visual")]
    public Transform visual;
    public float pickupScale = 1f;

    [Header("Absorb")]
    public float delayBeforeMove = 0.25f;
    public float moveSpeed = 6f;
    public float acceleration = 12f;
    public float collectDistance = 0.35f;
    [Tooltip("플레이어가 이 거리 안에 있을 때만 경험치가 끌려갑니다.")]
    [Min(0f)] public float attractionRange = 4f;

    [Header("Spawn Pop")]
    [Tooltip("퍼지는 거리 전체에 적용되는 배율입니다.")]
    [Min(0f)] public float popForce = 1.5f;
    public float popDuration = 0.2f;
    [Min(0f)] public float minScatterDistance = 0.2f;
    [Min(0f)] public float maxScatterDistance = 0.45f;
    [Min(0f)] public float scatterArcHeight = 0.2f;

    private Transform target;
    private PlayerStats targetStats;
    private float spawnTime;
    private float currentSpeed;
    private Vector3 popDirection;
    private Vector3 startPosition;
    private Vector3 landingPosition;

    private void Awake()
    {
        Transform scaleTarget = visual != null ? visual : transform;
        scaleTarget.localScale = Vector3.one * pickupScale;
    }

    private void Start()
    {
        spawnTime = Time.time;
        startPosition = transform.position;
        currentSpeed = moveSpeed;

        Vector2 randomDirection = Random.insideUnitCircle;
        popDirection = randomDirection.sqrMagnitude > 0.001f
            ? randomDirection.normalized
            : Vector2.up;
        float minDistance = Mathf.Min(minScatterDistance, maxScatterDistance);
        float maxDistance = Mathf.Max(minScatterDistance, maxScatterDistance);
        float scatterDistance = Random.Range(minDistance, maxDistance) * popForce;
        landingPosition = startPosition + popDirection * scatterDistance;

        FindPlayerTarget();
    }

    private void Update()
    {
        if (target == null || targetStats == null)
        {
            FindPlayerTarget();

            if (target == null || targetStats == null)
                return;
        }

        float elapsed = Time.time - spawnTime;

        if (elapsed < popDuration)
        {
            float progress = popDuration > 0f ? Mathf.Clamp01(elapsed / popDuration) : 1f;
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 position = Vector3.Lerp(startPosition, landingPosition, easedProgress);
            position.y += Mathf.Sin(progress * Mathf.PI) * scatterArcHeight;
            transform.position = position;
            return;
        }

        transform.position = elapsed - Time.deltaTime < popDuration
            ? landingPosition
            : transform.position;

        if (elapsed < delayBeforeMove)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, target.position);

        if (attractionRange <= 0f || distanceToPlayer > attractionRange)
        {
            currentSpeed = moveSpeed;
            return;
        }

        currentSpeed += acceleration * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            currentSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.position) <= collectDistance)
            Collect();
    }

    private void FindPlayerTarget()
    {
        targetStats = FindFirstObjectByType<PlayerStats>();

        if (targetStats != null)
            target = targetStats.transform;
    }

    private void Collect()
    {
        if (targetStats != null)
            targetStats.AddExp(value);

        Destroy(gameObject);
    }
}
