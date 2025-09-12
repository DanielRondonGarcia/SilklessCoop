// Sistema optimizado de sincronización de nombres de usuario para arquitectura Host-Cliente
// Integra con el WorldState para máximo rendimiento y consistencia

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;

namespace SilksongMultiplayer
{
    /// <summary>
    /// Sistema optimizado de sincronización de nombres de usuario
    /// Integrado con la arquitectura Host-Cliente para máximo rendimiento
    /// </summary>
    internal class OptimizedUsernameSync : MonoBehaviour
    {
        // Referencias principales
        private Transform cachedTransform;
        private Dictionary<string, PlayerNameUI> playerNameUIs = new Dictionary<string, PlayerNameUI>();
        
        // Configuración de rendimiento
        private const float UI_UPDATE_INTERVAL = 0.1f; // 10 FPS para UI
        private const float NAME_VISIBILITY_DISTANCE = 30f;
        private const float NAME_FADE_DISTANCE = 25f;
        
        // Cache de datos
        private float lastUIUpdate = 0f;
        private Vector3 lastCameraPosition;
        private Camera mainCamera;
        
        // Colores especiales para usuarios específicos
        private readonly Dictionary<ulong, Color> specialUserColors = new Dictionary<ulong, Color>
        {
            { 76561198929282998, Color.yellow },
            { 76561199835946204, Color.yellow }
        };
        
        // Pool de colores para jugadores
        private readonly Color[] playerColors = {
            new Color(1.0f, 0.2f, 0.2f, 0.9f), // Rojo
            new Color(0.2f, 0.8f, 0.2f, 0.9f), // Verde
            new Color(0.2f, 0.4f, 1.0f, 0.9f), // Azul
            new Color(1.0f, 0.8f, 0.2f, 0.9f), // Amarillo
            new Color(1.0f, 0.4f, 0.8f, 0.9f), // Rosa
            new Color(0.6f, 0.2f, 1.0f, 0.9f), // Púrpura
            new Color(0.2f, 0.8f, 0.8f, 0.9f), // Cian
            new Color(1.0f, 0.6f, 0.2f, 0.9f)  // Naranja
        };
        private int nextColorIndex = 0;
        
        private void Start()
        {
            cachedTransform = transform;
            mainCamera = Camera.main;
            
            // Configurar nombre local
            SetupLocalPlayerName();
        }
        
        private void Update()
        {
            // Actualizar UI de nombres con intervalo optimizado
            if (Time.time - lastUIUpdate >= UI_UPDATE_INTERVAL)
            {
                UpdatePlayerNamesUI();
                lastUIUpdate = Time.time;
            }
        }
        
        /// <summary>
        /// Configura el nombre del jugador local
        /// </summary>
        private void SetupLocalPlayerName()
        {
            string localPlayerName = SteamFriends.GetPersonaName();
            ulong localSteamId = SteamUser.GetSteamID().m_SteamID;
            
            // Determinar color del jugador local
            Color nameColor = specialUserColors.ContainsKey(localSteamId) 
                ? specialUserColors[localSteamId] 
                : Color.white;
            
            CreatePlayerNameUI(localSteamId.ToString(), localPlayerName, nameColor, cachedTransform.position, true);
        }
        
        /// <summary>
        /// Crea o actualiza la UI de nombre para un jugador
        /// </summary>
        public void CreatePlayerNameUI(string playerId, string displayName, Color nameColor, Vector3 worldPosition, bool isLocal = false)
        {
            if (playerNameUIs.ContainsKey(playerId))
            {
                // Actualizar UI existente
                UpdatePlayerNameUI(playerId, displayName, nameColor, worldPosition);
                return;
            }
            
            // Crear nueva UI de nombre
            GameObject nameContainer = new GameObject($"PlayerName_{playerId}");
            nameContainer.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            
            if (isLocal)
            {
                nameContainer.transform.SetParent(cachedTransform);
            }
            
            // Configurar Canvas
            Canvas canvas = nameContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "HUD";
            canvas.sortingLayerID = 629535577;
            canvas.sortingOrder = 50;
            
            RectTransform canvasRect = nameContainer.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2560f, 1440f);
            
            // Crear texto
            GameObject textObject = new GameObject("NameText");
            textObject.transform.SetParent(nameContainer.transform);
            textObject.AddComponent<CanvasRenderer>();
            textObject.transform.localScale = Vector3.one * 0.01f;
            
            Text nameText = textObject.AddComponent<Text>();
            nameText.text = displayName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 50;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = nameColor;
            
            // Crear estructura de datos
            var playerNameUI = new PlayerNameUI
            {
                PlayerId = playerId,
                DisplayName = displayName,
                NameColor = nameColor,
                Container = nameContainer,
                TextComponent = nameText,
                Canvas = canvas,
                IsLocal = isLocal,
                LastUpdateTime = Time.time
            };
            
            playerNameUIs[playerId] = playerNameUI;
        }
        
