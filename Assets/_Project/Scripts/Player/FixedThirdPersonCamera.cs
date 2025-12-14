    using UnityEngine;

/// <summary>
/// Стабильная слэшер-камера:
/// - всегда позади и чуть выше игрока,
/// - ПЛАВНО догоняет поворот персонажа,
/// - без ввода мыши/стика,
/// - сглаженное движение и простая коллизия.
/// </summary>
public class FixedThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;                   // root Player
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.7f, 0f); // точка около головы
    [SerializeField] private float pitch = 20f;                  // фиксированный наклон вниз (ограничивает вертикаль)
    [SerializeField] private float distance = 4f;                // дистанция позади

    [Header("Smoothing")]
    [SerializeField] private float yawFollowSpeed = 4f;       // чем меньше, тем плавнее камера догоняет поворот игрока
    [SerializeField] private float rotationSmooth = 8f;       // сглаживание собственного поворота камеры
    [SerializeField] private float positionSmoothTime = 0.2f;    // сглаживание позиции

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float collisionOffset = 0.2f;
    [SerializeField] private float minDistance = 1.2f;

    private float _yaw;                    // сглаженный угол вокруг Y
    private float _currentDistance;
    private Vector3 _positionVelocity;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("FixedThirdPersonCamera: target не назначен!");
            enabled = false;
            return;
        }

        _yaw = target.eulerAngles.y;
        _currentDistance = distance;

        // Стартовое положение
        Quaternion initialRot = Quaternion.Euler(pitch, _yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;
        Vector3 back = initialRot * Vector3.back;

        transform.position = pivot + back * distance;
        transform.rotation = initialRot;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        UpdateRotation();
        UpdatePosition();
    }

    private void UpdateRotation()
    {
        // Целевой угол = поворот игрока по Y
        float targetYaw = target.eulerAngles.y;

        // ПЛАВНО догоняем его
        _yaw = Mathf.LerpAngle(_yaw, targetYaw, yawFollowSpeed * Time.deltaTime);

        // Фиксированный pitch, без скачков по вертикали
        Quaternion desiredRotation = Quaternion.Euler(pitch, _yaw, 0f);

        // Дополнительное сглаживание поворота камеры
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmooth * Time.deltaTime
        );
    }

    private void UpdatePosition()
    {
        Vector3 pivot = target.position + pivotOffset;

        // Вектор "назад" от текущего ОБНОВЛЁННОГО поворота камеры
        Vector3 back = transform.rotation * Vector3.back;

        // Идеальная позиция
        Vector3 desiredPosition = pivot + back * distance;

        // Коллизия
        Vector3 dir = (desiredPosition - pivot).normalized;
        float baseDist = Vector3.Distance(pivot, desiredPosition);
        float targetDist = baseDist;

        if (Physics.SphereCast(
                pivot,
                collisionRadius,
                dir,
                out RaycastHit hit,
                baseDist + collisionOffset,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            float hitDist = Mathf.Max(hit.distance - collisionOffset, minDistance);
            targetDist = Mathf.Clamp(hitDist, minDistance, baseDist);
        }

        _currentDistance = Mathf.Lerp(_currentDistance, targetDist, 10f * Time.deltaTime);

        Vector3 finalTargetPos = pivot + dir * _currentDistance;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            finalTargetPos,
            ref _positionVelocity,
            positionSmoothTime
        );
    }
}