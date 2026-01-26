using UnityEngine;

namespace GuidanceLine
{
    /// <summary>
    /// Компонент для создания направляющей линии от игрока к целевой точке
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class GuidanceLine : MonoBehaviour
    {
        [Header("Настройки линии")]
        [Tooltip("Целевая точка (конец линии)")]
        [SerializeField] private Transform endPoint;

        [Tooltip("Смещение начальной точки по оси Y")]
        [SerializeField] private float pos1Y = 0f;

        [Tooltip("Ширина линии")]
        [SerializeField] private float lineWidth = 0.05f;

        [Tooltip("Количество точек для отрисовки линии (больше = плавнее)")]
        [SerializeField] private int linePoints = 50;

        [Tooltip("Материал для линии")]
        [SerializeField] private Material lineMaterial;

        private LineRenderer lineRenderer;
        private Transform playerTransform;
        private GuidanceLineArrows guidanceLineArrows;
        private bool isInitialized = false;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            // Получаем компонент LineRenderer
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            // Получаем компонент GuidanceLineArrows, если он есть
            guidanceLineArrows = GetComponent<GuidanceLineArrows>();

            // Настраиваем LineRenderer
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = linePoints;

            if (lineMaterial != null)
            {
                lineRenderer.material = lineMaterial;
            }

            // Находим игрока
            FindPlayer();

            isInitialized = true;
        }

        void FindPlayer()
        {
            // Автоматически находим игрока по тегу "Player"
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogWarning("GuidanceLine: Не найден объект с тегом 'Player'! Убедитесь, что игрок имеет тег 'Player'.");
            }
        }

        void Update()
        {
            // Обновляем позицию игрока, если он не найден (на случай, если он появится позже)
            if (playerTransform == null)
            {
                FindPlayer();
            }

            // Обновляем линию
            if (isInitialized && playerTransform != null && endPoint != null)
            {
                UpdateLine();
            }
            else if (endPoint == null)
            {
                // Скрываем линию, если нет целевой точки
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
        }

        void UpdateLine()
        {
            if (lineRenderer == null || playerTransform == null || endPoint == null)
                return;

            // Включаем линию
            lineRenderer.enabled = true;

            // Вычисляем позиции точек линии
            Vector3 startPos = playerTransform.position;
            startPos.y += pos1Y; // Применяем смещение по Y
            Vector3 endPos = endPoint.position;

            // Создаем плавную кривую между началом и концом
            for (int i = 0; i < linePoints; i++)
            {
                float t = (float)i / (linePoints - 1);
                // Можно использовать простую линейную интерполяцию или более сложную кривую
                Vector3 point = Vector3.Lerp(startPos, endPos, t);
                
                // Опционально: добавляем небольшую кривизну для более плавного вида
                // Можно раскомментировать, если нужна кривая
                // float curveHeight = 0.5f;
                // point.y += Mathf.Sin(t * Mathf.PI) * curveHeight;

                lineRenderer.SetPosition(i, point);
            }

            // Не вызываем RefreshArrows() здесь - стрелки сами обновляются в своем Update()
            // RefreshArrows() пересоздаёт стрелки, что сбрасывает их анимацию
        }

        void OnValidate()
        {
            // Обновляем настройки при изменении в инспекторе
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = lineWidth;
                lineRenderer.endWidth = lineWidth;
                lineRenderer.positionCount = linePoints;

                if (lineMaterial != null)
                {
                    lineRenderer.material = lineMaterial;
                }
            }
        }

        /// <summary>
        /// Устанавливает целевую точку программно
        /// </summary>
        public void SetEndPoint(Transform target)
        {
            endPoint = target;
        }

        /// <summary>
        /// Получает текущую целевую точку
        /// </summary>
        public Transform GetEndPoint()
        {
            return endPoint;
        }

        /// <summary>
        /// Получает позицию игрока
        /// </summary>
        public Transform GetPlayerTransform()
        {
            return playerTransform;
        }
    }
}