        /// <summary>
        /// Actualiza la UI de nombre de un jugador existente
        /// </summary>
        public void UpdatePlayerNameUI(string playerId, string displayName, Color nameColor, Vector3 worldPosition)
        {
            if (!playerNameUIs.ContainsKey(playerId)) return;
            
            var nameUI = playerNameUIs[playerId];
            
            // Actualizar posición si no es local
            if (!nameUI.IsLocal)
            {
                nameUI.Container.transform.position = worldPosition;
            }
            
            // Actualizar texto y color si han cambiado
            if (nameUI.DisplayName != displayName)
            {
                nameUI.DisplayName = displayName;
                nameUI.TextComponent.text = displayName;
            }
            
            if (nameUI.NameColor != nameColor)
            {
                nameUI.NameColor = nameColor;
                nameUI.TextComponent.color = nameColor;
            }
            
            nameUI.LastUpdateTime = Time.time;
        }
        
        /// <summary>
        /// Actualiza la visibilidad y posición de todas las UI de nombres
        /// </summary>
        private void UpdatePlayerNamesUI()
        {
            if (mainCamera == null) return;
            
            Vector3 cameraPosition = mainCamera.transform.position;
            
            foreach (var nameUI in playerNameUIs.Values)
            {
                if (nameUI.Container == null) continue;
                
                // Calcular distancia a la cámara
                float distance = Vector3.Distance(cameraPosition, nameUI.Container.transform.position);
                
                // Determinar visibilidad basada en distancia
                bool shouldBeVisible = distance <= NAME_VISIBILITY_DISTANCE;
                
                if (nameUI.Canvas.enabled != shouldBeVisible)
                {
                    nameUI.Canvas.enabled = shouldBeVisible;
                }
                
                // Aplicar fade basado en distancia
                if (shouldBeVisible && distance > NAME_FADE_DISTANCE)
                {
                    float alpha = Mathf.Lerp(1f, 0.3f, (distance - NAME_FADE_DISTANCE) / (NAME_VISIBILITY_DISTANCE - NAME_FADE_DISTANCE));
                    Color currentColor = nameUI.NameColor;
                    currentColor.a = alpha;
                    nameUI.TextComponent.color = currentColor;
                }
                else if (shouldBeVisible)
                {
                    nameUI.TextComponent.color = nameUI.NameColor;
                }
            }
        }
        
        /// <summary>
        /// Elimina la UI de nombre de un jugador
        /// </summary>
        public void RemovePlayerNameUI(string playerId)
        {
            if (!playerNameUIs.ContainsKey(playerId)) return;
            
            var nameUI = playerNameUIs[playerId];
            if (nameUI.Container != null)
            {
                Destroy(nameUI.Container);
            }
            
            playerNameUIs.Remove(playerId);
        }
        
        /// <summary>
        /// Obtiene el siguiente color disponible para un nuevo jugador
        /// </summary>
        public Color GetNextPlayerColor()
        {
            Color color = playerColors[nextColorIndex % playerColors.Length];
            nextColorIndex++;
            return color;
        }
        
        /// <summary>
        /// Aplica datos de nombres de usuario desde WorldState (para clientes)
        /// </summary>
        public void ApplyPlayerNameData(Dictionary<string, SilklessCoop.NetworkPlayerNameData> playerNameData)
        {
            foreach (var kvp in playerNameData)
            {
                var nameData = kvp.Value;
                CreatePlayerNameUI(nameData.PlayerId, nameData.DisplayName, nameData.NameColor, nameData.WorldPosition);
            }
        }
        
        /// <summary>
        /// Recopila datos de nombres de usuario para WorldState (para host)
        /// </summary>
        public Dictionary<string, SilklessCoop.NetworkPlayerNameData> CollectPlayerNameData()
        {
            var result = new Dictionary<string, SilklessCoop.NetworkPlayerNameData>();
            
            foreach (var kvp in playerNameUIs)
            {
                var nameUI = kvp.Value;
                var nameData = new SilklessCoop.NetworkPlayerNameData(
                    nameUI.PlayerId,
                    nameUI.DisplayName,
                    nameUI.NameColor,
                    nameUI.Container.transform.position
                );
                
                result[kvp.Key] = nameData;
            }
            
            return result;
        }
        
        /// <summary>
        /// Limpia todas las UI de nombres
        /// </summary>
        public void ClearAllPlayerNames()
        {
            foreach (var nameUI in playerNameUIs.Values)
            {
                if (nameUI.Container != null)
                {
                    Destroy(nameUI.Container);
                }
            }
            
            playerNameUIs.Clear();
        }
    }
    
    /// <summary>
    /// Estructura de datos para UI de nombres de jugador
    /// </summary>
    internal class PlayerNameUI
    {
        public string PlayerId;
        public string DisplayName;
        public Color NameColor;
        public GameObject Container;
        public Text TextComponent;
        public Canvas Canvas;
        public bool IsLocal;
        public float LastUpdateTime;
    }
}