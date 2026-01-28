using UnityEngine;

/// <summary>
/// Скрипт для управления направляющей линией к брейнроту
/// </summary>
public class Guide : MonoBehaviour
{
    [Header("Префабы")]
    [Tooltip("Префаб брейнрота для спавна")]
    [SerializeField] private GameObject brainrotPrefab;
    
    [Tooltip("Префаб GuidanceLine")]
    [SerializeField] private GameObject guidanceLinePrefab;
    
    [Header("Настройки спавна")]
    [Tooltip("Позиция спавна брейнрота")]
    [SerializeField] private Vector3 brainrotSpawnPosition = new Vector3(-4.581578f, 0f, 15.31983f);
    
    private GameObject spawnedBrainrot;
    private GameObject guidanceLineInstance;
    private GuidanceLine.GuidanceLine guidanceLineScript;
    private BrainrotObject brainrotObject;
    private GameObject tempTargetObject; // Временный объект для позиции панели
    private PlayerCarryController playerCarryController;
    private bool hasSpawnedBrainrot = false;
    
    void Start()
    {
        // Находим PlayerCarryController для проверки состояния переноски
        FindPlayerCarryController();
        
        // Создаем направляющую линию сразу при старте (независимо от баланса)
        CreateGuidanceLine();
    }
    
    void FindPlayerCarryController()
    {
        if (playerCarryController == null)
        {
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
        }
    }
    
    void Update()
    {
        // Проверяем баланс игрока
        if (GameStorage.Instance == null)
            return;
            
        int balance = GameStorage.Instance.GetBalance();
        
        // Если баланс 0 и брейнрот еще не заспавнен - спавним guide брейнрота
        if (balance == 0 && !hasSpawnedBrainrot)
        {
            SpawnBrainrot();
        }
        
        // Если guidanceline создана, обновляем endPos в зависимости от состояния
        if (guidanceLineScript != null)
        {
            UpdateGuidanceLineTarget();
        }
    }
    
    void SpawnBrainrot()
    {
        if (brainrotPrefab == null)
        {
            Debug.LogError("[Guide] Префаб брейнрота не назначен!");
            return;
        }
        
        // Спавним брейнрота
        spawnedBrainrot = Instantiate(brainrotPrefab, brainrotSpawnPosition, Quaternion.identity);
        brainrotObject = spawnedBrainrot.GetComponent<BrainrotObject>();
        
        if (brainrotObject == null)
        {
            Debug.LogError("[Guide] У префаба брейнрота нет компонента BrainrotObject!");
            return;
        }
        
        hasSpawnedBrainrot = true;
        
        // Устанавливаем заспавненного брейнрота как endPoint для guidanceline
        if (guidanceLineScript != null)
        {
            guidanceLineScript.SetEndPoint(spawnedBrainrot.transform);
        }
        
        Debug.Log("[Guide] Брейнрот заспавнен");
    }
    
    void CreateGuidanceLine()
    {
        if (guidanceLinePrefab == null)
        {
            Debug.LogError("[Guide] Префаб GuidanceLine не назначен!");
            return;
        }
        
        // Создаем экземпляр направляющей линии
        guidanceLineInstance = Instantiate(guidanceLinePrefab);
        guidanceLineScript = guidanceLineInstance.GetComponent<GuidanceLine.GuidanceLine>();
        
        if (guidanceLineScript == null)
        {
            Debug.LogError("[Guide] У префаба GuidanceLine нет компонента GuidanceLine!");
            return;
        }
        
        Debug.Log("[Guide] Направляющая линия создана");
    }
    
    void UpdateGuidanceLineTarget()
    {
        if (guidanceLineScript == null)
            return;
        
        // ВАЖНО: Проверяем, идет ли битва с боссом
        // Если битва активна, направляем линию на босса
        if (BattleManager.Instance != null && BattleManager.Instance.IsBattleActive())
        {
            // Находим BossController
            BossController bossController = FindFirstObjectByType<BossController>();
            
            if (bossController != null && bossController.transform != null)
            {
                // Включаем линию, если она была скрыта
                LineRenderer lineRenderer = guidanceLineInstance?.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = true;
                }
                
                // Устанавливаем endPos на босса
                Transform currentEndPoint = guidanceLineScript.GetEndPoint();
                if (currentEndPoint != bossController.transform)
                {
                    guidanceLineScript.SetEndPoint(bossController.transform);
                }
            }
            else
            {
                // Если босс не найден, скрываем линию
                LineRenderer lineRenderer = guidanceLineInstance?.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
            
            // Выходим из метода - во время битвы не используем стандартное поведение
            return;
        }
        
        // Стандартное поведение (когда битва не активна)
        // Проверяем, есть ли брейнрот в руках
        bool hasBrainrotInHands = false;
        
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
        }
        
