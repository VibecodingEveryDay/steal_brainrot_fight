using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Управляет телепортацией между стадиями игры (лобби и зона сражения).
/// Использует fade эффект для плавного перехода.
/// </summary>
public class TeleportManager : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("Префаб Canvas с Image для fade эффекта (черный экран)")]
    [SerializeField] private GameObject fadeCanvasPrefab;
    
    [Tooltip("Скорость fade эффекта (время затемнения/осветления в секундах)")]
    [SerializeField] private float fadeSpeed = 0.5f;
    
    [Header("References")]
    [Tooltip("Ссылка на BattleZone (зона сражения)")]
    [SerializeField] private BattleZone battleZone;
    
    [Tooltip("Позиция дома для телепортации после победы над боссом")]
    [SerializeField] private Transform housePos;
    
    private GameObject fadeCanvasInstance;
    private Image fadeImage;
    private bool isFading = false;
    
    // Позиция игрока в лобби (для возврата)
    private Vector3 lobbyPlayerPosition;
    private Quaternion lobbyPlayerRotation;
    
    // Ссылка на игрока
    private Transform playerTransform;
    private ThirdPersonController playerController;
    
    private static TeleportManager instance;
    
    public static TeleportManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TeleportManager>();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Создаем fade canvas если префаб не назначен
        if (fadeCanvasPrefab == null)
        {
            CreateFadeCanvas();
        }
    }
    
    private void Start()
    {
        FindPlayer();
    }
    
    /// <summary>
    /// Находит игрока в сцене
    /// </summary>
    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerController = player.GetComponent<ThirdPersonController>();
            }
            else
            {
                ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
                if (controller != null)
                {
                    playerTransform = controller.transform;
                    playerController = controller;
                }
            }
        }
    }
    
    /// <summary>
    /// Создает fade canvas вручную если префаб не назначен
    /// </summary>
    private void CreateFadeCanvas()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Высокий приоритет
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем Image для затемнения
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        
        Image image = imageObj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f); // Прозрачный
        
        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        fadeCanvasInstance = canvasObj;
        fadeImage = image;
        
        // Скрываем canvas по умолчанию
        canvasObj.SetActive(false);
    }
    
    /// <summary>
    /// Телепортирует игрока в зону сражения
    /// </summary>
    public void TeleportToBattleZone(BrainrotObject brainrotObject)
    {
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"D\",\"location\":\"TeleportManager.cs:137\",\"message\":\"TeleportToBattleZone entry\",\"data\":{{\"brainrotObjectNull\":{(brainrotObject == null).ToString().ToLower()},\"brainrotName\":\"{brainrotObject?.GetObjectName()}\",\"isFading\":{isFading.ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        if (brainrotObject == null)
        {
            Debug.LogError("[TeleportManager] BrainrotObject равен null!");
            return;
        }
        
        Debug.Log($"[TeleportManager] Запрос на телепортацию для: {brainrotObject.GetObjectName()}, isFading: {isFading}");
        
        // ВАЖНО: Если уже идет телепортация, останавливаем предыдущую и начинаем новую
        // Это предотвращает зависание, если предыдущая телепортация не завершилась
        if (isFading)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"TeleportManager.cs:149\",\"message\":\"isFading is true, stopping coroutines\",\"data\":{{\"isFading\":{isFading.ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.LogWarning("[TeleportManager] Уже выполняется телепортация, останавливаем предыдущую и начинаем новую");
            StopAllCoroutines();
            // ВАЖНО: Сбрасываем флаг сразу, чтобы следующая попытка могла начаться
            isFading = false;
        }
        
        // ВАЖНО: Находим BattleZone если она не назначена
        if (battleZone == null)
        {
            battleZone = FindFirstObjectByType<BattleZone>();
            if (battleZone == null)
            {
                Debug.LogError("[TeleportManager] BattleZone не найдена в сцене!");
                // НЕ возвращаемся, продолжаем попытку найти
            }
        }
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[TeleportManager] Игрок не найден!");
                // НЕ возвращаемся, продолжаем попытку найти
            }
        }
        
        // ВАЖНО: Проверяем критичные компоненты перед запуском
        if (battleZone == null)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:179\",\"message\":\"BattleZone is null, returning early\",\"data\":{{\"battleZoneNull\":true}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.LogError("[TeleportManager] BattleZone не найдена, телепортация невозможна!");
            return;
        }
        
        if (playerTransform == null)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:185\",\"message\":\"playerTransform is null, returning early\",\"data\":{{\"playerTransformNull\":true}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.LogError("[TeleportManager] Игрок не найден, телепортация невозможна!");
            return;
        }
        
        Debug.Log($"[TeleportManager] Начинается телепортация в зону сражения для брейнрота: {brainrotObject.GetObjectName()}");
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"F\",\"location\":\"TeleportManager.cs:194\",\"message\":\"Starting coroutine\",\"data\":{{\"brainrotName\":\"{brainrotObject.GetObjectName()}\"}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        // ВАЖНО: Всегда запускаем корутину
        StartCoroutine(TeleportToBattleZoneCoroutine(brainrotObject));
    }
    
    /// <summary>
    /// Телепортирует игрока в указанную позицию
    /// </summary>
    public void TeleportToPosition(Vector3 position, Quaternion rotation)
    {
        if (isFading)
        {
            Debug.LogWarning("[TeleportManager] Телепортация уже выполняется, пропускаем");
            return;
        }
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[TeleportManager] Игрок не найден!");
                return;
            }
        }
        
        StartCoroutine(TeleportToPositionCoroutine(position, rotation));
    }
    
    /// <summary>
    /// Телепортирует игрока в дом (после победы над боссом)
    /// </summary>
    public void TeleportToHouse()
    {
        if (housePos == null)
        {
            Debug.LogError("[TeleportManager] HousePos не назначен! Установите Transform в инспекторе.");
            return;
        }
        
        TeleportToPosition(housePos.position, housePos.rotation);
    }
    
    /// <summary>
    /// Телепортирует игрока обратно в лобби
    /// </summary>
    public void TeleportToLobby()
    {
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:225\",\"message\":\"TeleportToLobby called\",\"data\":{{\"isFading\":{isFading.ToString().ToLower()},\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)},\"stackTrace\":\"{System.Environment.StackTrace.Substring(0, Mathf.Min(200, System.Environment.StackTrace.Length))}\"}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        if (isFading)
        {
            Debug.LogWarning("[TeleportManager] Уже выполняется телепортация!");
            return;
        }
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[TeleportManager] Игрок не найден!");
                return;
            }
        }
        
        StartCoroutine(TeleportToLobbyCoroutine());
    }
    
    /// <summary>
    /// Корутина для телепортации в зону сражения
    /// </summary>
    private IEnumerator TeleportToBattleZoneCoroutine(BrainrotObject brainrotObject)
    {
        // КРИТИЧНО: Устанавливаем флаг ДО любых yield и проверок
        // Это предотвращает race condition, когда несколько корутин могут запуститься одновременно
        // ВАЖНО: Не проверяем isFading здесь, так как мы уже обработали это в TeleportToBattleZone()
        isFading = true;
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"TeleportManager.cs:229\",\"message\":\"Coroutine started, isFading set to true\",\"data\":{{\"brainrotName\":\"{brainrotObject?.GetObjectName()}\",\"isFading\":{isFading.ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        
        Debug.Log($"[TeleportManager] Корутина телепортации запущена, isFading установлен в true для: {brainrotObject?.GetObjectName() ?? "null"}");
        
        // ВАЖНО: В C# нельзя использовать yield в try-catch, поэтому используем только try-finally
        try
        {
            // ВАЖНО: Проверяем критичные компоненты в начале корутины
            if (brainrotObject == null)
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"TeleportManager.cs:237\",\"message\":\"yield break - brainrotObject is null\",\"data\":{{}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                Debug.LogError("[TeleportManager] BrainrotObject равен null в корутине!");
                yield break;
            }
            
            // ВАЖНО: Проверяем и находим игрока если нужно
            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null)
                {
                    // #region agent log
                    try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                        $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"TeleportManager.cs:247\",\"message\":\"yield break - playerTransform is null\",\"data\":{{}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                    // #endregion
                    
                    Debug.LogError("[TeleportManager] Игрок не найден в корутине!");
                    yield break;
                }
            }
            
            // ВАЖНО: Проверяем и находим BattleZone если нужно
            if (battleZone == null)
            {
                battleZone = FindFirstObjectByType<BattleZone>();
                if (battleZone == null)
                {
                    // #region agent log
                    try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                        $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"C\",\"location\":\"TeleportManager.cs:257\",\"message\":\"yield break - battleZone is null\",\"data\":{{}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                    // #endregion
                    
                    Debug.LogError("[TeleportManager] BattleZone не найдена в корутине!");
                    yield break;
                }
            }
            
            Debug.Log($"[TeleportManager] Начало телепортации для: {brainrotObject.GetObjectName()}");
            
            // Сохраняем позицию игрока в лобби
            if (playerTransform != null)
            {
                lobbyPlayerPosition = playerTransform.position;
                lobbyPlayerRotation = playerTransform.rotation;
            }
            
            // Инициализируем fade canvas если нужно
            if (fadeCanvasInstance == null)
            {
                if (fadeCanvasPrefab != null)
                {
                    fadeCanvasInstance = Instantiate(fadeCanvasPrefab);
                    fadeImage = fadeCanvasInstance.GetComponentInChildren<Image>();
                }
                else
                {
                    CreateFadeCanvas();
                }
            }
            
            // ВАЖНО: Убеждаемся, что fade canvas активен и цвет черный
            if (fadeCanvasInstance != null)
            {
                fadeCanvasInstance.SetActive(true);
                if (fadeImage != null)
                {
                    // Устанавливаем черный цвет
                    Color blackColor = new Color(0f, 0f, 0f, fadeImage.color.a);
                    fadeImage.color = blackColor;
                }
            }
            else
            {
                Debug.LogWarning("[TeleportManager] FadeCanvas не создан, создаем заново");
                CreateFadeCanvas();
                if (fadeCanvasInstance != null)
                {
                    fadeCanvasInstance.SetActive(true);
                    if (fadeImage != null)
                    {
                        Color blackColor = new Color(0f, 0f, 0f, 0f);
                        fadeImage.color = blackColor;
                    }
                }
            }
            
            // Затемняем экран
            yield return StartCoroutine(FadeOut());
            
            Debug.Log("[TeleportManager] Экран затемнен, телепортируем игрока");
            
            // ВАЖНО: Находим BattleZone если она все еще null
            if (battleZone == null)
            {
                battleZone = FindFirstObjectByType<BattleZone>();
                if (battleZone == null)
                {
                    Debug.LogError("[TeleportManager] BattleZone не найдена, но продолжаем телепортацию");
                    // Продолжаем, но используем позицию по умолчанию
                }
            }
            
            // Телепортируем игрока
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:376\",\"message\":\"Before teleporting player\",\"data\":{{\"playerTransformNull\":{(playerTransform == null).ToString().ToLower()},\"battleZoneNull\":{(battleZone == null).ToString().ToLower()},\"currentPlayerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            if (playerTransform != null && battleZone != null)
            {
                // ВАЖНО: Отключаем CharacterController перед телепортацией, чтобы он не сбрасывал позицию
                CharacterController characterController = playerTransform.GetComponent<CharacterController>();
                bool wasControllerEnabled = false;
                if (characterController != null)
                {
                    wasControllerEnabled = characterController.enabled;
                    characterController.enabled = false;
                    
                    // #region agent log
                    try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                        $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:383\",\"message\":\"CharacterController disabled before teleport\",\"data\":{{\"wasEnabled\":{wasControllerEnabled.ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                    // #endregion
                }
                
                Vector3 battleSpawnPosition = battleZone.GetPlayerSpawnPosition();
                Quaternion battleSpawnRotation = battleZone.GetPlayerSpawnRotation();
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:391\",\"message\":\"Setting player position\",\"data\":{{\"battleSpawnPosition\":{{\"x\":{battleSpawnPosition.x},\"y\":{battleSpawnPosition.y},\"z\":{battleSpawnPosition.z}}},\"currentPlayerY\":{playerTransform.position.y}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                playerTransform.position = battleSpawnPosition;
                playerTransform.rotation = battleSpawnRotation;
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:396\",\"message\":\"After setting player position\",\"data\":{{\"expectedY\":{battleSpawnPosition.y},\"actualY\":{playerTransform.position.y},\"difference\":{Mathf.Abs(playerTransform.position.y - battleSpawnPosition.y)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                Debug.Log($"[TeleportManager] Игрок телепортирован в позицию: {battleSpawnPosition}");
                
                // ВАЖНО: Ждем несколько кадров перед включением CharacterController обратно
                // Это гарантирует, что позиция установилась правильно
                yield return null;
                yield return null;
                
                // Включаем CharacterController обратно, если он был включен
                if (characterController != null && wasControllerEnabled)
                {
                    characterController.enabled = true;
                    
                    // #region agent log
                    try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                        $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:410\",\"message\":\"CharacterController re-enabled after teleport\",\"data\":{{\"playerY\":{playerTransform.position.y}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                    // #endregion
                }
            }
            else
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:388\",\"message\":\"Cannot teleport - null check failed\",\"data\":{{\"playerTransformNull\":{(playerTransform == null).ToString().ToLower()},\"battleZoneNull\":{(battleZone == null).ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
                
                Debug.LogError("[TeleportManager] playerTransform или battleZone равны null!");
            }
            
            // Ждем несколько кадров, чтобы камера успела обновиться и позиция игрока установилась
            yield return null;
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:412\",\"message\":\"After first yield, checking player position\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            yield return null;
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:415\",\"message\":\"After second yield, checking player position\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            // Сбрасываем камеру после телепортации
            ResetCameraAfterTeleport();
            
            // ВАЖНО: Обновляем видимость брейнротов после телепортации
            // Это гарантирует, что модельки брейнротов будут видны после перемещения игрока
            BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
            if (distanceHider != null)
            {
                distanceHider.ForceRefresh();
                Debug.Log("[TeleportManager] Видимость брейнротов обновлена после телепортации");
            }
            
            // Ждем еще один кадр после сброса камеры
            yield return null;
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:422\",\"message\":\"After ResetCamera, checking player position before StartBattle\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            // Уведомляем BattleZone о начале боя
            if (battleZone != null && brainrotObject != null)
            {
                Debug.Log($"[TeleportManager] Уведомляем BattleZone о начале боя для: {brainrotObject.GetObjectName()}");
                battleZone.StartBattle(brainrotObject);
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                    $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"B\",\"location\":\"TeleportManager.cs:428\",\"message\":\"After StartBattle, checking player position\",\"data\":{{\"playerY\":{(playerTransform != null ? playerTransform.position.y : 0)}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
                // #endregion
            }
            else
            {
                Debug.LogError("[TeleportManager] BattleZone или brainrotObject равны null при уведомлении о начале боя!");
            }
            
            // Осветляем экран
            yield return StartCoroutine(FadeIn());
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"TeleportManager.cs:360\",\"message\":\"Coroutine completed successfully\",\"data\":{{}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.Log("[TeleportManager] Телепортация завершена успешно");
        }
        finally
        {
            // ВАЖНО: Всегда сбрасываем флаг, даже если произошла ошибка или yield break
            // Это критично для предотвращения зависания
            isFading = false;
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"A\",\"location\":\"TeleportManager.cs:finally\",\"message\":\"isFading reset to false in finally\",\"data\":{{\"isFading\":{isFading.ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            
            Debug.Log("[TeleportManager] Флаг isFading сброшен в finally блоке");
        }
    }
    
    /// <summary>
    /// Корутина для телепортации обратно в лобби
    /// </summary>
    /// <summary>
    /// Телепортирует игрока в указанную позицию (корутина)
    /// </summary>
    private IEnumerator TeleportToPositionCoroutine(Vector3 position, Quaternion rotation)
    {
        isFading = true;
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // Телепортируем игрока в указанную позицию
        if (playerTransform == null)
        {
            FindPlayer();
        }
        
        if (playerTransform != null)
        {
            // Отключаем CharacterController перед телепортацией
            CharacterController characterController = playerTransform.GetComponent<CharacterController>();
            bool wasControllerEnabled = false;
            if (characterController != null)
            {
                wasControllerEnabled = characterController.enabled;
                characterController.enabled = false;
            }
            
            playerTransform.position = position;
            playerTransform.rotation = rotation;
            
            // Ждем несколько кадров, чтобы позиция игрока установилась
            yield return null;
            yield return null;
            
            // Включаем CharacterController обратно
            if (characterController != null && wasControllerEnabled)
            {
                characterController.enabled = true;
            }
        }
        
        // Сбрасываем камеру после телепортации
        ResetCameraAfterTeleport();
        
        // ВАЖНО: Обновляем видимость брейнротов после телепортации
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
            Debug.Log("[TeleportManager] Видимость брейнротов обновлена после телепортации");
        }
        
        // Ждем еще один кадр после сброса камеры
        yield return null;
        
        // Уведомляем BattleZone о конце боя
        if (battleZone != null)
        {
            battleZone.EndBattle();
        }
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        isFading = false;
    }
    
    private IEnumerator TeleportToLobbyCoroutine()
    {
        isFading = true;
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // Телепортируем игрока обратно в лобби
        playerTransform.position = lobbyPlayerPosition;
        playerTransform.rotation = lobbyPlayerRotation;
        
        // Ждем несколько кадров, чтобы камера успела обновиться и позиция игрока установилась
        yield return null;
        yield return null;
        
        // Сбрасываем камеру после телепортации
        ResetCameraAfterTeleport();
        
        // ВАЖНО: Обновляем видимость брейнротов после телепортации
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
            Debug.Log("[TeleportManager] Видимость брейнротов обновлена после телепортации в лобби");
        }
        
        // Ждем еще один кадр после сброса камеры
        yield return null;
        
        // Уведомляем BattleZone о конце боя
        if (battleZone != null)
        {
            battleZone.EndBattle();
        }
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        isFading = false;
    }
    
    /// <summary>
    /// Затемняет экран
    /// </summary>
    private IEnumerator FadeOut()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[TeleportManager] FadeImage равен null, пропускаем fade");
            yield break;
        }
        
        float elapsed = 0f;
        Color color = fadeImage.color;
        
        // ВАЖНО: Убеждаемся, что цвет черный (RGB = 0, 0, 0)
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeSpeed);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Устанавливаем полностью черный экран
        color.a = 1f;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;
    }
    
    /// <summary>
    /// Осветляет экран
    /// </summary>
    private IEnumerator FadeIn()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[TeleportManager] FadeImage равен null, пропускаем fade");
            yield break;
        }
        
        float elapsed = 0f;
        Color color = fadeImage.color;
        
        // ВАЖНО: Убеждаемся, что цвет черный (RGB = 0, 0, 0)
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / fadeSpeed));
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Полностью прозрачный черный
        color.a = 0f;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;
        
        // Скрываем canvas после завершения
        if (fadeCanvasInstance != null)
        {
            fadeCanvasInstance.SetActive(false);
        }
    }
    
    /// <summary>
    /// Устанавливает ссылку на BattleZone
    /// </summary>
    public void SetBattleZone(BattleZone zone)
    {
        battleZone = zone;
    }
    
    /// <summary>
    /// Сбрасывает камеру после телепортации, чтобы предотвратить слишком сильное приближение
    /// </summary>
    private void ResetCameraAfterTeleport()
    {
        // Находим камеру
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera != null)
        {
            // Сначала обновляем ThirdPersonCamera, чтобы камера была на правильной позиции
            ThirdPersonCamera thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCamera>();
            if (thirdPersonCamera != null)
            {
                // Принудительно обновляем позицию камеры
                // Это гарантирует, что камера будет на правильном расстоянии
                thirdPersonCamera.ForceUpdateCameraPosition();
            }
            
            // Затем сбрасываем CameraCollisionHandler, чтобы он пересчитал расстояние
            CameraCollisionHandler collisionHandler = mainCamera.GetComponent<CameraCollisionHandler>();
            if (collisionHandler != null)
            {
                // Обновляем цель камеры, если нужно
                if (playerTransform != null)
                {
                    Transform cameraTarget = playerTransform.Find("CameraTarget");
                    if (cameraTarget != null)
                    {
                        collisionHandler.SetTarget(cameraTarget);
                    }
                }
                
                // Принудительно сбрасываем камеру после телепортации
                // Это полностью пересчитывает направление и расстояние, предотвращая быстрое приближение
                collisionHandler.ForceResetAfterTeleport();
            }
        }
    }
}
