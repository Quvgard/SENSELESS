using UnityEngine;

/// <summary>
/// Глобальный менеджер эхолокации (пинга).
/// Логика:
/// - Игрок вызывает EmitPingFrom(себя).
/// - Менеджер находит точку удара (пол или стена).
/// - Спавнит VFX (опционально).
/// - Делает OverlapSphere и вызывает IPingResponder у найденных объектов.
/// </summary>
public class PingManager : MonoBehaviour
{
    public static PingManager Instance { get; private set; }

    [Header("Echolocation (Ping)")]
    [SerializeField] private float pingRadius = 6f;
    [SerializeField] private LayerMask pingDetectionLayers = ~0;   // враги, ловушки и т.п.
    [SerializeField] private LayerMask pingEnvironmentLayers = ~0; // пол/стены, куда бьём мечом
    [SerializeField] private GameObject pingVfxPrefab;              // волна/эффект (опционально)
    [SerializeField] private float pingVfxLifetime = 2f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// Пинг из позиции и ориентации источника (например, игрока).
    /// </summary>
    public void EmitPingFrom(Transform source)
    {
        if (source == null)
            return;

        Vector3 origin = ComputePingOrigin(source);
        EmitPing(origin);
    }

    /// <summary>
    /// Пинг из заранее известной точки.
    /// </summary>
    public void EmitPing(Vector3 origin)
    {
        Debug.Log($"PING: эхолокация из точки {origin}, радиус {pingRadius}", this);

        // Дебаг-луч для удобства
        Debug.DrawRay(origin, Vector3.up * 0.5f, Color.cyan, 1.5f);

        // VFX волны (если есть)
        if (pingVfxPrefab != null)
        {
            GameObject vfx = Instantiate(pingVfxPrefab, origin, Quaternion.identity);
            if (pingVfxLifetime > 0f)
                Destroy(vfx, pingVfxLifetime);
        }

        // Поиск целей
        Collider[] hits = Physics.OverlapSphere(
            origin,
            pingRadius,
            pingDetectionLayers,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider col in hits)
        {
            if (col == null)
                continue;

            IPingResponder responder = col.GetComponentInParent<IPingResponder>();
            if (responder != null)
            {
                responder.OnPing(origin, pingRadius);
            }
        }
    }

    /// <summary>
    /// Находим точку удара меча (пол либо стена перед персонажем).
    /// </summary>
    private Vector3 ComputePingOrigin(Transform source)
    {
        Vector3 pingOrigin = source.position;

        // 1) Луч вниз — удар об пол
        Vector3 downOrigin = source.position + Vector3.up * 1.0f;
        if (Physics.Raycast(
                downOrigin,
                Vector3.down,
                out RaycastHit groundHit,
                3f,
                pingEnvironmentLayers,
                QueryTriggerInteraction.Ignore))
        {
            return groundHit.point;
        }

        // 2) Если пола нет, пробуем стену перед собой
        Vector3 chestOrigin = source.position + Vector3.up * 1.2f;
        if (Physics.Raycast(
                chestOrigin,
                source.forward,
                out RaycastHit wallHit,
                3f,
                pingEnvironmentLayers,
                QueryTriggerInteraction.Ignore))
        {
            return wallHit.point;
        }

        // 3) Если ничего не нашли — пингуем просто из позиции персонажа
        return pingOrigin;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
#endif
}