using UnityEngine;

/// <summary>
/// Скрипт для управления направляющей линией к брейнроту
/// </summary>
public class Guide : MonoBehaviour
{
    [Header("Префабы")]
    [Tooltip("Префаб GuidanceLine")]
    [SerializeField] private GameObject guidanceLinePrefab;
    
    [Header("Цели")]
    [Tooltip("Transform кнопки спавна брейнрота. Если на карте нет unfought брейнротов — линия ведёт на кнопку.")]
    [SerializeField] private Transform spawnBrainrotButtonTransform;
    
    private GameObject guidanceLineInstance;
    private GuidanceLine.GuidanceLine guidanceLineScript;
    private GameObject tempTargetObject; // Временный объект для позиции панели
    private PlayerCarryController playerCarryController;
    
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
        if (guidanceLineScript != null)
        {
            UpdateGuidanceLineTarget();
        }
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
        
        // Если в руках есть брейнрот — ведём к ближайшему пустому placement, иначе к любому placement
        if (hasBrainrotInHands)
        {
            PlacementPanel targetPanel = FindNearestEmptyPlacement();
            if (targetPanel == null)
                targetPanel = FindNearestPlacement();
            
            if (targetPanel != null)
            {
                if (tempTargetObject == null)
                    tempTargetObject = new GameObject("Guide_TempTarget");
                tempTargetObject.transform.position = targetPanel.GetPlacementPosition();
                SetLineEnabled(true);
                Transform currentEndPoint = guidanceLineScript.GetEndPoint();
                if (currentEndPoint != tempTargetObject.transform)
                    guidanceLineScript.SetEndPoint(tempTargetObject.transform);
            }
            else
            {
                SetLineEnabled(false);
            }
        }
        else
        {
            // В руках нет брейнрота: ведём на unfought брейнрота или на кнопку спавна
            Transform target = FindNearestUnfoughtBrainrot();
            if (target == null && spawnBrainrotButtonTransform != null)
                target = spawnBrainrotButtonTransform;
            
            if (target != null)
            {
                SetLineEnabled(true);
                Transform currentEndPoint = guidanceLineScript.GetEndPoint();
                if (currentEndPoint != target)
                    guidanceLineScript.SetEndPoint(target);
            }
            else
            {
                SetLineEnabled(false);
            }
        }
    }
    
    void SetLineEnabled(bool enabled)
    {
        LineRenderer lineRenderer = guidanceLineInstance?.GetComponent<LineRenderer>();
        if (lineRenderer != null)
            lineRenderer.enabled = enabled;
    }
    
    Transform FindNearestBrainrot()
    {
        BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        if (allBrainrots == null || allBrainrots.Length == 0)
            return null;
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        BrainrotObject closest = null;
        foreach (BrainrotObject brainrot in allBrainrots)
        {
            if (brainrot.IsCarried() || brainrot.IsPlaced())
                continue;
            float distance = Vector3.Distance(playerTransform.position, brainrot.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = brainrot;
            }
        }
        return closest?.transform;
    }
    
    /// <summary>Ближайший unfought брейнрот (не в руках, не размещён).</summary>
    Transform FindNearestUnfoughtBrainrot()
    {
        BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        if (allBrainrots == null || allBrainrots.Length == 0)
            return null;
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        BrainrotObject closest = null;
        foreach (BrainrotObject brainrot in allBrainrots)
        {
            if (!brainrot.IsUnfought() || brainrot.IsCarried() || brainrot.IsPlaced())
                continue;
            float distance = Vector3.Distance(playerTransform.position, brainrot.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = brainrot;
            }
        }
        return closest?.transform;
    }
    
    Transform GetPlayerTransform()
    {
        if (playerCarryController != null)
            return playerCarryController.GetPlayerTransform();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }
    
    PlacementPanel FindNearestEmptyPlacement()
    {
        // Находим все панели размещения в сцене
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        
        if (allPanels == null || allPanels.Length == 0)
        {
            return null;
        }
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        PlacementPanel closest = null;
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null)
                continue;
            if (panel.GetPlacedBrainrot() != null)
                continue;
            float distance = Vector3.Distance(playerTransform.position, panel.GetPlacementPosition());
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = panel;
            }
        }
        return closest;
    }
    
    /// <summary>Ближайшая панель размещения (пустая или занятая).</summary>
    PlacementPanel FindNearestPlacement()
    {
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        if (allPanels == null || allPanels.Length == 0)
            return null;
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        PlacementPanel closest = null;
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null)
                continue;
            float distance = Vector3.Distance(playerTransform.position, panel.GetPlacementPosition());
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
