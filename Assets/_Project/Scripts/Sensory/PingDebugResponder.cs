using UnityEngine;

/// <summary>
/// Тестовая реализация IPingResponder.
/// Повесь на врага или объект, чтобы убедиться, что пинг его "видит".
/// </summary>
public class PingDebugResponder : MonoBehaviour, IPingResponder
{
    [SerializeField] private Color debugColor = Color.cyan;
    [SerializeField] private float debugDuration = 1.5f;

    public void OnPing(Vector3 origin, float radius)
    {
        Debug.DrawLine(origin, transform.position, debugColor, debugDuration);
        Debug.Log($"PingDebugResponder: объект {name} получил пинг из {origin}, радиус {radius}", this);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = debugColor;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}