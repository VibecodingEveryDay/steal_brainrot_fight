using UnityEngine;

/// <summary>
/// Компонент для управления движением стены и обработки коллизий
/// </summary>
public class WallMovement : MonoBehaviour
{
    private float speed;
    private float endPosZ;
    private WallSpawner spawner;
    private bool isInitialized = false;
    private bool hasCollided = false; // Флаг для предотвращения множественных срабатываний
    
    // Для проверки коллизии с игроком
    private Transform playerTransform;
    
    // Параметры проверки коллизии
    private const float minX = -142f;
    private const float maxX = 50.41f;
    private const float minY = -1f;
    [SerializeField] private float zTolerance = 3f; // Допустимая разница по Z для обнаружения коллизии (настраивается в Inspector)
    
    [Header("Debug")]
    [SerializeField] private bool debugCollision = false; // Включить отладку коллизий
    
    /// <summary>
    /// Инициализирует компонент движения стены
    /// </summary>
    public void Initialize(float wallSpeed, float endPositionZ, WallSpawner wallSpawner, float collisionZTolerance = 3f)
    {
        speed = wallSpeed;
        endPosZ = endPositionZ;
        spawner = wallSpawner;
        zTolerance = collisionZTolerance; // Устанавливаем допуск из параметра
        isInitialized = true;
        
        // Находим игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
        
        // Удаляем все коллайдеры у стены
        RemoveColliders();
    }
    
