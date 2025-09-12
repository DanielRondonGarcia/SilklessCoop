using BepInEx.Logging;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SilklessCoop
{
    /// <summary>
    /// Maneja las barras de vida que aparecen sobre los enemigos cuando reciben daño
    /// </summary>
    public class EnemyHealthBar : MonoBehaviour
    {
        internal ManualLogSource Logger;
        internal ModConfig Config;
        
        private Canvas _worldCanvas;
        private Dictionary<string, GameObject> _activeHealthBars = new Dictionary<string, GameObject>();
        private Dictionary<string, Coroutine> _fadeCoroutines = new Dictionary<string, Coroutine>();
        
        // Configuración visual
        private const float HEALTH_BAR_WIDTH = 40f;
        private const float HEALTH_BAR_HEIGHT = 4f;
        private const float HEALTH_BAR_OFFSET_Y = 1.5f;
        private const float FADE_DURATION = 3f;
        private const float DISPLAY_DURATION = 2f;
        
        private void Start()
        {
            CreateWorldCanvas();
        }
        
        /// <summary>
        /// Crea un canvas en espacio mundial para las barras de vida
        /// </summary>
        private void CreateWorldCanvas()
        {
            GameObject canvasObj = new GameObject("EnemyHealthBarCanvas");
            _worldCanvas = canvasObj.AddComponent<Canvas>();
            
            // Intentar usar ScreenSpaceCamera si hay una cámara disponible
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _worldCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                _worldCanvas.worldCamera = mainCamera;
                _worldCanvas.planeDistance = 10f;
            }
            else
            {
                // Fallback a WorldSpace si no hay cámara
                _worldCanvas.renderMode = RenderMode.WorldSpace;
            }
            
            _worldCanvas.sortingOrder = 100; // Encima de otros elementos del juego
            
            // Configurar el canvas
            RectTransform canvasRect = _worldCanvas.GetComponent<RectTransform>();
            if (_worldCanvas.renderMode == RenderMode.WorldSpace)
            {
                canvasRect.sizeDelta = new Vector2(100, 100);
                canvasRect.localScale = Vector3.one * 0.01f; // Escala muy pequeña para WorldSpace
            }
            else
            {
                canvasRect.sizeDelta = new Vector2(Screen.width, Screen.height);
            }
            
            // Agregar CanvasScaler
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            
            // Agregar GraphicRaycaster
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            if (Config.PrintDebugOutput)
                Logger.LogInfo($"Created canvas for enemy health bars (mode: {_worldCanvas.renderMode})");
        }
        
        /// <summary>
        /// Muestra una barra de vida sobre un enemigo
        /// </summary>
        /// <param name="enemyId">ID del enemigo</param>
        /// <param name="enemyObject">GameObject del enemigo</param>
        /// <param name="currentHealth">Vida actual</param>
        /// <param name="maxHealth">Vida máxima</param>
        public void ShowHealthBar(string enemyId, GameObject enemyObject, int currentHealth, int maxHealth)
        {
            if (Config.PrintDebugOutput)
                Logger.LogInfo($"ShowHealthBar called for {enemyId}: {currentHealth}/{maxHealth}");
            
            if (enemyObject == null)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogWarning($"Enemy object is null for {enemyId}");
                return;
            }
            
            if (maxHealth <= 0)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogWarning($"Invalid max health ({maxHealth}) for enemy {enemyId}");
                return;
            }
            
            if (_worldCanvas == null)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogWarning("World canvas is null, cannot create health bar");
                return;
            }
            
            try
            {
                // Si ya existe una barra para este enemigo, actualizarla
                if (_activeHealthBars.ContainsKey(enemyId))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Updating existing health bar for {enemyId}");
                    UpdateHealthBar(enemyId, currentHealth, maxHealth);
                    return;
                }
                
                // Crear nueva barra de vida
                GameObject healthBarObj = CreateHealthBarObject(enemyId, enemyObject, currentHealth, maxHealth);
                if (healthBarObj == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"Failed to create health bar object for {enemyId}");
                    return;
                }
                
                _activeHealthBars[enemyId] = healthBarObj;
                
                // Iniciar corrutina de fade out
                if (_fadeCoroutines.ContainsKey(enemyId))
                {
                    StopCoroutine(_fadeCoroutines[enemyId]);
                }
                _fadeCoroutines[enemyId] = StartCoroutine(FadeOutHealthBar(enemyId));
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Successfully created health bar for enemy {enemyId}: {currentHealth}/{maxHealth}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error showing health bar for enemy {enemyId}: {ex.Message}\nStack trace: {ex.StackTrace}");
            }
        }
        
        /// <summary>
        /// Crea el GameObject de la barra de vida
        /// </summary>
        private GameObject CreateHealthBarObject(string enemyId, GameObject enemyObject, int currentHealth, int maxHealth)
        {
            if (Config.PrintDebugOutput)
                Logger.LogInfo($"Creating health bar for enemy {enemyId} at position {enemyObject.transform.position}");
            
            // Crear contenedor principal
            GameObject healthBarContainer = new GameObject($"HealthBar_{enemyId}");
            healthBarContainer.transform.SetParent(_worldCanvas.transform, false);
            
            // Configurar RectTransform
            RectTransform containerRect = healthBarContainer.AddComponent<RectTransform>();
            containerRect.sizeDelta = new Vector2(HEALTH_BAR_WIDTH, HEALTH_BAR_HEIGHT);
            
            // Posicionamiento depende del modo de renderizado
            if (_worldCanvas.renderMode == RenderMode.WorldSpace)
            {
                // En WorldSpace, usar posición mundial
                Vector3 enemyPos = enemyObject.transform.position;
                healthBarContainer.transform.position = enemyPos + Vector3.up * HEALTH_BAR_OFFSET_Y;
            }
            else
            {
                // En ScreenSpaceCamera, convertir a posición de pantalla
                Vector3 enemyPos = enemyObject.transform.position + Vector3.up * HEALTH_BAR_OFFSET_Y;
                Vector3 screenPos = _worldCanvas.worldCamera.WorldToScreenPoint(enemyPos);
                containerRect.position = screenPos;
            }
            
            // Crear fondo de la barra (negro semi-transparente)
            GameObject background = new GameObject("Background");
            background.transform.SetParent(healthBarContainer.transform, false);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.6f);
            
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            // Crear barra de vida (roja a verde según el porcentaje)
            GameObject healthBar = new GameObject("HealthBar");
            healthBar.transform.SetParent(healthBarContainer.transform, false);
            Image healthImage = healthBar.AddComponent<Image>();
            
            // Color basado en el porcentaje de vida
            float healthPercent = (float)currentHealth / maxHealth;
            Color healthColor = Color.Lerp(Color.red, Color.green, healthPercent);
            healthImage.color = healthColor;
            
            RectTransform healthRect = healthBar.GetComponent<RectTransform>();
            healthRect.anchorMin = Vector2.zero;
            healthRect.anchorMax = new Vector2(healthPercent, 1f);
            healthRect.sizeDelta = Vector2.zero;
            healthRect.anchoredPosition = Vector2.zero;
            
            // Agregar componente para seguir al enemigo
            EnemyHealthBarFollower follower = healthBarContainer.AddComponent<EnemyHealthBarFollower>();
            follower.Initialize(enemyObject, HEALTH_BAR_OFFSET_Y);
            
            return healthBarContainer;
        }
        
        /// <summary>
        /// Actualiza una barra de vida existente
        /// </summary>
        private void UpdateHealthBar(string enemyId, int currentHealth, int maxHealth)
        {
            if (!_activeHealthBars.ContainsKey(enemyId)) return;
            
            GameObject healthBarContainer = _activeHealthBars[enemyId];
            if (healthBarContainer == null) return;
            
            // Encontrar la barra de vida
            Transform healthBarTransform = healthBarContainer.transform.Find("HealthBar");
            if (healthBarTransform == null) return;
            
            Image healthImage = healthBarTransform.GetComponent<Image>();
            RectTransform healthRect = healthBarTransform.GetComponent<RectTransform>();
            
            if (healthImage != null && healthRect != null)
            {
                // Actualizar color y tamaño
                float healthPercent = (float)currentHealth / maxHealth;
                Color healthColor = Color.Lerp(Color.red, Color.green, healthPercent);
                healthImage.color = healthColor;
                healthRect.anchorMax = new Vector2(healthPercent, 1f);
                
                // Reiniciar el fade timer
                if (_fadeCoroutines.ContainsKey(enemyId))
                {
                    StopCoroutine(_fadeCoroutines[enemyId]);
                }
                _fadeCoroutines[enemyId] = StartCoroutine(FadeOutHealthBar(enemyId));
                
                // Resetear alpha
                CanvasGroup canvasGroup = healthBarContainer.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }
        }
        
        /// <summary>
        /// Corrutina que hace fade out de la barra de vida después de un tiempo
        /// </summary>
        private IEnumerator FadeOutHealthBar(string enemyId)
        {
            yield return new WaitForSeconds(DISPLAY_DURATION);
            
            if (!_activeHealthBars.ContainsKey(enemyId)) yield break;
            
            GameObject healthBarContainer = _activeHealthBars[enemyId];
            if (healthBarContainer == null) yield break;
            
            // Agregar CanvasGroup si no existe
            CanvasGroup canvasGroup = healthBarContainer.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = healthBarContainer.AddComponent<CanvasGroup>();
            }
            
            // Fade out gradual
            float fadeTimer = 0f;
            while (fadeTimer < FADE_DURATION)
            {
                fadeTimer += Time.deltaTime;
                float alpha = 1f - (fadeTimer / FADE_DURATION);
                canvasGroup.alpha = alpha;
                yield return null;
            }
            
            // Destruir la barra de vida
            DestroyHealthBar(enemyId);
        }
        
        /// <summary>
        /// Destruye una barra de vida
        /// </summary>
        private void DestroyHealthBar(string enemyId)
        {
            if (_activeHealthBars.ContainsKey(enemyId))
            {
                GameObject healthBar = _activeHealthBars[enemyId];
                if (healthBar != null)
                {
                    Destroy(healthBar);
                }
                _activeHealthBars.Remove(enemyId);
            }
            
            if (_fadeCoroutines.ContainsKey(enemyId))
            {
                _fadeCoroutines.Remove(enemyId);
            }
        }
        
        /// <summary>
        /// Limpia todas las barras de vida activas
        /// </summary>
        public void ClearAllHealthBars()
        {
            foreach (var kvp in _activeHealthBars)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value);
                }
            }
            _activeHealthBars.Clear();
            
            foreach (var kvp in _fadeCoroutines)
            {
                if (kvp.Value != null)
                {
                    StopCoroutine(kvp.Value);
                }
            }
            _fadeCoroutines.Clear();
        }
    }
    
    /// <summary>
    /// Componente que hace que la barra de vida siga al enemigo
    /// </summary>
    public class EnemyHealthBarFollower : MonoBehaviour
    {
        private GameObject _target;
        private float _offsetY;
        private Camera _camera;
        
        public void Initialize(GameObject target, float offsetY)
        {
            _target = target;
            _offsetY = offsetY;
            _camera = Camera.main;
        }
        
        private void Update()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }
            
            // Obtener el canvas padre para determinar el modo de renderizado
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;
            
            Vector3 targetPos = _target.transform.position + Vector3.up * _offsetY;
            
            if (parentCanvas.renderMode == RenderMode.WorldSpace)
            {
                // En WorldSpace, usar posición mundial directamente
                transform.position = targetPos;
                
                // Hacer que la barra siempre mire hacia la cámara
                if (_camera != null)
                {
                    transform.LookAt(transform.position + _camera.transform.rotation * Vector3.forward,
                                    _camera.transform.rotation * Vector3.up);
                }
            }
            else if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera && parentCanvas.worldCamera != null)
            {
                // En ScreenSpaceCamera, convertir a posición de pantalla
                Vector3 screenPos = parentCanvas.worldCamera.WorldToScreenPoint(targetPos);
                RectTransform rectTransform = GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.position = screenPos;
                }
            }
        }
    }
}