using UnityEngine;

/// <summary>
/// Зона сражения с боссом.
/// Определяет границы арены и точки спавна.
/// </summary>
public class BattleZone : MonoBehaviour
{
    [Header("Spawn Points")]
    [Tooltip("Точка спавна игрока в зоне сражения")]
    [SerializeField] private Transform playerSpawnPoint;
    
    [Tooltip("Точка спавна босса")]
    [SerializeField] private Transform bossSpawnPoint;
    
    [Tooltip("Точки спавна союзников (ботов)")]
    [SerializeField] private Transform[] allySpawnPoints;
    
    [Header("Zone Boundaries")]
    [Tooltip("Границы зоны сражения (BoxCollider для определения границ)")]
    [SerializeField] private Collider zoneCollider;
    
    [Header("References")]
    [Tooltip("Ссылка на BattleManager")]
    [SerializeField] private BattleManager battleManager;
    
    private bool isBattleActive = false;
    private BrainrotObject currentBrainrot;
    
    /// <summary>
    /// Получает позицию спавна игрока
    /// </summary>
    public Vector3 GetPlayerSpawnPosition()
    {
        if (playerSpawnPoint != null)
        {
            return playerSpawnPoint.position;
        }
        
        // Если точка спавна не назначена, используем позицию зоны
        return transform.position;
    }
    
    /// <summary>
    /// Получает поворот спавна игрока
    /// </summary>
    public Quaternion GetPlayerSpawnRotation()
    {
        if (playerSpawnPoint != null)
        {
            return playerSpawnPoint.rotation;
        }
        
        return transform.rotation;
    }
    
    /// <summary>
    /// Получает позицию спавна босса
    /// </summary>
    public Vector3 GetBossSpawnPosition()
    {
        if (bossSpawnPoint != null)
        {
            return bossSpawnPoint.position;
        }
        
        // Если точка спавна не назначена, используем позицию зоны с небольшим смещением
        return transform.position + Vector3.forward * 5f;
    }
    
    /// <summary>
    /// Получает поворот спавна босса
    /// </summary>
    public Quaternion GetBossSpawnRotation()
    {
        if (bossSpawnPoint != null)
        {
            return bossSpawnPoint.rotation;
        }
        
        return Quaternion.LookRotation(-Vector3.forward);
    }
    
    /// <summary>
    /// Получает случайную точку спавна союзника
    /// </summary>
    public Vector3 GetRandomAllySpawnPosition()
    {
        if (allySpawnPoints != null && allySpawnPoints.Length > 0)
        {
            Transform randomPoint = allySpawnPoints[Random.Range(0, allySpawnPoints.Length)];
            if (randomPoint != null)
            {
                return randomPoint.position;
            }
        }
        
        // Если точек спавна нет, генерируем случайную позицию вокруг игрока
        Vector3 playerPos = GetPlayerSpawnPosition();
        Vector2 randomCircle = Random.insideUnitCircle * 3f;
        return playerPos + new Vector3(randomCircle.x, 0f, randomCircle.y);
    }
    
    /// <summary>
    /// Точка спавна союзника по индексу (0..4). Y подменяется на groundY (FightZoneGround).
    /// </summary>
    public Vector3 GetAllySpawnPositionForIndex(int index, float groundY)
    {
        Vector3 pos;
        if (allySpawnPoints != null && index >= 0 && index < allySpawnPoints.Length && allySpawnPoints[index] != null)
        {
            pos = allySpawnPoints[index].position;
        }
        else
        {
            Vector3 playerPos = GetPlayerSpawnPosition();
            float angle = index * (360f / 5f) * Mathf.Deg2Rad;
            float r = 2.5f;
            pos = playerPos + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
        }
        pos.y = groundY;
        return pos;
    }
    
    /// <summary>
    /// Проверяет, находится ли позиция внутри зоны сражения
    /// </summary>
    public bool IsPositionInZone(Vector3 position)
    {
        if (zoneCollider != null)
        {
            return zoneCollider.bounds.Contains(position);
        }
        
        // Если коллайдер не назначен, считаем что позиция всегда в зоне
        return true;
    }
    
    /// <summary>
    /// Начинает бой
    /// </summary>
    public void StartBattle(BrainrotObject brainrotObject)
    {
        if (brainrotObject == null)
        {
            Debug.LogError("[BattleZone] BrainrotObject равен null!");
            return;
        }
        
        if (isBattleActive)
        {
            Debug.LogWarning("[BattleZone] Бой уже активен!");
            return;
        }
        
        // ВАЖНО: Находим BattleManager если он не назначен
        if (battleManager == null)
        {
            battleManager = FindFirstObjectByType<BattleManager>();
            if (battleManager == null)
            {
                Debug.LogError("[BattleZone] BattleManager не найден в сцене!");
                return;
            }
        }
        
        isBattleActive = true;
        currentBrainrot = brainrotObject;
        
        Debug.Log($"[BattleZone] Начинается бой с брейнротом: {brainrotObject.GetObjectName()}");
        
        // #region agent log
        try { 
            Transform playerTransform = battleManager != null ? battleManager.GetPlayerTransform() : null;
            System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BattleZone.cs:149\",\"message\":\"Before calling BattleManager.StartBattle\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); 
        } catch {}
        // #endregion
        
        // Уведомляем BattleManager о начале боя
        if (battleManager != null)
        {
            battleManager.StartBattle(brainrotObject, this);
            
            // #region agent log
            try { 
                Transform playerTransform = battleManager.GetPlayerTransform();
                System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"BattleZone.cs:157\",\"message\":\"After calling BattleManager.StartBattle\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); 
            } catch {}
            // #endregion
        }
        else
        {
            Debug.LogError("[BattleZone] BattleManager равен null при начале боя!");
        }
    }
    
    /// <summary>
    /// Заканчивает бой
    /// </summary>
    public void EndBattle()
    {
        if (!isBattleActive)
        {
            return;
        }
        
        isBattleActive = false;
        currentBrainrot = null;
        
        // Уведомляем BattleManager о конце боя
        if (battleManager != null)
        {
            battleManager.EndBattle();
        }
    }
    
    /// <summary>
    /// Проверяет, активен ли бой
    /// </summary>
    public bool IsBattleActive()
    {
        return isBattleActive;
    }
    
    /// <summary>
    /// Получает текущий брейнрот (босс)
    /// </summary>
    public BrainrotObject GetCurrentBrainrot()
    {
        return currentBrainrot;
    }
    
    private void Awake()
    {
        // Автоматически находим BattleManager если не назначен
        if (battleManager == null)
        {
            battleManager = FindFirstObjectByType<BattleManager>();
        }
        
        // Автоматически находим коллайдер если не назначен
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }
    }
}