        if (playerCarryController != null)
        {
            BrainrotObject carriedObject = playerCarryController.GetCurrentCarriedObject();
            hasBrainrotInHands = (carriedObject != null);
        }
        
        // Если в руках есть брейнрот - ведем к ближайшему пустому placement
        if (hasBrainrotInHands)
        {
            PlacementPanel nearestEmptyPanel = FindNearestEmptyPlacement();
            
            if (nearestEmptyPanel != null)
            {
                // Создаем временный объект для позиции панели, если его еще нет
                if (tempTargetObject == null)
                {
                    tempTargetObject = new GameObject("Guide_TempTarget");
                }
                
                // Обновляем позицию временного объекта
                Vector3 panelPosition = nearestEmptyPanel.GetPlacementPosition();
                tempTargetObject.transform.position = panelPosition;
                
                // Устанавливаем endPos на панель
                Transform currentEndPoint = guidanceLineScript.GetEndPoint();
                if (currentEndPoint != tempTargetObject.transform)
                {
                    guidanceLineScript.SetEndPoint(tempTargetObject.transform);
                }
            }
            else
            {
                // Если нет пустых панелей, скрываем линию (но не удаляем)
                if (guidanceLineScript != null)
                {
                    LineRenderer lineRenderer = guidanceLineInstance?.GetComponent<LineRenderer>();
                    if (lineRenderer != null)
                    {
                        lineRenderer.enabled = false;
                    }
                }
            }
        }
        else
        {
            // Если в руках нет брейнрота - ведем к ближайшему брейнроту
            Transform nearestBrainrotTransform = FindNearestBrainrot();
            
            if (nearestBrainrotTransform != null)
            {
                // Включаем линию, если она была скрыта
                LineRenderer lineRenderer = guidanceLineInstance?.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = true;
                }
                
                // Устанавливаем endPos на ближайший брейнрот
                Transform currentEndPoint = guidanceLineScript.GetEndPoint();
                if (currentEndPoint != nearestBrainrotTransform)
                {
                    guidanceLineScript.SetEndPoint(nearestBrainrotTransform);
                }
            }
            else
            {
                // Если нет доступных брейнротов, скрываем линию (но не удаляем)
                LineRenderer lineRenderer = guidanceLineInstance?.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
        }
    }
    
    Transform FindNearestBrainrot()
    {
        // Находим все брейнроты в сцене
        BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        
        if (allBrainrots == null || allBrainrots.Length == 0)
        {
            return null;
        }
        
        // Находим игрока
        Transform playerTransform = null;
        if (playerCarryController != null)
        {
            playerTransform = playerCarryController.GetPlayerTransform();
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        if (playerTransform == null)
        {
            return null;
        }
        
        // Ищем ближайший брейнрот (не в руках, не размещенный)
        float minDistance = float.MaxValue;
        BrainrotObject closest = null;
        
        foreach (BrainrotObject brainrot in allBrainrots)
        {
            // Пропускаем брейнроты, которые уже в руках или размещены
            if (brainrot.IsCarried() || brainrot.IsPlaced())
                continue;
            
            // Вычисляем расстояние до игрока
            float distance = Vector3.Distance(playerTransform.position, brainrot.transform.position);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = brainrot;
            }
        }
        
        return closest?.transform;
    }
    
    PlacementPanel FindNearestEmptyPlacement()
    {
        // Находим все панели размещения в сцене
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        
        if (allPanels == null || allPanels.Length == 0)
        {
            return null;
        }
        
        // Находим игрока
        Transform playerTransform = null;
        if (playerCarryController != null)
        {
            playerTransform = playerCarryController.GetPlayerTransform();
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        if (playerTransform == null)
        {
            return null;
        }
        
        // Ищем ближайшую пустую панель (GetPlacedBrainrot() == null)
        float minDistance = float.MaxValue;
        PlacementPanel closest = null;
        
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null)
                continue;
            
            // Проверяем, пуста ли панель
            BrainrotObject placedBrainrot = panel.GetPlacedBrainrot();
            if (placedBrainrot != null)
                continue; // Панель занята
            
            // Вычисляем расстояние до игрока
            Vector3 panelPosition = panel.GetPlacementPosition();
            float distance = Vector3.Distance(playerTransform.position, panelPosition);
            
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = panel;
            }
        }
        
        return closest;
    }
    
    void OnDestroy()
    {
        // Удаляем guidanceline только при уничтожении Guide
        if (guidanceLineInstance != null)
        {
            Destroy(guidanceLineInstance);
            guidanceLineInstance = null;
            guidanceLineScript = null;
        }
        
        // Удаляем временный объект для панели, если он есть
        if (tempTargetObject != null)
        {
            Destroy(tempTargetObject);
            tempTargetObject = null;
        }
    }
}
