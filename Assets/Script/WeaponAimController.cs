using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAimController : MonoBehaviour
{
    [Header("References")]
    public Transform playerRoot;
    public Transform weaponPivot;
    public Camera targetCamera;

    [Header("Aim Rotation")]
    public float swordWeaponTipAngleOffset;
    public float archerWeaponTipAngleOffset;
    public float tankWeaponTipAngleOffset;
    public bool compensateActiveWeaponLocalRotation = true;
    public float minAimAngle = -90f;
    public float maxAimAngle = 90f;
    public float attackSwingAngle = -45f;

    [Header("Role Pivot Positions")]
    public Vector2 swordPivotPosition = Vector2.zero;
    public Vector2 archerPivotPosition = new Vector2(0.35f, 0f);
    public Vector2 tankPivotPosition = new Vector2(0.55f, 0f);

    [Header("Sword/Archer Orbit")]
    public float swordOrbitRadius = 0.55f;
    public float archerOrbitRadius = 0.65f;

    [Header("Flip")]
    public bool flipWeaponOnLeft = true;
    public bool swordFlipWeaponOnLeft = true;
    public bool archerFlipWeaponOnLeft;
    public bool tankFlipWeaponOnLeft = true;
    public bool mirrorRotationOnLeft = true;
    public bool swordMirrorRotationOnLeft = true;
    public bool archerMirrorRotationOnLeft;
    public bool tankMirrorRotationOnLeft = true;
    public bool compensateParentFlip = true;

    [Header("Attack Motion")]
    public float attackDuration = 0.08f;
    public float returnDuration = 0.12f;
    public bool lockSideDuringAttack = true;

    [Header("Tank Side Aim")]
    public bool tankUsesSideOnlyAim = true;
    public float tankSideOnlyRotation;

    private Coroutine attackCoroutine;
    private bool isAttacking;
    private bool lockedMouseIsLeft;
    private PlayerController playerController;
    private readonly Dictionary<Transform, Vector3> baseWeaponScales = new Dictionary<Transform, Vector3>();

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        CacheBaseWeaponScales();
    }

    private void Reset()
    {
        playerRoot = transform;

        Transform foundPivot = transform.Find("WeaponPivot");
        if (foundPivot != null)
        {
            weaponPivot = foundPivot;
        }
    }

    private void LateUpdate()
    {
        if (weaponPivot == null)
            return;

        bool mouseIsLeft = isAttacking && lockSideDuringAttack
            ? lockedMouseIsLeft
            : IsMouseLeftOfPlayer();

        if (!isAttacking)
        {
            ApplyPose(mouseIsLeft, GetIdleRotation(mouseIsLeft));
        }
    }

    public void PlayAttackSlash()
    {
        if (weaponPivot == null)
            return;

        if (attackCoroutine != null)
        {
            StopCoroutine(attackCoroutine);
        }

        lockedMouseIsLeft = IsMouseLeftOfPlayer();
        attackCoroutine = StartCoroutine(AttackSlashRoutine(lockedMouseIsLeft));
    }

    private IEnumerator AttackSlashRoutine(bool mouseIsLeft)
    {
        isAttacking = true;

        float idleRotation = GetIdleRotation(mouseIsLeft);
        float attackRotation = idleRotation + attackSwingAngle;

        yield return RotatePose(mouseIsLeft, idleRotation, attackRotation, attackDuration);
        yield return RotatePose(mouseIsLeft, attackRotation, idleRotation, returnDuration);

        isAttacking = false;
        attackCoroutine = null;
    }

    private IEnumerator RotatePose(bool mouseIsLeft, float fromRotation, float toRotation, float duration)
    {
        if (duration <= 0f)
        {
            ApplyPose(mouseIsLeft, toRotation);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 2f);
            float rotation = Mathf.LerpAngle(fromRotation, toRotation, easedT);
            ApplyPose(mouseIsLeft, rotation);
            yield return null;
        }

        ApplyPose(mouseIsLeft, toRotation);
    }

    private bool IsMouseLeftOfPlayer()
    {
        if (Mouse.current == null)
            return false;

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
            return false;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = cameraToUse.ScreenToWorldPoint(mouseScreenPosition);
        Vector3 origin = playerRoot != null ? playerRoot.position : transform.position;
        return mouseWorldPosition.x < origin.x;
    }

    private float GetAimRotation(bool mouseIsLeft)
    {
        Vector2 direction = GetMouseDirection(mouseIsLeft);
        float localAimAngle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
        localAimAngle = Mathf.Clamp(localAimAngle, minAimAngle, maxAimAngle);
        float rotation = localAimAngle + GetCurrentWeaponTipAngleOffset();

        if (compensateActiveWeaponLocalRotation)
        {
            rotation -= GetActiveWeaponLocalZRotation();
        }

        return rotation;
    }

    private float GetCurrentWeaponTipAngleOffset()
    {
        if (playerController == null)
            return swordWeaponTipAngleOffset;

        switch (playerController.CurrentRole)
        {
            case PlayerRole.Sword:
                return swordWeaponTipAngleOffset;
            case PlayerRole.Archer:
                return archerWeaponTipAngleOffset;
            case PlayerRole.Tank:
                return tankWeaponTipAngleOffset;
            default:
                return swordWeaponTipAngleOffset;
        }
    }

    private float GetIdleRotation(bool mouseIsLeft)
    {
        if (ShouldUseTankSideOnlyAim())
            return tankSideOnlyRotation;

        return GetAimRotation(mouseIsLeft);
    }

    private bool ShouldUseTankSideOnlyAim()
    {
        return tankUsesSideOnlyAim
            && playerController != null
            && playerController.CurrentRole == PlayerRole.Tank;
    }

    private Vector2 GetMouseDirection(bool mouseIsLeft)
    {
        if (Mouse.current == null)
            return Vector2.right;

        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
            return Vector2.right;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = cameraToUse.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;

        Vector3 origin = GetAimCenterWorldPosition(mouseIsLeft);
        Vector2 direction = (Vector2)(mouseWorldPosition - origin);

        if (direction == Vector2.zero)
            return Vector2.right;

        return direction.normalized;
    }

    private Vector3 GetAimCenterWorldPosition(bool mouseIsLeft)
    {
        if (weaponPivot == null)
            return playerRoot != null ? playerRoot.position : transform.position;

        Vector3 localPosition = GetOrbitCenterLocalPosition(mouseIsLeft);

        if (weaponPivot.parent != null)
            return weaponPivot.parent.TransformPoint(localPosition);

        return localPosition;
    }

    private void ApplyPose(bool mouseIsLeft, float rotationZ)
    {
        float sideSign = mouseIsLeft ? -1f : 1f;
        bool parentFlipped = compensateParentFlip && weaponPivot.parent != null && weaponPivot.parent.lossyScale.x < 0f;
        float parentSign = parentFlipped ? -1f : 1f;

        Vector3 localPosition = GetPivotLocalPosition(mouseIsLeft);
        float localRotationZ = ShouldMirrorCurrentWeaponRotationOnLeft() && mouseIsLeft ? -rotationZ : rotationZ;

        weaponPivot.localPosition = localPosition;
        weaponPivot.localScale = new Vector3(parentSign, 1f, 1f);
        weaponPivot.localRotation = Quaternion.Euler(0f, 0f, localRotationZ);

        ApplyActiveWeaponFlip(mouseIsLeft);
    }

    private Vector3 GetPivotLocalPosition(bool mouseIsLeft)
    {
        float sideSign = mouseIsLeft ? -1f : 1f;
        bool parentFlipped = compensateParentFlip && weaponPivot != null && weaponPivot.parent != null && weaponPivot.parent.lossyScale.x < 0f;
        float parentSign = parentFlipped ? -1f : 1f;
        Vector2 rolePivotPosition = GetCurrentRolePivotPosition();

        if (ShouldUseOrbitPosition())
        {
            Vector2 direction = GetMouseDirection(mouseIsLeft);
            float radius = GetCurrentOrbitRadius();
            Vector2 center = GetCurrentRolePivotPosition();
            return new Vector3(
                (center.x + direction.x * radius) * parentSign,
                center.y + direction.y * radius,
                0f
            );
        }

        return new Vector3(rolePivotPosition.x * sideSign * parentSign, rolePivotPosition.y, 0f);
    }

    private Vector3 GetOrbitCenterLocalPosition(bool mouseIsLeft)
    {
        bool parentFlipped = compensateParentFlip && weaponPivot != null && weaponPivot.parent != null && weaponPivot.parent.lossyScale.x < 0f;
        float parentSign = parentFlipped ? -1f : 1f;
        Vector2 center = GetCurrentRolePivotPosition();

        if (ShouldUseOrbitPosition())
            return new Vector3(center.x * parentSign, center.y, 0f);

        float sideSign = mouseIsLeft ? -1f : 1f;
        return new Vector3(center.x * sideSign * parentSign, center.y, 0f);
    }

    private bool ShouldUseOrbitPosition()
    {
        return playerController == null
            || playerController.CurrentRole == PlayerRole.Sword
            || playerController.CurrentRole == PlayerRole.Archer;
    }

    private float GetCurrentOrbitRadius()
    {
        if (playerController == null)
            return swordOrbitRadius;

        switch (playerController.CurrentRole)
        {
            case PlayerRole.Sword:
                return swordOrbitRadius;
            case PlayerRole.Archer:
                return archerOrbitRadius;
            default:
                return 0f;
        }
    }

    private Vector2 GetCurrentRolePivotPosition()
    {
        if (playerController == null)
            return Vector2.zero;

        switch (playerController.CurrentRole)
        {
            case PlayerRole.Sword:
                return swordPivotPosition;
            case PlayerRole.Archer:
                return archerPivotPosition;
            case PlayerRole.Tank:
                return tankPivotPosition;
            default:
                return Vector2.zero;
        }
    }

    private void ApplyActiveWeaponFlip(bool mouseIsLeft)
    {
        if (weaponPivot == null)
            return;

        Transform activeWeapon = GetActiveWeapon();
        if (activeWeapon == null)
            return;

        Vector3 scale = GetBaseWeaponScale(activeWeapon);
        float scaleSign = ShouldFlipCurrentWeaponOnLeft() && mouseIsLeft ? -1f : 1f;
        scale.x = Mathf.Abs(scale.x) * scaleSign;
        scale.y = Mathf.Abs(scale.y);
        activeWeapon.localScale = scale;
    }

    private void CacheBaseWeaponScales()
    {
        if (weaponPivot == null)
            return;

        for (int i = 0; i < weaponPivot.childCount; i++)
        {
            Transform child = weaponPivot.GetChild(i);
            baseWeaponScales[child] = new Vector3(
                Mathf.Abs(child.localScale.x),
                Mathf.Abs(child.localScale.y),
                child.localScale.z
            );
        }
    }

    private Vector3 GetBaseWeaponScale(Transform weapon)
    {
        if (weapon == null)
            return Vector3.one;

        if (baseWeaponScales.TryGetValue(weapon, out Vector3 scale))
            return scale;

        scale = new Vector3(Mathf.Abs(weapon.localScale.x), Mathf.Abs(weapon.localScale.y), weapon.localScale.z);
        baseWeaponScales[weapon] = scale;
        return scale;
    }

    private bool ShouldFlipCurrentWeaponOnLeft()
    {
        if (!flipWeaponOnLeft)
            return false;

        if (playerController == null)
            return true;

        switch (playerController.CurrentRole)
        {
            case PlayerRole.Sword:
                return swordFlipWeaponOnLeft;
            case PlayerRole.Archer:
                return archerFlipWeaponOnLeft;
            case PlayerRole.Tank:
                return tankFlipWeaponOnLeft;
            default:
                return true;
        }
    }

    private bool ShouldMirrorCurrentWeaponRotationOnLeft()
    {
        if (!mirrorRotationOnLeft)
            return false;

        if (playerController == null)
            return true;

        switch (playerController.CurrentRole)
        {
            case PlayerRole.Sword:
                return swordMirrorRotationOnLeft;
            case PlayerRole.Archer:
                return archerMirrorRotationOnLeft;
            case PlayerRole.Tank:
                return tankMirrorRotationOnLeft;
            default:
                return true;
        }
    }

    private Transform GetActiveWeapon()
    {
        for (int i = 0; i < weaponPivot.childCount; i++)
        {
            Transform child = weaponPivot.GetChild(i);

            if (child.gameObject.activeInHierarchy)
                return child;
        }

        return null;
    }

    private float GetActiveWeaponLocalZRotation()
    {
        Transform activeWeapon = GetActiveWeapon();
        if (activeWeapon == null)
            return 0f;

        return NormalizeAngle(activeWeapon.localEulerAngles.z);
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;

        if (angle > 180f)
            angle -= 360f;

        if (angle < -180f)
            angle += 360f;

        return angle;
    }
}
