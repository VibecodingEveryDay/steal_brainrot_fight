using UnityEngine;

/// <summary>
/// Скрипт для 3D объектов-блокировок безопасных зон
/// Блокирует проход через объект, пока зона не куплена
/// После покупки деактивирует Collider, делая объект проходимым
/// </summary>
[RequireComponent(typeof(Collider))]
public class SafeZone : MonoBehaviour
{
    [Header("Zone Settings")]
    [Tooltip("Номер безопасной зоны (1-4). Должен соответствовать номеру зоны в SafeZonesManager")]
    [SerializeField] private int zoneNumber = 1;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    private GameStorage gameStorage;
    private Collider zoneCollider;
    
    private void Awake()
    {
        // Получаем компонент Collider
        zoneCollider = GetComponent<Collider>();
        
        if (zoneCollider == null)
        {
            Debug.LogError($"[SafeZone] Collider не найден на объекте {gameObject.name}!");
        }
        
        // Проверяем корректность номера зоны
        if (zoneNumber < 1 || zoneNumber > 4)
        {
            Debug.LogWarning($"[SafeZone] Некорректный номер зоны: {zoneNumber} на объекте {gameObject.name}. Допустимые значения: 1-4");
        }
    }
    
    private void Start()
    {
        // Получаем ссылку на GameStorage
        gameStorage = GameStorage.Instance;
        
        if (gameStorage == null)
        {
            Debug.LogError($"[SafeZone] GameStorage.Instance не найден на объекте {gameObject.name}!");
            return;
        }
        
        // Проверяем статус покупки зоны
        CheckPurchaseStatus();
    }
    
    /// <summary>
    /// Проверяет статус покупки зоны и деактивирует Collider, если зона куплена
    /// </summary>
    private void CheckPurchaseStatus()
    {
        if (gameStorage == null || zoneCollider == null) return;
        
        bool isPurchased = gameStorage.IsSafeZonePurchased(zoneNumber);
        
        if (isPurchased)
        {
            // Если зона куплена, деактивируем Collider, чтобы игрок мог пройти
            zoneCollider.enabled = false;
            
            if (debug)
            {
                Debug.Log($"[SafeZone] Зона {zoneNumber} на объекте {gameObject.name} куплена. Collider деактивирован.");
            }
        }
        else
        {
            // Если зона не куплена, Collider остается активным (блокирует проход)
            zoneCollider.enabled = true;
            
            if (debug)
            {
                Debug.Log($"[SafeZone] Зона {zoneNumber} на объекте {gameObject.name} не куплена. Collider активен (блокирует проход).");
            }
        }
    }
    
    /// <summary>
    /// Публичный метод для обновления статуса зоны (можно вызвать извне при покупке)
    /// </summary>
    public void UpdatePurchaseStatus()
    {
        CheckPurchaseStatus();
    }
}
