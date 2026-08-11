using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class AttackRangeVisualizer : MonoBehaviour
{
    [Header("Colors")]
    public Color swordColor = new Color(1f, 0.9f, 0.1f, 0.25f);
    public Color archerColor = new Color(0.2f, 1f, 0.3f, 0.25f);
    public Color tankColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    [Header("Shape")]
    public int circleSegments = 64;
    public float archerWidth = 0.25f;
    public int sortingOrder = 15;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private Material material;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
    }

    public void DrawRange(
        PlayerRole role,
        Vector2 direction,
        float swordRange,
        float swordAngle,
        float archerRange,
        float tankRange
    )
    {
        if (direction == Vector2.zero)
            direction = Vector2.down;

        switch (role)
        {
            case PlayerRole.Sword:
                DrawFan(direction, swordRange, swordAngle, swordColor);
                break;

            case PlayerRole.Archer:
                DrawRectangle(direction, archerRange, archerWidth, archerColor);
                break;

            case PlayerRole.Tank:
                DrawCircle(tankRange, tankColor);
                break;
        }
    }

    private void DrawFan(Vector2 direction, float range, float angle, Color color)
    {
        int segments = Mathf.Max(8, Mathf.RoundToInt(circleSegments * (angle / 360f)));
        Vector3[] vertices = new Vector3[segments + 2];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;

        float startAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - angle * 0.5f;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = startAngle + angle * i / segments;
            float rad = currentAngle * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * range;
        }

        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        ApplyMesh(vertices, triangles, color);
    }

    private void DrawCircle(float range, Color color)
    {
        Vector3[] vertices = new Vector3[circleSegments + 2];
        int[] triangles = new int[circleSegments * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = 360f * i / circleSegments;
            float rad = angle * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * range;
        }

        for (int i = 0; i < circleSegments; i++)
        {
            int triangleIndex = i * 3;
            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = i + 1;
            triangles[triangleIndex + 2] = i + 2;
        }

        ApplyMesh(vertices, triangles, color);
    }

    private void DrawRectangle(Vector2 direction, float range, float width, Color color)
    {
        Vector2 forward = direction.normalized;
        Vector2 right = new Vector2(-forward.y, forward.x) * (width * 0.5f);

        Vector3[] vertices = new Vector3[4];
        vertices[0] = right;
        vertices[1] = -right;
        vertices[2] = forward * range - right;
        vertices[3] = forward * range + right;

        int[] triangles =
        {
            0, 1, 2,
            0, 2, 3
        };

        ApplyMesh(vertices, triangles, color);
    }

    private void ApplyMesh(Vector3[] vertices, int[] triangles, Color color)
    {
        EnsureInitialized();

        if (mesh == null || material == null)
            return;

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        material.color = color;
    }

    private void EnsureInitialized()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Attack Range Mesh";
        }

        if (meshFilter != null && meshFilter.sharedMesh != mesh)
            meshFilter.mesh = mesh;

        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                material = new Material(shader);
                material.color = Color.white;
            }
        }

        if (meshRenderer != null)
        {
            if (material != null)
                meshRenderer.material = material;

            meshRenderer.sortingOrder = sortingOrder;
        }
    }
}
