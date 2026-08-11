using UnityEngine;
using UnityEngine.InputSystem;

public class CoinChest : MonoBehaviour
{
    [Header("Chest Sprite")]
    public SpriteRenderer chestSpriteRenderer;
    public Sprite closedSprite;
    public Sprite openedSprite;

    [Header("Coin")]
    public GameObject coinPrefab;
    public int coinCount = 10;
    public float spawnRadius = 0.4f;

    [Header("Interaction")]
    public float interactRange = 1.5f;

    private bool isOpened;
    private Transform player;

    private void Awake()
    {
        if (chestSpriteRenderer == null)
        {
            chestSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (chestSpriteRenderer != null && closedSprite != null)
        {
            chestSpriteRenderer.sprite = closedSprite;
        }
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (isOpened)
            return;

        if (player == null)
            return;

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance > interactRange)
            return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            OpenChest();
        }
    }

    private void OpenChest()
    {
        isOpened = true;

        if (chestSpriteRenderer != null && openedSprite != null)
        {
            chestSpriteRenderer.sprite = openedSprite;
        }

        SpawnCoins();

        Debug.Log($"상자 열림! 코인 {coinCount}개 생성");
    }

    private void SpawnCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogWarning("Coin Prefab이 연결되지 않았습니다.");
            return;
        }

        for (int i = 0; i < coinCount; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPosition = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);

            Instantiate(coinPrefab, spawnPosition, Quaternion.identity);
        }
    }
}