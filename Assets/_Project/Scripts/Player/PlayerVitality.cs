using UnityEngine;

/// <summary>
/// Жизненные параметры игрока:
/// - Blood (здоровье),
/// - Breath (выносливость / стамина),
/// - Mana (чакра).
///
/// Сейчас активно используется только Breath:
/// - Бег и рывок тратят Breath.
/// - Если Breath кончился, игрок не может бежать и делать рывок.
/// - Восстановление Breath начинается с задержкой, особенно если ушли в 0.
/// </summary>
public class PlayerVitality : MonoBehaviour
{
    // ---------------- BLOOD (HP) ----------------

    [Header("Blood (Health)")]
    [SerializeField] private float maxBlood = 100f;

    [SerializeField, Tooltip("Текущее здоровье (для отладки)")]
    private float currentBloodDebug;

    public float MaxBlood => maxBlood;

    public float CurrentBlood
    {
        get => currentBloodDebug;
        private set => currentBloodDebug = Mathf.Clamp(value, 0f, maxBlood);
    }

    // ---------------- MANA (CHAKRA) ----------------

    [Header("Mana (Chakra)")]
    [SerializeField] private float maxMana = 50f;

    [SerializeField, Tooltip("Текущее значение чакры (для отладки)")]
    private float currentManaDebug;

    public float MaxMana => maxMana;

    public float CurrentMana
    {
        get => currentManaDebug;
        private set => currentManaDebug = Mathf.Clamp(value, 0f, maxMana);
    }

    // ---------------- BREATH (STAMINA) ----------------

    [Header("Breath (Stamina)")]
    [SerializeField] private float maxBreath = 100f;
    [SerializeField] private float breathRegenPerSecond = 15f;
    [SerializeField] private float runBreathDrainPerSecond = 25f;
    [SerializeField] private float dashBreathCost = 30f;

    [Tooltip("Задержка перед началом восстановления дыхания после любой траты.")]
    [SerializeField] private float breathRegenDelay = 1.0f;

    [Tooltip("Дополнительная задержка, если дыхание опустилось до нуля (полное истощение).")]
    [SerializeField] private float breathExhaustedExtraDelay = 1.0f;

    [SerializeField, Tooltip("Текущее дыхание (для отладки)")]
    private float currentBreathDebug;

    /// <summary>Текущее дыхание (стамина).</summary>
    public float CurrentBreath
    {
        get => currentBreathDebug;
        private set => currentBreathDebug = Mathf.Clamp(value, 0f, maxBreath);
    }

    /// <summary>Можно ли сейчас бежать.</summary>
    public bool CanRun => CurrentBreath > 1f;   // чуть выше нуля, чтобы не дёргалось на границе

    /// <summary>Можно ли сейчас делать рывок.</summary>
    public bool CanDash => CurrentBreath >= dashBreathCost && !IsBreathExhausted;

    /// <summary>Состояние "истощён": дыхание на нуле и ещё идёт задержка до регена.</summary>
    public bool IsBreathExhausted => CurrentBreath <= 0.01f && _breathRegenTimer > 0f;

    // Внутренний таймер до начала регенерации дыхания
    private float _breathRegenTimer;

    private void Awake()
    {
        // Заполняем все ресурсы до максимума
        CurrentBlood = maxBlood;
        CurrentMana = maxMana;
        CurrentBreath = maxBreath;
        _breathRegenTimer = 0f;
    }

    private void Update()
    {
        UpdateBreath(Time.deltaTime);
        // Позже сюда добавим реген/эффекты для Blood и Mana при необходимости
    }

    // ---------------- ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ ДЫХАНИЯ ----------------

    /// <summary>
    /// Тратим дыхание за бег (вызывается каждый кадр, пока бежим).
    /// </summary>
    public void ConsumeRun(float deltaTime)
    {
        if (CurrentBreath <= 0f)
            return;

        float before = CurrentBreath;
        CurrentBreath -= runBreathDrainPerSecond * deltaTime;

        StartBreathRegenDelay(before);
    }

    /// <summary>
    /// Пытаемся потратить дыхание на рывок. Возвращает true, если получилось.
    /// </summary>
    public bool TryConsumeDash()
    {
        if (!CanDash)
            return false;

        float before = CurrentBreath;
        CurrentBreath -= dashBreathCost;

        StartBreathRegenDelay(before);
        return true;
    }

    // ---------------- ВНУТРЕННЯЯ ЛОГИКА BREATH ----------------

    /// <summary>
    /// Запускает таймер задержки регенерации, с учётом того, опустились ли мы до нуля.
    /// </summary>
    private void StartBreathRegenDelay(float valueBeforeConsume)
    {
        // Базовая задержка после любой траты
        _breathRegenTimer = breathRegenDelay;

        // Если до траты было > 0, а стало 0 или меньше — полное истощение
        if (valueBeforeConsume > 0f && CurrentBreath <= 0f)
        {
            _breathRegenTimer += breathExhaustedExtraDelay;
        }
    }

    private void UpdateBreath(float deltaTime)
    {
        // Уже полный запас — ничего не делаем
        if (CurrentBreath >= maxBreath)
        {
            CurrentBreath = maxBreath;
            _breathRegenTimer = 0f;
            return;
        }

        // Если ещё идёт задержка — тикаем таймер
        if (_breathRegenTimer > 0f)
        {
            _breathRegenTimer -= deltaTime;
            return;
        }

        // Восстановление дыхания
        if (CurrentBreath < maxBreath)
        {
            CurrentBreath += breathRegenPerSecond * deltaTime;
            if (CurrentBreath > maxBreath)
                CurrentBreath = maxBreath;
        }
    }

    // ---------------- ЗАГЛУШКИ ДЛЯ BLOOD / MANA (на будущее) ----------------

    /// <summary>Наносим урон по здоровью.</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentBlood -= amount;
        // TODO: смерть, ранение и т.п. позже
    }

    /// <summary>Восстанавливаем здоровье.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentBlood += amount;
    }

    /// <summary>Пробуем потратить ману. Возвращает true, если удалось.</summary>
    public bool TryConsumeMana(float amount)
    {
        if (amount <= 0f)
            return true;

        if (CurrentMana < amount)
            return false;

        CurrentMana -= amount;
        return true;
    }

    /// <summary>Восстанавливаем ману.</summary>
    public void RestoreMana(float amount)
    {
        if (amount <= 0f)
            return;

        CurrentMana += amount;
    }
}