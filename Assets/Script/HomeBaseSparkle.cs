using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class HomeBaseSparkle : MonoBehaviour
{
    [Header("Sparkle Timing")]
    [Min(0f)]
    [SerializeField] private float waitSeconds = 4f;

    [Header("Animator")]
    [SerializeField] private string sparkleTriggerName = "Sparkle";
    [SerializeField] private string sparkleStateName = "Sparkle";
    [Min(0)]
    [SerializeField] private int animatorLayer = 0;

    private Animator animator;
    private int sparkleTriggerHash;
    private int sparkleStateHash;
    private Coroutine sparkleRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        sparkleTriggerHash = Animator.StringToHash(sparkleTriggerName);
        sparkleStateHash = Animator.StringToHash(sparkleStateName);
    }

    private void OnEnable()
    {
        sparkleRoutine = StartCoroutine(SparkleLoop());
    }

    private void OnDisable()
    {
        if (sparkleRoutine != null)
        {
            StopCoroutine(sparkleRoutine);
            sparkleRoutine = null;
        }

        if (animator != null)
            animator.ResetTrigger(sparkleTriggerHash);
    }

    private IEnumerator SparkleLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(waitSeconds);

            animator.ResetTrigger(sparkleTriggerHash);
            animator.SetTrigger(sparkleTriggerHash);

            // Animator가 Sparkle 상태로 전환될 때까지 기다린다.
            yield return new WaitUntil(IsPlayingSparkle);

            // Sparkle이 끝나 Idle로 돌아올 때까지 기다린 뒤 대기 시간을 다시 센다.
            yield return new WaitWhile(IsPlayingSparkle);
        }
    }

    private bool IsPlayingSparkle()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        return state.shortNameHash == sparkleStateHash;
    }
}
