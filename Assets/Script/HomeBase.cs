using UnityEngine;

public class HomeBase : MonoBehaviour
{
    [Header("HP")]
    public int maxHp = 200;
    public int hp = 200;

    [Header("Collision")]
    public bool makeCollidersTrigger = true;

    private bool isDestroyed;

    private void Awake()
    {
        hp = Mathf.Clamp(hp, 0, maxHp);

        if (makeCollidersTrigger)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();

            foreach (Collider2D homeCollider in colliders)
            {
                homeCollider.isTrigger = true;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDestroyed)
            return;

        hp = Mathf.Max(0, hp - Mathf.Max(0, damage));
        Debug.Log($"집 피해: {damage}, 현재 HP: {hp}");

        if (hp <= 0)
        {
            DestroyHome();
        }
    }

    public void RepairFull()
    {
        hp = maxHp;
        isDestroyed = false;
    }

    private void DestroyHome()
    {
        isDestroyed = true;
        Debug.Log("집 파괴");
    }
}