    /// <summary>
    /// Удаляет все коллайдеры у стены и её дочерних объектов
    /// </summary>
    private void RemoveColliders()
    {
        // Удаляем коллайдеры на корневом объекте
        Collider[] rootColliders = GetComponents<Collider>();
        foreach (Collider col in rootColliders)
        {
            Destroy(col);
        }
        
        // Удаляем коллайдеры в дочерних объектах
        Collider[] childColliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in childColliders)
        {
            Destroy(col);
        }
    }
    
    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }
        
        // ВАЖНО: Проверяем коллизию ДО движения стены, чтобы избежать ситуации,
        // когда стена уже прошла игрока, но проверка все еще срабатывает
        if (!hasCollided && playerTransform != null)
        {
            CheckPlayerCollision();
        }
        
        // Движемся по оси Z (после проверки коллизии)
        transform.position += Vector3.back * speed * Time.deltaTime;
        
        // Проверяем, достигли ли конечной позиции
        if (transform.position.z <= endPosZ)
        {
            // Уничтожаем стену
            if (spawner != null)
            {
                spawner.RemoveWall(gameObject);
            }
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Проверяет коллизию с игроком по заданным условиям:
    /// X: от -142 до 50.41
    /// Y: > -1
    /// Z: позиция игрока и стены совпадают (с учетом движения стены)
    /// </summary>
    private void CheckPlayerCollision()
    {
        if (playerTransform == null)
        {
            return;
        }
        
        Vector3 playerPos = playerTransform.position;
        Vector3 wallPos = transform.position;
        
        // АЛЬТЕРНАТИВНЫЙ ПОДХОД 1: Строгая проверка Z С УЧЕТОМ ДВИЖЕНИЯ
        // Стена движется назад (от большего Z к меньшему)
        // Вычисляем, где будет стена в следующем кадре (с учетом скорости)
        float nextWallZ = wallPos.z - (speed * Time.deltaTime);
        
        // Если игрок УЖЕ за текущей позицией стены - столкновения нет
        // ИЛИ если игрок будет за следующей позицией стены - столкновения нет
        if (playerPos.z > wallPos.z)
        {
            // Игрок уже за стеной (Z игрока > Z стены) - столкновения НЕТ
            if (debugCollision)
            {
                Debug.Log($"[WallMovement] АЛЬТ ПОДХОД: Игрок ЗА стеной! Z игрока: {playerPos.z:F2} > Z стены: {wallPos.z:F2}, столкновение НЕ происходит");
            }
            return;
        }
        
        // Если игрок будет за следующей позицией стены (стена его уже обгонит), тоже не считаем столкновение
        // Но только если разница достаточно большая (стена точно пройдет мимо)
        if (playerPos.z > nextWallZ && (playerPos.z - nextWallZ) > 0.1f)
        {
            // Стена пройдет мимо игрока в следующем кадре, и игрок будет за ней
            if (debugCollision)
            {
                Debug.Log($"[WallMovement] АЛЬТ ПОДХОД: Стена пройдет мимо игрока! Z игрока: {playerPos.z:F2} > Следующая Z стены: {nextWallZ:F2}, столкновение НЕ происходит");
            }
            return;
        }
        
        // Проверка X: игрок должен быть в диапазоне от -142 до 50.41
        bool checkX = playerPos.x >= minX && playerPos.x <= maxX;
        
        // Проверка Y: игрок должен быть выше -1
        bool checkY = playerPos.y > minY;
        
        if (!checkX || !checkY)
        {
            return; // Если X или Y не подходят, дальше не проверяем
        }
        
        // Проверка Z: игрок впереди стены или на линии (playerPos.z <= wallPos.z)
        // Проверяем, что игрок не слишком далеко впереди стены (в пределах zTolerance)
        float zDifference = playerPos.z - wallPos.z;
        
        // СТРОГАЯ проверка: игрок должен быть ВПЕРЕДИ стены (zDifference < 0, НЕ <= 0)
        // И в пределах zTolerance
        // Исключаем случай, когда игрок на одной линии со стеной (zDifference == 0)
        // Столкновение только если игрок строго впереди стены на расстояние <= zTolerance
        bool checkZ = zDifference < 0 && zDifference >= -zTolerance;
        
        if (debugCollision)
        {
            Debug.Log($"[WallMovement] Проверка Z: игрок впереди стены, разница={zDifference:F2}, допуск={zTolerance}, результат={checkZ}");
            Debug.Log($"[WallMovement] Игрок: Z={playerPos.z:F2}, Стена: Z={wallPos.z:F2}, Следующая Z стены: {nextWallZ:F2}, X={checkX}, Y={checkY}");
        }
        
        // Если все условия выполнены, коллизия обнаружена
        if (checkZ)
        {
            if (debugCollision)
            {
                Debug.Log($"[WallMovement] КОЛЛИЗИЯ ОБНАРУЖЕНА! Игрок: {playerPos}, Стена: {wallPos}, Z разница: {zDifference:F2}");
            }
            OnPlayerCollision();
        }
    }
    
    /// <summary>
    /// Обрабатывает коллизию с игроком
    /// </summary>
    private void OnPlayerCollision()
    {
        if (hasCollided)
        {
            return; // Уже обработали коллизию
        }
        
        // Дополнительная защита: проверяем Z координату еще раз перед телепортом
        if (playerTransform != null)
        {
            float playerZ = playerTransform.position.z;
            float wallZ = transform.position.z;
            
            // Если игрок за стеной (Z игрока > Z стены), НЕ телепортируем
            if (playerZ > wallZ)
            {
                if (debugCollision)
                {
                    Debug.LogWarning($"[WallMovement] ЗАЩИТА: Игрок за стеной при вызове OnPlayerCollision! Z игрока: {playerZ:F2} > Z стены: {wallZ:F2}, телепорт ОТМЕНЁН");
                }
                return;
            }
        }
        
        // Финальная проверка перед установкой флага: еще раз проверяем Z координату
        if (playerTransform != null)
        {
            float finalPlayerZ = playerTransform.position.z;
            float finalWallZ = transform.position.z;
            
            // Если игрок за стеной (Z игрока > Z стены), НЕ телепортируем
            if (finalPlayerZ > finalWallZ)
            {
                if (debugCollision)
                {
                    Debug.LogWarning($"[WallMovement] ФИНАЛЬНАЯ ЗАЩИТА: Игрок за стеной! Z игрока: {finalPlayerZ:F2} > Z стены: {finalWallZ:F2}, телепорт ОТМЕНЁН");
                }
                return;
            }
        }
        
        hasCollided = true;
        
        // Уведомляем спавнер о коллизии, передавая ссылку на эту стену
        if (spawner != null)
        {
            spawner.OnWallCollisionWithPlayer(gameObject);
        }
    }
}
