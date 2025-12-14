using UnityEngine;
using Cinemachine;

public class CameraManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;               // root Player
    [SerializeField] private CinemachineVirtualCamera vcam;  // VCam_Iso
    [SerializeField] private BoxCollider bounds;             // границы арены (опционально)

    [Header("Isometric setup")]
    [SerializeField] private Vector3 isoEuler = new Vector3(35f, 0f, 0f); // угол камеры (Pitch, Yaw, Roll)
    [SerializeField] private float cameraDistance = 10f;                   // расстояние от фокуса до камеры
    [SerializeField] private float baseOrthoSize = 6f;                     // базовый Orthographic Size
    [SerializeField] private Vector3 focusOffset = Vector3.zero;           // сдвиг фокуса (если нужно сместить игрока в кадре)

    [Header("Follow")]
    [SerializeField] private float followSmoothTime = 0.15f; // сглаживание камеры
    [SerializeField] private float lookAheadDistance = 3f;   // смещение фокуса вперёд по движению

    [Header("Combat Focus")]
    [SerializeField] private Transform currentEnemy;         // цель lock-on (опционально)
    [SerializeField] private float combatZoomFactor = 0.85f; // уменьшение size при ближнем бое
    [SerializeField] private float zoomSmoothSpeed = 3f;
    [SerializeField] private float closeCombatDistance = 3f;
    [SerializeField] private float farCombatDistance = 8f;

    private Vector3 _currentFocusPos;
    private Vector3 _focusVelocity;
    private float _currentOrthoSize;
    private Quaternion _isoRotation;

    private void Awake()
    {
        if (vcam == null)
        {
            Debug.LogError("CameraManager: VCam не назначен!");
            enabled = false;
            return;
        }

        _isoRotation = Quaternion.Euler(isoEuler);
        vcam.m_Lens.Orthographic = true;
        vcam.m_Lens.OrthographicSize = baseOrthoSize;
        vcam.transform.rotation = _isoRotation;

        _currentOrthoSize = baseOrthoSize;

        if (player != null)
        {
            _currentFocusPos = player.position;
        }
    }

    private void LateUpdate()
    {
        if (player == null || vcam == null)
            return;

        // 1. Фокус
        Vector3 desiredFocus = CalculateFocusPoint();
        desiredFocus = ClampToBounds(desiredFocus);

        // 2. Плавное следование
        _currentFocusPos = Vector3.SmoothDamp(
            _currentFocusPos,
            desiredFocus,
            ref _focusVelocity,
            followSmoothTime
        );

        // 3. Позиция камеры: фокус + смещение вдоль взгляда
        Vector3 camOffset = _isoRotation * (Vector3.back * cameraDistance);
        Vector3 camPos = _currentFocusPos + camOffset + focusOffset;

        vcam.transform.position = camPos;

        // 4. Зум в бою
        UpdateZoom();
    }

    private Vector3 CalculateFocusPoint()
    {
        Vector3 basePos = player.position;

        // Lock-on: midpoint игрок–враг
        if (currentEnemy != null)
        {
            Vector3 mid = (player.position + currentEnemy.position) * 0.5f;
            return mid;
        }

        // Обычный режим: немного вперёд по движению
        Vector3 focus = basePos;

        if (InputManager.Instance != null)
        {
            Vector2 moveInput = InputManager.Instance.Move;
            if (moveInput.sqrMagnitude > 0.001f)
            {
                Vector3 dir = CalculateWorldMoveDir(moveInput);
                focus += dir * lookAheadDistance;
            }
        }

        return focus;
    }

    private Vector3 CalculateWorldMoveDir(Vector2 moveInput)
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Camera cam = Camera.main;
        if (cam != null)
        {
            Transform ct = cam.transform;
            Vector3 camForward = ct.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = ct.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 dir = camRight * input.x + camForward * input.z;
            if (dir.sqrMagnitude > 0.0001f)
                dir.Normalize();
            return dir;
        }

        return input;
    }

    private Vector3 ClampToBounds(Vector3 pos)
    {
        if (bounds == null)
            return pos;

        Bounds b = bounds.bounds;

        pos.x = Mathf.Clamp(pos.x, b.min.x, b.max.x);
        pos.z = Mathf.Clamp(pos.z, b.min.z, b.max.z);

        return pos;
    }

    private void UpdateZoom()
    {
        float targetSize = baseOrthoSize;

        if (currentEnemy != null)
        {
            float dist = Vector3.Distance(player.position, currentEnemy.position);
            float t = Mathf.InverseLerp(farCombatDistance, closeCombatDistance, dist);
            t = Mathf.Clamp01(t);

            float minSize = baseOrthoSize * combatZoomFactor;
            targetSize = Mathf.Lerp(baseOrthoSize, minSize, t);
        }

        _currentOrthoSize = Mathf.Lerp(
            _currentOrthoSize,
            targetSize,
            zoomSmoothSpeed * Time.deltaTime
        );

        vcam.m_Lens.OrthographicSize = _currentOrthoSize;
    }

    public void SetCombatTarget(Transform enemy)
    {
        currentEnemy = enemy;
    }
}