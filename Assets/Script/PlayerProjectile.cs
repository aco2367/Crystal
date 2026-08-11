using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerProjectile : MonoBehaviour
{
    [Header("Projectile")]
    public float lifetime = 3f;
    public bool destroyOnHit = true;
    [Tooltip("투사체가 닿으면 즉시 사라지는 벽/장애물 레이어입니다.")]
    public LayerMask obstacleLayers = 1 << 8;
    [Tooltip("기존 씬처럼 레이어가 Default여도 이름이 Wall인 부모 아래의 콜라이더를 벽으로 처리합니다.")]
    public bool detectWallParentByName = true;

    [Header("Visual")]
    public bool rotateToDirection = true;
    public int sortingOrder = 40;

    private int damage;
    private float maxDistance;
    private LayerMask enemyLayer;
    private Vector3 startPosition;
    private Rigidbody2D rb;
    [HideInInspector] public PlayerAugmentController ownerAugmentController;
    [HideInInspector] public PlayerDamageSource damageSource = PlayerDamageSource.Projectile;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        ApplySortingOrder();
    }

    private void ApplySortingOrder()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = sortingOrder;
        }
    }

    public void Launch(Vector2 direction, float speed, int projectileDamage, float range, LayerMask targetLayer)
    {
        damage = projectileDamage;
        maxDistance = range;
        enemyLayer = targetLayer;
        startPosition = transform.position;

        Vector2 normalizedDirection = direction.normalized;
        rb.linearVelocity = normalizedDirection * speed;

        if (rotateToDirection && normalizedDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (maxDistance <= 0f)
            return;

        float traveledDistance = Vector3.Distance(startPosition, transform.position);

        if (traveledDistance >= maxDistance)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsObstacle(other))
        {
            Destroy(gameObject);
            return;
        }

        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
            return;

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
            enemyHealth = other.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            return;

        int finalDamage = ownerAugmentController != null
            ? ownerAugmentController.ModifyOutgoingDamage(enemyHealth, damage, damageSource)
            : damage;
        bool wasAlive = enemyHealth.hp > 0;
        enemyHealth.TakeDamage(finalDamage);

        if (ownerAugmentController != null)
        {
            ownerAugmentController.OnHitEnemy(enemyHealth, finalDamage, damageSource);
            if (wasAlive && enemyHealth.hp <= 0)
                ownerAugmentController.OnEnemyKilled();
        }

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private bool IsObstacle(Collider2D other)
    {
        Transform current = other.transform;

        while (current != null)
        {
            if (IsInLayerMask(current.gameObject.layer, obstacleLayers))
                return true;

            if (detectWallParentByName && current.name == "Wall")
                return true;

            current = current.parent;
        }

        return false;
    }
}
