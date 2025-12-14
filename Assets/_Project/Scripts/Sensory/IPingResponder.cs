using UnityEngine;

/// <summary>
/// Интерфейс для объектов, которые реагируют на эхолокационный пинг.
/// Например, враги могут кратко "проявляться".
/// </summary>
public interface IPingResponder
{
    /// <summary>
    /// Вызывается, когда из точки origin был выпущен пинг радиусом radius.
    /// </summary>
    void OnPing(Vector3 origin, float radius);
}