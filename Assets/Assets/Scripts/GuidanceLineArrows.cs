using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GuidanceLine
{
    /// <summary>
    /// Компонент для создания анимированных стрелок вдоль LineRenderer
    /// </summary>
    public class GuidanceLineArrows : MonoBehaviour
    {
        [Header("Настройки стрелок")]
        [Tooltip("Sprite стрелки, который будет отображаться вдоль линии")]
        [SerializeField] private Sprite arrowSprite;
        
        [Tooltip("Количество стрелок на единицу расстояния (например, 2 = 2 стрелки на 1 единицу длины)")]
        [SerializeField] private float arrowsPerUnit = 2f;
        
        [Tooltip("Скорость движения стрелок (единиц в секунду)")]
        [SerializeField] private float arrowSpeed = 2f;
        
        [Tooltip("Размер стрелок")]
        [SerializeField] private Vector2 arrowSize = new Vector2(0.5f, 0.5f);
        
        [Tooltip("Смещение стрелок от линии по оси Y")]
        [SerializeField] private float arrowOffsetY = 0.1f;
        
        [Tooltip("Расстояние между стрелками (0 = равномерное распределение)")]
        [SerializeField] private float arrowSpacing = 0f;
        
        [Tooltip("Цвет стрелок")]
        [SerializeField] private Color arrowColor = Color.white;
        
        [Header("Настройки анимации")]
        [Tooltip("Зациклить анимацию стрелок")]
        [SerializeField] private bool loopAnimation = true;
        
        [Tooltip("Начальная позиция анимации (0 = начало линии, 1 = конец)")]
        [SerializeField] private float animationStartPosition = 0f;

        private LineRenderer lineRenderer;
        private List<GameObject> arrowObjects = new List<GameObject>();
        private List<float> arrowPositions = new List<float>(); // Позиция каждой стрелки от 0 до 1
        private float totalLineLength = 0f;
        private float lastArrowCountLength = 0f; // Длина линии при последнем создании стрелок
        private bool isInitialized = false;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            lineRenderer = GetComponent<LineRenderer>();
            
            if (lineRenderer == null)
            {
                Debug.LogError("GuidanceLineArrows: LineRenderer не найден на объекте!");
                return;
            }

            if (arrowSprite == null)
            {
                Debug.LogWarning("GuidanceLineArrows: Sprite стрелки не назначен!");
                return;
            }

            // Ждем один кадр, чтобы LineRenderer успел инициализироваться
            StartCoroutine(DelayedInitialization());
        }

        System.Collections.IEnumerator DelayedInitialization()
        {
            // Ждем один кадр
            yield return null;
            
            // Проверяем, что LineRenderer имеет точки
            if (lineRenderer != null && lineRenderer.positionCount >= 2)
            {
                CalculateLineLength();
                CreateArrows();
                isInitialized = true;
            }
            else
            {
                // Если точек еще нет, ждем еще немного
                yield return new WaitForSeconds(0.1f);
                if (lineRenderer != null && lineRenderer.positionCount >= 2)
                {
                    CalculateLineLength();
                    CreateArrows();
                    isInitialized = true;
                }
            }
        }

        void Update()
        {
            if (lineRenderer == null)
                return;

            // Если стрелок нет, но линия есть - создаём их
            if (!isInitialized && lineRenderer.positionCount >= 2)
            {
                CalculateLineLength();
                if (totalLineLength > 0)
                {
                    CreateArrows();
                    lastArrowCountLength = totalLineLength;
                    isInitialized = true;
                }
                return;
            }

            if (!isInitialized)
                return;

            // Пересчитываем длину линии
            CalculateLineLength();
            
            // Если длина линии изменилась значительно, пересоздаём стрелки
            if (Mathf.Abs(totalLineLength - lastArrowCountLength) > 0.1f)
            {
                int oldCount = Mathf.RoundToInt(lastArrowCountLength * arrowsPerUnit);
                int newCount = Mathf.RoundToInt(totalLineLength * arrowsPerUnit);
                
                // Пересоздаём стрелки только если их количество изменилось
                if (oldCount != newCount)
                {
                    CreateArrows();
                    lastArrowCountLength = totalLineLength;
                }
            }

            if (arrowObjects.Count == 0)
                return;

            // Обновляем позиции стрелок
            UpdateArrowPositions();
        }

        void CalculateLineLength()
        {
            if (lineRenderer.positionCount < 2)
            {
                totalLineLength = 0f;
                return;
            }

            totalLineLength = 0f;
            for (int i = 0; i < lineRenderer.positionCount - 1; i++)
            {
                Vector3 point1 = lineRenderer.GetPosition(i);
                Vector3 point2 = lineRenderer.GetPosition(i + 1);
                totalLineLength += Vector3.Distance(point1, point2);
            }
        }

        void CreateArrows()
        {
            // Удаляем старые стрелки
            ClearArrows();

            if (arrowsPerUnit <= 0 || totalLineLength <= 0)
                return;

            // Вычисляем количество стрелок на основе расстояния
            int arrowCount = Mathf.RoundToInt(totalLineLength * arrowsPerUnit);
            
            // Минимум 1 стрелка, если линия существует
            if (arrowCount < 1)
                arrowCount = 1;

            // Создаем новые стрелки
            for (int i = 0; i < arrowCount; i++)
            {
                GameObject arrowObj = new GameObject($"Arrow_{i}");
                arrowObj.transform.SetParent(transform);
                
                SpriteRenderer spriteRenderer = arrowObj.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = arrowSprite;
                spriteRenderer.color = arrowColor;
                spriteRenderer.sortingOrder = 10; // Чтобы стрелки были поверх линии

                // Устанавливаем размер
                arrowObj.transform.localScale = new Vector3(arrowSize.x, arrowSize.y, 1f);

                arrowObjects.Add(arrowObj);

                // Вычисляем начальную позицию
                float startPos = animationStartPosition;
                if (arrowSpacing > 0)
                {
                    // Используем фиксированное расстояние
                    startPos = (i * arrowSpacing) / totalLineLength;
                }
                else
                {
                    // Равномерное распределение
                    startPos = (float)i / arrowCount;
                }
                
                arrowPositions.Add(startPos);
            }
        }

        void UpdateArrowPositions()
        {
            if (lineRenderer.positionCount < 2)
                return;
            
            if (totalLineLength <= 0)
                return;

            float deltaTime = Time.deltaTime;
            float distancePerFrame = (arrowSpeed * deltaTime) / totalLineLength;

            for (int i = 0; i < arrowObjects.Count; i++)
            {
                if (arrowObjects[i] == null)
                    continue;

                // Обновляем позицию стрелки (нормализованная позиция от 0 до 1)
                arrowPositions[i] += distancePerFrame;

                // Зацикливаем анимацию
                if (loopAnimation && arrowPositions[i] >= 1f)
                {
                    arrowPositions[i] = arrowPositions[i] - 1f; // Сохраняем остаток для плавности
                }

                // Получаем позицию и направление на линии
                float normalizedPos = Mathf.Clamp01(arrowPositions[i]);
                Vector3 position = GetPositionOnLine(normalizedPos);
                Vector3 direction = GetDirectionOnLine(normalizedPos);

                // Устанавливаем позицию стрелки
                arrowObjects[i].transform.position = position + Vector3.up * arrowOffsetY;

                // Поворачиваем стрелку по направлению движения
                if (direction != Vector3.zero)
                {
                    // Для 3D используем LookRotation, для 2D - поворот по Z
                    if (Mathf.Abs(direction.z) < 0.01f)
                    {
                        // 2D случай (XY плоскость)
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        arrowObjects[i].transform.rotation = Quaternion.Euler(90f, 0, angle - 90f);
                    }
                    else
                    {
                        // 3D случай
                        arrowObjects[i].transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0, 0);
                    }
                }

                // Скрываем стрелку, если она вышла за пределы линии
                arrowObjects[i].SetActive(normalizedPos < 1f || loopAnimation);
            }
        }

        Vector3 GetPositionOnLine(float normalizedPosition)
        {
            if (lineRenderer.positionCount < 2)
                return transform.position;

            normalizedPosition = Mathf.Clamp01(normalizedPosition);
            float totalDistance = normalizedPosition * totalLineLength;
            float currentDistance = 0f;

            for (int i = 0; i < lineRenderer.positionCount - 1; i++)
            {
                Vector3 point1 = lineRenderer.GetPosition(i);
                Vector3 point2 = lineRenderer.GetPosition(i + 1);
                float segmentLength = Vector3.Distance(point1, point2);

                if (currentDistance + segmentLength >= totalDistance)
                {
                    // Стрелка находится в этом сегменте
                    float t = (totalDistance - currentDistance) / segmentLength;
                    return Vector3.Lerp(point1, point2, t);
                }

                currentDistance += segmentLength;
            }

            // Если дошли сюда, возвращаем последнюю точку
            return lineRenderer.GetPosition(lineRenderer.positionCount - 1);
        }

        Vector3 GetDirectionOnLine(float normalizedPosition)
        {
            if (lineRenderer.positionCount < 2)
                return Vector3.right;

            normalizedPosition = Mathf.Clamp01(normalizedPosition);
            float totalDistance = normalizedPosition * totalLineLength;
            float currentDistance = 0f;

            for (int i = 0; i < lineRenderer.positionCount - 1; i++)
            {
                Vector3 point1 = lineRenderer.GetPosition(i);
                Vector3 point2 = lineRenderer.GetPosition(i + 1);
                float segmentLength = Vector3.Distance(point1, point2);

                if (currentDistance + segmentLength >= totalDistance)
                {
                    // Направление в этом сегменте
                    Vector3 direction = (point2 - point1).normalized;
                    return direction;
                }

                currentDistance += segmentLength;
            }

            // Если дошли сюда, возвращаем направление последнего сегмента
            if (lineRenderer.positionCount >= 2)
            {
                Vector3 lastPoint1 = lineRenderer.GetPosition(lineRenderer.positionCount - 2);
                Vector3 lastPoint2 = lineRenderer.GetPosition(lineRenderer.positionCount - 1);
                return (lastPoint2 - lastPoint1).normalized;
            }

            return Vector3.right;
        }

        void ClearArrows()
        {
            foreach (GameObject arrow in arrowObjects)
            {
                if (arrow != null)
                {
                    if (Application.isPlaying)
                        Destroy(arrow);
                    else
                        DestroyImmediate(arrow);
                }
            }
            arrowObjects.Clear();
            arrowPositions.Clear();
        }

        void OnDestroy()
        {
            ClearArrows();
        }

        void OnDisable()
        {
            // Скрываем стрелки при отключении компонента
            foreach (GameObject arrow in arrowObjects)
            {
                if (arrow != null)
                    arrow.SetActive(false);
            }
        }

        void OnEnable()
        {
            // Показываем стрелки при включении компонента
            if (isInitialized)
            {
                foreach (GameObject arrow in arrowObjects)
                {
                    if (arrow != null)
                        arrow.SetActive(true);
                }
            }
        }

        // Метод для обновления стрелок при изменении линии (можно вызывать извне)
        // ВНИМАНИЕ: Этот метод пересоздаёт стрелки, что сбрасывает их анимацию
        // Используйте только при необходимости (например, при изменении количества стрелок)
        public void RefreshArrows()
        {
            if (lineRenderer == null || lineRenderer.positionCount < 2)
                return;

            CalculateLineLength();
            
            // Если стрелки еще не инициализированы, инициализируем их
            if (!isInitialized && totalLineLength > 0)
            {
                CreateArrows();
                isInitialized = true;
            }
            // Не пересоздаём стрелки, если они уже есть - это сбрасывает анимацию
            // Стрелки сами обновляются в Update() через UpdateArrowPositions()
        }
    }
}
