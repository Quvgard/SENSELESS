using UnityEngine;

/// <summary>
/// Камера от третьего лица:
/// - Свободно вращается вокруг игрока мышью/стиком.
/// - Двигается от точки над плечами (pivotOffset).
/// - Плавно следует за игроком и слегка "отстаёт".
/// - Автоматически выравнивается за спину при движении, если игрок не крутит камеру.
/// - Имеет простую коллизию со стенами (SphereCast).
/// 
/// Игрок ходит относительно направления камеры, т.к. PlayerMovement смотрит на Camera.main.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;                 // Корень игрока (Player_Rig)
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.7f, 0f); // Позиция головы/плеч

    [Header("Distance")]
    [SerializeField] private float distance = 4.5f;            // Базовая дистанция до цели
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 6.0f;

    [Header("Rotation (мышь/стик)")]
    [Tooltip("Чувствительность по горизонтали. Для мыши ~0.1–0.3, для геймпада можно больше.")]
    [SerializeField] private float horizontalSensitivity = 0.2f;
    [Tooltip("Чувствительность по вертикали.")]
    [SerializeField] private float verticalSensitivity = 0.15f;
    [SerializeField] private float minVerticalAngle = -20f;    // минимальный наклон (вниз)
    [SerializeField] private float maxVerticalAngle = 55f;     // максимальный наклон (вверх)
    [Tooltip("Сглаживание поворота камеры. Меньше = более плавно, но медленнее.")]
    [SerializeField] private float rotationSmoothSpeed = 10f;

    [Header("Auto Align (возврат за спину)")]
    [Tooltip("Включать ли авто-выравнивание камеры за спину игрока.")]
    [SerializeField] private bool autoAlignEnabled = true;
    [Tooltip("Через сколько секунд без вращения камеры начинать выравнивание.")]
    [SerializeField] private float autoAlignDelay = 2.0f;
    [Tooltip("Скорость поворота камеры за спину игрока.")]
    [SerializeField] private float autoAlignSpeed = 2.0f;

    [Header("Follow Smoothing")]
    [Tooltip("Плавность следования pivot за игроком.")]
    [SerializeField] private float followSmoothTime = 0.1f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers = ~0;   // Слои, с которыми камера сталкивается
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float collisionOffset = 0.2f;     // На сколько "недолетать" до стены
    [Tooltip("Скорость, с которой камера меняет дистанцию при столкновении.")]
    [SerializeField] private float distanceAdjustSpeed = 10f;

    // --- внутренние поля ---

    private Vector3 _currentPivotPos;      // сглаженная позиция pivot
    private Vector3 _pivotVelocity;

    private float _yaw;                    // угол вокруг Y
    private float _pitch;                  // угол наклона по X

    private float _currentDistance;        // сглаженная дистанция (фактическая)
    private float _lastManualRotateTime;   // время последнего ввода поворота камеры

    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();

        if (target == null)
        {
            Debug.LogWarning("CameraController: target не назначен! Камера будет отключена.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Инициализируем углы из текущего поворота камеры
        Vector3 angles = transform.eulerAngles;
        _pitch = Mathf.Clamp(angles.x, minVerticalAngle, maxVerticalAngle);
        _yaw = angles.y;

        _currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        // Начальный pivot — сразу в позиции игрока
        _currentPivotPos = target.position + pivotOffset;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateRotation();
        UpdatePivot();
        UpdatePositionWithCollision();
    }

    /// <summary>
    /// Обработка поворота камеры по вводу + авто-выравнивание за спину.
    /// </summary>
    private void UpdateRotation()
    {
        Vector2 lookInput = Vector2.zero;
        if (InputManager.Instance != null)
            lookInput = InputManager.Instance.Look;

        bool hasLookInput = lookInput.sqrMagnitude > 0.0001f;

        if (hasLookInput)
        {
            // --- Ручное вращение камерой мышью/стиком ---
            _yaw += lookInput.x * horizontalSensitivity;
            _pitch -= lookInput.y * verticalSensitivity;

            _pitch = Mathf.Clamp(_pitch, minVerticalAngle, maxVerticalAngle);

            _lastManualRotateTime = Time.time;
        }
        else if (autoAlignEnabled)
        {
            // --- Авто-выравнивание за спину, если игрок двигается и камеру давно не крутили ---
            float timeSinceRotate = Time.time - _lastManualRotateTime;

            if (timeSinceRotate > autoAlignDelay && InputManager.Instance != null)
            {
                Vector2 moveInput = InputManager.Instance.Move;
                bool isMoving = moveInput.sqrMagnitude > 0.01f;

                if (isMoving)
                {
                    // Целевой угол по forward игрока
                    Vector3 fwd = target.forward;
                    fwd.y = 0f;

                    if (fwd.sqrMagnitude > 0.0001f)
                    {
                        fwd.Normalize();
                        float targetYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;

                        _yaw = Mathf.LerpAngle(
                            _yaw,
                            targetYaw,
                            autoAlignSpeed * Time.deltaTime
                        );
                    }
                }
            }
        }

        // Целевой поворот камеры
        Quaternion targetRotation = Quaternion.Euler(_pitch, _yaw, 0f);

        // Плавный поворот
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Плавное следование pivot (точки над плечами) за игроком.
    /// </summary>
    private void UpdatePivot()
    {
        Vector3 desiredPivot = target.position + pivotOffset;

        _currentPivotPos = Vector3.SmoothDamp(
            _currentPivotPos,
            desiredPivot,
            ref _pivotVelocity,
            followSmoothTime
        );
    }

    /// <summary>
    /// Позиционируем камеру на нужной дистанции от pivot и учитываем коллизию со стенами.
    /// </summary>
    private void UpdatePositionWithCollision()
    {
        // Направление "назад" от pivot по текущему повороту камеры
        Vector3 back = transform.rotation * Vector3.back;
        back.Normalize();

        float desiredDistance = Mathf.Clamp(distance, minDistance, maxDistance);

        Vector3 desiredPosition = _currentPivotPos + back * desiredDistance;

        // --- Проверка коллизии ---
        Vector3 dir = (desiredPosition - _currentPivotPos).normalized;
        float baseDist = Vector3.Distance(_currentPivotPos, desiredPosition);
        float targetDist = baseDist;

        if (Physics.SphereCast(
                _currentPivotPos,
                collisionRadius,
                dir,
                out RaycastHit hit,
                baseDist + collisionOffset,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            // Попали в стену: подвигаем камеру ближе к игроку
            float hitDist = Mathf.Max(hit.distance - collisionOffset, minDistance);
            targetDist = Mathf.Clamp(hitDist, minDistance, baseDist);
        }

        // Плавно подгоняем текущую дистанцию под целевую
        _currentDistance = Mathf.Lerp(
            _currentDistance,
            targetDist,
            distanceAdjustSpeed * Time.deltaTime
        );

        Vector3 finalPosition = _currentPivotPos + dir * _currentDistance;
        transform.position = finalPosition;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Gizmos.color = Color.cyan;
        Vector3 pivot = (Application.isPlaying ? _currentPivotPos : target.position + pivotOffset);
        Gizmos.DrawWireSphere(pivot, 0.1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pivot, minDistance);
        Gizmos.DrawWireSphere(pivot, distance);
    }
#endif
}