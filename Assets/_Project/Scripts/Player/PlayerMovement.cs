using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Speeds")]
    [SerializeField] private float walkSpeed = 4f;   // стандартная скорость
    [SerializeField] private float runSpeed = 7f;   // бег
    [SerializeField] private float crouchSpeed = 2f;   // присед

    [Header("Crouch Collider")]
    [SerializeField] private float crouchHeightMultiplier = 0.6f; // уменьшение высоты капсулы

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 10f; // плавность поворота модели

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;           // максимальная скорость рывка
    [SerializeField] private float dashDurationNormal = 0.25f;
    [SerializeField] private float dashDurationCrouch = 0.2f;
    [SerializeField]
    private AnimationCurve dashSpeedCurve =
        AnimationCurve.EaseInOut(0f, 1f, 1f, 0f); // резкий старт -> плавное затухание

    [Header("Animation")]
    [SerializeField] private Animator animator; // Animator на модельке
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIsCrouching = Animator.StringToHash("IsCrouching");

    private CharacterController _controller;

    private bool _isCrouching;
    private float _animSpeed;

    // сохранённый нормальный хитбокс
    private float _originalHeight;
    private Vector3 _originalCenter;

    // ---- Dash ----
    private bool _isDashing;
    private bool _dashFromCrouch;
    private float _dashTimer;
    private float _dashDurationCurrent;
    private Vector3 _dashDirection;

    public bool IsDashing => _isDashing;
    public bool DashFromCrouch => _dashFromCrouch;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _originalHeight = _controller.height;
        _originalCenter = _controller.center;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("PlayerMovement: Animator not assigned and not found in children.", this);
            }
        }

        if (animator != null)
            animator.applyRootMotion = false;
    }

    /// <summary>Вызывается PlayerController при входе/выходе из стейта Crouch.</summary>
    public void SetCrouch(bool active)
    {
        if (_isCrouching == active)
            return;

        _isCrouching = active;

        if (active)
        {
            // Уменьшаем высоту, оставляя нижнюю точку на том же уровне
            float newHeight = _originalHeight * crouchHeightMultiplier;
            float delta = _originalHeight - newHeight;

            _controller.height = newHeight;
            _controller.center = new Vector3(
                _originalCenter.x,
                _originalCenter.y - delta * 0.5f,
                _originalCenter.z
            );
        }
        else
        {
            _controller.height = _originalHeight;
            _controller.center = _originalCenter;
        }
    }

    // ----------- NORMAL MOVEMENT (с бегом) --------------

    public void HandleMovement(Vector2 moveInput, Vector2 lookInput, bool isRunningInput)
    {
        if (_isDashing)
            return; // во время рывка обычное движение не выполняем

        Vector3 moveDir;
        bool hasInput = TryGetMoveDirection(moveInput, out moveDir);

        float targetSpeed = 0f;
        if (hasInput)
        {
            bool isRunning = isRunningInput;
            targetSpeed = isRunning ? runSpeed : walkSpeed;
        }

        MoveAndRotate(moveDir, hasInput, targetSpeed);
        UpdateAnimator(targetSpeed);
    }

    // ----------- CROUCH MOVEMENT ------------------------

    public void HandleCrouch(Vector2 moveInput)
    {
        if (_isDashing)
            return;

        Vector3 moveDir;
        bool hasInput = TryGetMoveDirection(moveInput, out moveDir);

        float targetSpeed = hasInput ? crouchSpeed : 0f;

        MoveAndRotate(moveDir, hasInput, targetSpeed);
        UpdateAnimator(targetSpeed);
    }

    // ----------- DASH (рывок) ---------------------------

    /// <summary>
    /// Запускает рывок в направлении ввода (или вперёд, если ввода нет).
    /// Возвращает true, если рывок успешно начат.
    /// </summary>
    public bool BeginDash(Vector2 moveInput, bool fromCrouch)
    {
        if (_isDashing)
            return false;

        Vector3 dir;
        bool hasInput = TryGetMoveDirection(moveInput, out dir);

        if (!hasInput)
        {
            dir = transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return false;
            dir.Normalize();
        }

        _dashDirection = dir;
        _dashFromCrouch = fromCrouch;
        _dashDurationCurrent = fromCrouch ? dashDurationCrouch : dashDurationNormal;
        _dashTimer = 0f;
        _isDashing = true;

        return true;
    }

    /// <summary>
    /// Обновляет движение рывка. Возвращает true, если рывок завершился.
    /// </summary>
    public bool UpdateDash(float deltaTime)
    {
        if (!_isDashing)
            return true;

        _dashTimer += deltaTime;
        float t = Mathf.Clamp01(_dashTimer / _dashDurationCurrent);

        float curveValue = dashSpeedCurve != null ? dashSpeedCurve.Evaluate(t) : (1f - t);
        float currentSpeed = dashSpeed * Mathf.Max(0f, curveValue);

        Vector3 velocity = _dashDirection * currentSpeed;
        _controller.SimpleMove(velocity);

        // Поворачиваемся в сторону рывка
        Quaternion targetRot = Quaternion.LookRotation(_dashDirection, Vector3.up);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            rotationSpeed * deltaTime
        );

        // Обновляем анимацию (скорость нормализуем до 0..1)
        UpdateAnimator(currentSpeed);

        if (_dashTimer >= _dashDurationCurrent)
        {
            _isDashing = false;
            return true;
        }

        return false;
    }

    // ----------- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ----------------

    private void MoveAndRotate(Vector3 moveDir, bool hasInput, float speed)
    {
        Vector3 velocity = hasInput ? moveDir * speed : Vector3.zero;
        _controller.SimpleMove(velocity);

        if (hasInput)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                rotationSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// 3.1 — движение от камеры: вперёд всегда "вглубь экрана".
    /// Берём forward/right камеры, игнорируя её наклон по Y.
    /// </summary>
    private bool TryGetMoveDirection(Vector2 moveInput, out Vector3 moveDir)
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        if (Camera.main != null)
        {
            Transform cam = Camera.main.transform;

            Vector3 camForward = cam.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = cam.right;
            camRight.y = 0f;
            camRight.Normalize();

            moveDir = camRight * input.x + camForward * input.z;
        }
        else
        {
            moveDir = input;
        }

        bool hasInput = moveDir.sqrMagnitude > 0.0001f;
        if (hasInput)
            moveDir.Normalize();

        return hasInput;
    }

    private void UpdateAnimator(float moveSpeed)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        float maxSpeed = _isCrouching ? crouchSpeed : runSpeed;
        float target = 0f;
        if (maxSpeed > 0.01f)
            target = Mathf.Clamp01(moveSpeed / maxSpeed);

        _animSpeed = Mathf.Lerp(_animSpeed, target, 10f * Time.deltaTime);

        animator.SetFloat(AnimSpeed, _animSpeed);
        animator.SetBool(AnimIsCrouching, _isCrouching);
    }
}