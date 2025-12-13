using UnityEngine;

/// <summary>
/// Интерфейс для объектов, с которыми игрок может взаимодействовать.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Вызывается, когда игрок взаимодействует с объектом.
    /// </summary>
    /// <param name="pc">Контроллер игрока.</param>
    void Interact(PlayerController pc);

    /// <summary>
    /// Текст подсказки (например: "Нажмите E, чтобы открыть дверь").
    /// </summary>
    string GetDescription();
}