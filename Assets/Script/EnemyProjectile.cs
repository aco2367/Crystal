using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    public float lifetime = 4f;
    [Tooltip("투사체가 닿으면 즉시 사라지는 벽/장애물 레이어입니다.")]
    public LayerMask obstacleLayers = 1 << 8;
    [Tooltip("기존 씬처럼 레이어가 Default여도 이름이 Wall인 부모 아래의 콜라이더를 벽으로 처리합니다.")]
    public bool detectWallParentByName = true;

    private int damage;
    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    public void Launch(Vector2 direction, float speed, int projectileDamage)
    {
        damage = projectileDamage;
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsObstacle(other))
        {
            Destroy(gameObject);
            return;
        }

        PlayerStats playerStats = other.GetComponent<PlayerStats>();

        if (playerStats == null)
            playerStats = other.GetComponentInParent<PlayerStats>();

        if (playerStats == null)
            return;

        playerStats.TakeDamage(damage, null, true);
        Destroy(gameObject);
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
