using UnityEngine;

/// <summary>
/// Автоматически создает BoxCollider для земли, чтобы брейнроты спавнились на правильной высоте.
/// Добавьте этот компонент на объект земли.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class GroundCollider : MonoBehaviour
{
    [Header("Настройки коллайдера")]
    [Tooltip("Размер коллайдера по X (ширина)")]
    [SerializeField] private float sizeX = 100f;
    
    [Tooltip("Размер коллайдера по Y (высота коллайдера, не высота земли)")]
    [SerializeField] private float sizeY = 1f;
    
    [Tooltip("Размер коллайдера по Z (глубина)")]
    [SerializeField] private float sizeZ = 100f;
    
    [Tooltip("Центр коллайдера относительно объекта")]
    [SerializeField] private Vector3 center = Vector3.zero;
    
    [Tooltip("Автоматически настроить размер на основе Renderer (если есть)")]
    [SerializeField] private bool autoSizeFromRenderer = true;
    
    private BoxCollider boxCollider;
    
    private void Awake()
    {
        // Получаем или создаем BoxCollider
        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        // Настраиваем коллайдер
        SetupCollider();
    }
    
    /// <summary>
    /// Настраивает BoxCollider
    /// </summary>
    private void SetupCollider()
    {
        if (boxCollider == null) return;
        
        // Если включена автоматическая настройка, пытаемся получить размер из Renderer
        if (autoSizeFromRenderer)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.bounds.size.magnitude > 0.1f)
            {
                Vector3 boundsSize = renderer.bounds.size;
                sizeX = boundsSize.x;
                sizeZ = boundsSize.z;
                sizeY = Mathf.Max(1f, boundsSize.y);
                
                // Центр относительно локальных координат
                center = transform.InverseTransformPoint(renderer.bounds.center);
            }
        }
        
        // Устанавливаем размер и центр
        boxCollider.size = new Vector3(sizeX, sizeY, sizeZ);
        boxCollider.center = center;
        
        // Делаем коллайдер триггером, если нужно (по умолчанию нет)
        // boxCollider.isTrigger = false;
        
        Debug.Log($"[GroundCollider] {gameObject.name}: BoxCollider настроен. Размер: {boxCollider.size}, Центр: {boxCollider.center}");
    }
    
    /// <summary>
    /// Обновляет размер коллайдера вручную
    /// </summary>
    public void UpdateColliderSize(float newSizeX, float newSizeY, float newSizeZ)
    {
        sizeX = newSizeX;
        sizeY = newSizeY;
        sizeZ = newSizeZ;
        
        if (boxCollider != null)
        {
            boxCollider.size = new Vector3(sizeX, sizeY, sizeZ);
        }
    }
    
    /// <summary>
    /// Обновляет центр коллайдера вручную
    /// </summary>
    public void UpdateColliderCenter(Vector3 newCenter)
    {
        center = newCenter;
        
        if (boxCollider != null)
        {
            boxCollider.center = center;
        }
    }
    
#if UNITY_EDITOR
    /// <summary>
    /// Рисует визуализацию коллайдера в редакторе
    /// </summary>
    private void OnDrawGizmos()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }
        
        if (boxCollider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        }
    }
#endif
}
