using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Спавнит ботов-помощников в битве с боссом.
/// Топ-5 размещённых брейнротов по доходу — префабы те же, что в placement (без UI подсказки).
/// Боты спавнятся вокруг босса по окружности на одном с ним уровне по Y.
/// </summary>
public class BotSpawner : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Делитель дохода для урона бота (урон = finalIncome / botDamageDivider), аналогично bossHPDivider")]
    [SerializeField] private float botDamageDivider = 1f;
    
    [Tooltip("Максимум ботов (топ по доходу)")]
    [SerializeField] private int maxBots = 5;
    
    [Header("Spawn around boss")]
    [Tooltip("Радиус окружности вокруг босса, на которой спавнятся боты")]
    [SerializeField] private float bossRadius = 3f;
    
    [Tooltip("Смещение по Y относительно уровня босса (0 = тот же уровень)")]
    [SerializeField] private float botOffsetY = 0f;
    
    [Header("Bot movement (видны в Inspector)")]
    [Tooltip("Скорость сближения бота с боссом")]
    [SerializeField] private float approachSpeed = 4f;
    
    [Tooltip("Скорость возврата бота на исходную позицию после удара")]
    [SerializeField] private float returnSpeed = 10f;
    
    [Header("Задержка старта (1 раз с начала битвы)")]
    [Tooltip("Шаг задержки в секундах: 1-й бот бьёт сразу, 2-й через n, 3-й через n*2 и т.д.")]
    [SerializeField] private float botStartDelayN = 0.5f;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private BattleManager battleManager;
    private BattleZone battleZone;
    private readonly List<GameObject> spawnedBots = new List<GameObject>();
    
    private void Awake()
    {
        if (battleManager == null)
            battleManager = BattleManager.Instance;
    }
    
    private void OnEnable()
    {
        if (battleManager == null)
            battleManager = BattleManager.Instance;
        if (battleManager != null)
        {
            battleManager.OnBattleStarted += OnBattleStarted;
            battleManager.OnBattleEnded += OnBattleEnded;
        }
    }
    
    private void OnDisable()
    {
        if (battleManager != null)
        {
            battleManager.OnBattleStarted -= OnBattleStarted;
            battleManager.OnBattleEnded -= OnBattleEnded;
        }
    }
    
    private void OnBattleStarted()
    {
        ClearBots();
        
        battleZone = battleManager != null ? battleManager.GetCurrentBattleZone() : null;
        if (battleZone == null)
        {
            battleZone = FindFirstObjectByType<BattleZone>();
            if (battleZone == null && debug)
                Debug.LogWarning("[BotSpawner] BattleZone не найдена.");
        }
        
        Vector3 bossCenter = battleZone != null ? battleZone.GetBossSpawnPosition() : transform.position;
        float spawnY = bossCenter.y + botOffsetY;
        
        List<PlacementData> placed = GameStorage.Instance != null
            ? new List<PlacementData>(GameStorage.Instance.GetAllPlacedBrainrots())
            : new List<PlacementData>();
        
        if (placed.Count == 0)
        {
            if (debug) Debug.Log("[BotSpawner] Нет размещённых брейнротов.");
            return;
        }
        
        var withIncome = placed
            .Select(p => new { Placement = p, Income = GameStorage.GetFinalIncomeFromPlacement(p) })
            .Where(x => x.Income > 0)
            .OrderByDescending(x => x.Income)
            .Take(maxBots)
            .ToList();
        
        for (int i = 0; i < withIncome.Count; i++)
        {
            PlacementData p = withIncome[i].Placement;
            double income = withIncome[i].Income;
            float damage = botDamageDivider > 0 ? (float)(income / botDamageDivider) : (float)income;
            Vector3 spawnPos = GetSpawnPositionAroundBoss(i, withIncome.Count, bossCenter, spawnY);
            
            GameObject prefab = LoadBrainrotPrefabByName(p.brainrotName);
            if (prefab == null)
            {
                if (debug) Debug.LogWarning($"[BotSpawner] Не найден префаб брейнрота '{p.brainrotName}'.");
                continue;
            }
            
            GameObject bot = Instantiate(prefab, spawnPos, Quaternion.identity);
            
            BrainrotObject brainrot = bot.GetComponentInChildren<BrainrotObject>();
            if (brainrot != null)
            {
                brainrot.SetRarity(p.rarity);
                brainrot.SetLevel(p.level);
                brainrot.SetBaseIncome(p.baseIncome);
                if (brainrot.GetInvertBossRotation())
                    bot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                brainrot.enabled = false;
            }
            
            DisableBrainrotInfoUI(bot);
            
            if (bot.GetComponent<CharacterController>() == null)
                bot.AddComponent<CharacterController>();
            AllyBotController ally = bot.GetComponent<AllyBotController>();
            if (ally == null)
                ally = bot.AddComponent<AllyBotController>();
            ally.Initialize(spawnPos, damage);
            ally.SetSpeeds(approachSpeed, returnSpeed);
            ally.SetStartDelay(i * botStartDelayN);
            
            spawnedBots.Add(bot);
            if (battleManager != null)
                battleManager.AddAlly(bot);
            
            if (debug)
                Debug.Log($"[BotSpawner] Заспавнен бот {i + 1} ({p.brainrotName}), доход: {income}, урон: {damage}, позиция: {spawnPos}");
        }
    }
    
    /// <summary>
    /// Отключает UI подсказку (brinfo) у заспавненного брейнрота — как в placement, но без подсказки.
    /// </summary>
    private static void DisableBrainrotInfoUI(GameObject brainrotRoot)
    {
        Transform[] all = brainrotRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t != null && t.gameObject != null &&
                (t.name.Contains("InfoPrefab") || t.name.Contains("UI")))
            {
                t.gameObject.SetActive(false);
            }
        }
        InteractableObject io = brainrotRoot.GetComponent<InteractableObject>();
        if (io != null)
            io.HideUI();
    }
    
    /// <summary>
    /// Загружает префаб брейнрота по имени из Resources (работает в билде и в редакторе).
    /// </summary>
    private static GameObject LoadBrainrotPrefabByName(string brainrotName)
    {
        if (string.IsNullOrEmpty(brainrotName)) return null;
        GameObject[] all = Resources.LoadAll<GameObject>("game/Brainrots");
        if (all == null) return null;
        foreach (GameObject prefab in all)
        {
            if (prefab == null) continue;
            BrainrotObject br = prefab.GetComponentInChildren<BrainrotObject>(true);
            if (br != null && br.GetObjectName() == brainrotName)
                return prefab;
        }
        return null;
    }
    
    private void OnBattleEnded()
    {
        ClearBots();
    }
    
    /// <summary>
    /// Позиция бота на окружности вокруг босса (центр = bossCenter, радиус = bossRadius, Y = spawnY).
    /// </summary>
    private Vector3 GetSpawnPositionAroundBoss(int index, int totalCount, Vector3 bossCenter, float spawnY)
    {
        if (totalCount <= 0) totalCount = 1;
        float angle = index * (360f / totalCount) * Mathf.Deg2Rad;
        float x = bossCenter.x + Mathf.Cos(angle) * bossRadius;
        float z = bossCenter.z + Mathf.Sin(angle) * bossRadius;
        return new Vector3(x, spawnY, z);
    }
    
    /// <summary>
    /// Уничтожает всех заспавненных ботов и очищает список.
    /// </summary>
    public void ClearBots()
    {
        foreach (GameObject bot in spawnedBots)
        {
            if (bot != null)
                Destroy(bot);
        }
        spawnedBots.Clear();
    }
}
