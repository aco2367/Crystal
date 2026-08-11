using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    [Header("Shake")]
    public float defaultDuration = 0.08f;
    public float defaultStrength = 0.08f;

    private Coroutine shakeCoroutine;
    private Vector3 currentOffset;

    private void Awake()
    {
        Instance = this;
    }

    public void Shake()
    {
        Shake(defaultDuration, defaultStrength);
    }

    public void Shake(float duration, float strength)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            RemoveCurrentOffset();
        }

        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, strength));
    }

    private IEnumerator ShakeRoutine(float duration, float strength)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            RemoveCurrentOffset();

            float x = Random.Range(-1f, 1f) * strength;
            float y = Random.Range(-1f, 1f) * strength;

            currentOffset = new Vector3(x, y, 0f);
            transform.position += currentOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        RemoveCurrentOffset();
        shakeCoroutine = null;
    }

    private void RemoveCurrentOffset()
    {
        if (currentOffset == Vector3.zero)
            return;

        transform.position -= currentOffset;
        currentOffset = Vector3.zero;
    }
}
