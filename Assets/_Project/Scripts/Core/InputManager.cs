using UnityEngine;
using UnityEngine.InputSystem; // Новый Input System

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    private SenselessControls _controls;

    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }

    public bool SprintHeld { get; private set; }
    public bool SprintPressedThisFrame { get; private set; }

    public bool CrouchHeld { get; private set; }
    public bool CrouchPressedThisFrame { get; private set; }

    public bool DashHeld { get; private set; }
    public bool DashPressedThisFrame { get; private set; }

    public bool InteractHeld { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureControlsCreated();
    }

    private void EnsureControlsCreated()
    {
        if (_controls == null)
            _controls = new SenselessControls();
    }

    private void OnEnable()
    {
        EnsureControlsCreated();
        _controls.Enable();
    }

    private void OnDisable()
    {
        if (_controls != null)
            _controls.Disable();
    }

    private void Update()
    {
        if (_controls == null)
            return;

        var player = _controls.Player;

        // --- Векторы ---
        Vector2 rawMove = player.Move.ReadValue<Vector2>();
        if (rawMove.sqrMagnitude > 1f)
            rawMove = rawMove.normalized;
        Move = rawMove;

        Look = player.Look.ReadValue<Vector2>();

        // --- Кнопки ---
        SprintHeld = player.Sprint.IsPressed();
        SprintPressedThisFrame = player.Sprint.WasPressedThisFrame();

        CrouchHeld = player.Crouch.IsPressed();
        CrouchPressedThisFrame = player.Crouch.WasPressedThisFrame();

        DashHeld = player.Dash.IsPressed();
        DashPressedThisFrame = player.Dash.WasPressedThisFrame();

        InteractHeld = player.Interact.IsPressed();
        InteractPressedThisFrame = player.Interact.WasPressedThisFrame();
    }
}