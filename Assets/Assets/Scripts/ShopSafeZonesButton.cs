using UnityEngine;
using System.Collections;

/// <summary>
/// Скрипт для 3D кнопки открытия магазина безопасных зон
/// При наступлении игрока на кнопку показывает SafeZonesModalContainer
/// При уходе игрока скрывает модальное окно
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShopSafeZonesButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SafeZonesModalContainer GameObject, который будет показан при наступлении на кнопку")]
    [SerializeField] private GameObject safeZonesModalContainer;
    
    [Header("Settings")]
    [Tooltip("Скрывать SafeZonesModalContainer при старте (если true, контейнер будет скрыт в Start)")]
    [SerializeField] private bool hideOnStart = true;
    
    [Header("Player Detection")]
    [Tooltip("Тег игрока (по умолчанию 'Player')")]
    [SerializeField] private string playerTag = "Player";
    
    // Флаг для отслеживания, находится ли игрок на кнопке
    private bool isPlayerOnButton = false;
    
    private void Awake()
    {
        // Проверяем наличие триггера
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Убеждаемся, что коллайдер настроен как триггер
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }
    }
    
    private void Start()
    {
        // Автоматически находим SafeZonesModalContainer, если не назначен
        if (safeZonesModalContainer == null)
        {
            // Ищем в сцене объект с именем "SafeZonesModalContainer"
            GameObject foundContainer = GameObject.Find("SafeZonesModalContainer");
            if (foundContainer != null)
            {
                safeZonesModalContainer = foundContainer;
            }
        }
        
        // Скрываем контейнер при старте, если требуется
        if (safeZonesModalContainer != null && hideOnStart)
        {
            safeZonesModalContainer.SetActive(false);
        }
    }
    
    /// <summary>
    /// Вызывается когда объект входит в триггер
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, что это игрок
        if (other.CompareTag(playerTag))
        {
            isPlayerOnButton = true;
            OpenShop();
        }
    }
    
    /// <summary>
    /// Вызывается когда объект выходит из триггера
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // Проверяем, что это игрок
        if (other.CompareTag(playerTag))
        {
            isPlayerOnButton = false;
            
            // Скрываем модальное окно при уходе игрока
            CloseShop();
        }
    }
    
    /// <summary>
    /// Публичный метод для открытия магазина (можно вызвать извне)
    /// </summary>
    public void OpenShop()
    {
        if (safeZonesModalContainer != null)
        {
            // Активируем модальное окно
            safeZonesModalContainer.SetActive(true);
            
            // Обновляем локализацию после активации с небольшой задержкой
            // Это необходимо, чтобы YG2 успел инициализироваться
            StartCoroutine(UpdateLocalizationDelayed());
        }
    }
    
    /// <summary>
    /// Обновляет все LocalizedText компоненты в модальном окне с небольшой задержкой
    /// Это необходимо, чтобы YG2 успел инициализироваться перед обновлением текстов
    /// Также обновляет цвета цен в зависимости от баланса
    /// </summary>
    private IEnumerator UpdateLocalizationDelayed()
    {
        // Ждем конец кадра, чтобы YG2 успел инициализироваться
        yield return new WaitForEndOfFrame();
        
        if (safeZonesModalContainer != null && safeZonesModalContainer.activeSelf)
        {
            // Находим все LocalizedText компоненты в модальном окне (включая дочерние объекты)
            LocalizedText[] localizedTexts = safeZonesModalContainer.GetComponentsInChildren<LocalizedText>(true);
            
            foreach (LocalizedText localizedText in localizedTexts)
            {
                if (localizedText != null)
                {
                    // Обновляем текст с правильным языком
                    localizedText.UpdateText();
                }
            }
            
            // Обновляем цвета цен в SafeZonesManager
            SafeZonesManager zonesManager = safeZonesModalContainer.GetComponentInChildren<SafeZonesManager>();
            if (zonesManager != null)
            {
                // Вызываем метод обновления всех зон для обновления цветов цен
                zonesManager.UpdateAllSafeZones();
            }
        }
    }
    
    /// <summary>
    /// Публичный метод для закрытия магазина
    /// </summary>
    public void CloseShop()
    {
        if (safeZonesModalContainer != null)
        {
            safeZonesModalContainer.SetActive(false);
        }
    }
}
