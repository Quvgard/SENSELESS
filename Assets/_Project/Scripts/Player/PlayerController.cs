using UnityEngine;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerVitality))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Normal,
        Crouch,
        Dash,
        Interact
    }

    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    private PlayerMovement _movement;
    private PlayerVitality _vitality;

    [Header("Interaction")]
    [SerializeField] private float interactRadius = 1.5f;          // радиус поиска объектов
    [SerializeField] private float interactForwardOffset = 1.0f;   // смещение сферы вперёд от игрока
    [SerializeField] private float interactRayDistance = 3.0f;     // дальность Raycast от камеры
    [SerializeField] private LayerMask interactLayers = ~0;        // какие слои считаем интерактивными
    [SerializeField] private float interactLockTime = 0.25f;       // "залипание" в стейте Interact

    [Header("Dash")]
    [SerializeField] private float dashCooldown = 0.5f; // задержка между рывками (в секундах)
    private float _dashCooldownTimer;

    private float _interactTimer;
    private PlayerState _stateBeforeInteract;

    /// <summary>Можно ли сейчас делать рывок (не на кулдауне ли).</summary>
    public bool IsDashOnCooldown => _dashCooldownTimer > 0f;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _vitality = GetComponent<PlayerVitality>();
    }

    private void Update()
    {
        if (InputManager.Instance == null)
            return;

        // --- Обновляем кулдаун рывка ---
        if (_dashCooldownTimer > 0f)
            _dashCooldownTimer -= Time.deltaTime;

        var im = InputManager.Instance;

        Vector2 moveInput = im.Move;
        Vector2 lookInput = im.Look;

        bool sprintHeld = im.SprintHeld;
        bool crouchPressed = im.CrouchPressedThisFrame;
        bool dashPressed = im.DashPressedThisFrame;
        bool interactPressed = im.InteractPressedThisFrame;

        bool hasMoveInput = moveInput.sqrMagnitude > 0.01f;

        switch (CurrentState)
        {
            case PlayerState.Normal:
                {
                    // --- Бег: только если есть стамина и есть движение ---
                    bool canRun = sprintHeld && hasMoveInput && _vitality.CanRun;
                    _movement.HandleMovement(moveInput, lookInput, canRun);

                    if (canRun)
                        _vitality.ConsumeRun(Time.deltaTime);

                    if (crouchPressed)
                    {
                        EnterCrouch();
                    }
                    else if (dashPressed && hasMoveInput && TryStartDash(fromCrouch: false, moveInput))
                    {
                        CurrentState = PlayerState.Dash;
                    }
                    else if (interactPressed)
                    {
                        StartInteract();
                    }

                    break;
                }

            case PlayerState.Crouch:
                {
                    _movement.HandleCrouch(moveInput);

                    if (crouchPressed)
                    {
                        ExitCrouch();
                    }
                    else if (dashPressed && hasMoveInput && TryStartDash(fromCrouch: true, moveInput))
                    {
                        CurrentState = PlayerState.Dash;
                    }
                    else if (interactPressed)
                    {
                        StartInteract();
                    }

                    break;
                }

            case PlayerState.Dash:
                {
                    bool finished = _movement.UpdateDash(Time.deltaTime);
                    if (finished)
                    {
                        CurrentState = _movement.DashFromCrouch ? PlayerState.Crouch : PlayerState.Normal;
                    }
                    break;
                }

            case PlayerState.Interact:
                {
                    // Во время взаимодействия не двигаемся
                    _movement.HandleMovement(Vector2.zero, Vector2.zero, false);

                    _interactTimer -= Time.deltaTime;
                    if (_interactTimer <= 0f)
                    {
                        CurrentState = _stateBeforeInteract;
                    }
                    break;
                }
        }
    }

    private void EnterCrouch()
    {
        CurrentState = PlayerState.Crouch;
        _movement.SetCrouch(true);
    }

    private void ExitCrouch()
    {
        _movement.SetCrouch(false);
        CurrentState = PlayerState.Normal;
    }

    /// <summary>
    /// Пытаемся начать рывок: проверяем кулдаун и стамину.
    /// </summary>
    private bool TryStartDash(bool fromCrouch, Vector2 moveInput)
    {
        // 1. Проверяем кулдаун
        if (_dashCooldownTimer > 0f)
            return false;

        // 2. Проверяем стамину
        if (!_vitality.TryConsumeDash())
            return false;

        // 3. Пытаемся запустить сам рывок
        bool started = _movement.BeginDash(moveInput, fromCrouch);
        if (!started)
        {
            return false;
        }

        // 4. Если рывок успешно начат — запускаем кулдаун
        _dashCooldownTimer = dashCooldown;

        return true;
    }

    // -------------------- ВЗАИМОДЕЙСТВИЕ + ПИНГ --------------------

    private void StartInteract()
    {
        _stateBeforeInteract = CurrentState;
        CurrentState = PlayerState.Interact;
        _interactTimer = interactLockTime;

        bool interacted = TryInteract();
        if (!interacted && PingManager.Instance != null)
        {
            // Если перед игроком нет подходящих объектов — запускаем эхолокацию
            PingManager.Instance.EmitPingFrom(transform);
        }
    }

    /// <summary>
    /// Пытаемся найти лучший IInteractable перед игроком и взаимодействовать с ним.
    /// Возвращает true, если что-то нашли.
    /// </summary>
    private bool TryInteract()
    {
        IInteractable best = null;

        // ---------- Raycast от камеры вперёд ----------
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    interactRayDistance,
                    interactLayers,
                    QueryTriggerInteraction.Collide))
            {
                IInteractable interactable =
                    hit.collider.GetComponent<IInteractable>() ??
                    hit.collider.GetComponentInParent<IInteractable>();

                if (interactable != null)
                {
                    best = interactable;
                }
            }
        }

        // ---------- OverlapSphere перед игроком + выбор ближайшего ----------
        if (best == null)
        {
            Vector3 origin = transform.position + transform.forward * interactForwardOffset;

            Collider[] hits = Physics.OverlapSphere(
                origin,
                interactRadius,
                interactLayers,
                QueryTriggerInteraction.Collide
            );

            if (hits.Length == 0)
            {
                return false;
            }

            float closestDistSqr = float.MaxValue;

            foreach (Collider hit in hits)
            {
                if (hit == null)
                    continue;

                IInteractable interactable =
                    hit.GetComponent<IInteractable>() ??
                    hit.GetComponentInParent<IInteractable>();

                if (interactable == null)
                    continue;

                float distSqr = (hit.transform.position - origin).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    best = interactable;
                }
            }

            if (best == null)
            {
                return false;
            }
        }

        Debug.Log($"Взаимодействуем с: {best.GetDescription()}");
        best.Interact(this);
        return true;
    }

    public void SetState(PlayerState newState)
    {
        CurrentState = newState;
    }

    private void OnDrawGizmosSelected()
    {
        // Сфера взаимодействия перед игроком
        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position + transform.forward * interactForwardOffset;
        Gizmos.DrawWireSphere(origin, interactRadius);
    }
}