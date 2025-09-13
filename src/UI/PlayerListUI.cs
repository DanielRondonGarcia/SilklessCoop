using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using SilklessCoop.Core;
using BepInEx.Logging;

namespace SilklessCoop.UI
{
    /// <summary>
    /// UI para mostrar la lista de jugadores conectados
    /// </summary>
    public class PlayerListUI : MonoBehaviour
    {
        private static ManualLogSource Logger => SilklessCoopPlugin.Logger;
        
        private GameObject playerListPanel;
        private Transform playerListContent;
        private bool isVisible = false;
        
        private Dictionary<CSteamID, GameObject> playerEntries = new Dictionary<CSteamID, GameObject>();
        
        private void Start()
        {
            CreatePlayerListUI();
            
            // Suscribirse a eventos de red
            NetworkEventManager.OnPlayerConnected += OnPlayerConnected;
            NetworkEventManager.OnPlayerDisconnected += OnPlayerDisconnected;
            
            // Suscribirse a eventos de lobby
            LobbyManager.OnPlayerJoinedLobby += OnPlayerJoinedLobby;
            LobbyManager.OnPlayerLeftLobby += OnPlayerLeftLobby;
        }
        
        private void OnDestroy()
        {
            // Desuscribirse de eventos
            NetworkEventManager.OnPlayerConnected -= OnPlayerConnected;
            NetworkEventManager.OnPlayerDisconnected -= OnPlayerDisconnected;
            LobbyManager.OnPlayerJoinedLobby -= OnPlayerJoinedLobby;
            LobbyManager.OnPlayerLeftLobby -= OnPlayerLeftLobby;
        }
        
        private void Update()
        {
            // Alternar visibilidad con Tab
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleVisibility();
            }
        }
        
        private void CreatePlayerListUI()
        {
            try
            {
                // Buscar o crear Canvas
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null)
                {
                    GameObject canvasObj = new GameObject("PlayerListCanvas");
                    canvas = canvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasObj.AddComponent<CanvasScaler>();
                    canvasObj.AddComponent<GraphicRaycaster>();
                }
                
                // Crear panel principal
                playerListPanel = new GameObject("PlayerListPanel");
                playerListPanel.transform.SetParent(canvas.transform, false);
                
                var panelRect = playerListPanel.AddComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(0.02f, 0.7f);
                panelRect.anchorMax = new Vector2(0.3f, 0.98f);
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
                
                var panelImage = playerListPanel.AddComponent<Image>();
                panelImage.color = new Color(0, 0, 0, 0.8f);
                
                // Crear título
                GameObject titleObj = new GameObject("Title");
                titleObj.transform.SetParent(playerListPanel.transform, false);
                
                var titleRect = titleObj.AddComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0, 0.85f);
                titleRect.anchorMax = new Vector2(1, 1);
                titleRect.offsetMin = Vector2.zero;
                titleRect.offsetMax = Vector2.zero;
                
                var titleText = titleObj.AddComponent<Text>();
                titleText.text = "Jugadores Conectados";
                titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                titleText.fontSize = 16;
                titleText.color = Color.white;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.fontStyle = FontStyle.Bold;
                
                // Crear área de scroll
                GameObject scrollObj = new GameObject("ScrollArea");
                scrollObj.transform.SetParent(playerListPanel.transform, false);
                
                var scrollRect = scrollObj.AddComponent<RectTransform>();
                scrollRect.anchorMin = new Vector2(0, 0);
                scrollRect.anchorMax = new Vector2(1, 0.85f);
                scrollRect.offsetMin = Vector2.zero;
                scrollRect.offsetMax = Vector2.zero;
                
                var scrollView = scrollObj.AddComponent<ScrollRect>();
                scrollView.horizontal = false;
                scrollView.vertical = true;
                
                // Crear contenido del scroll
                GameObject contentObj = new GameObject("Content");
                contentObj.transform.SetParent(scrollObj.transform, false);
                
                playerListContent = contentObj.transform;
                var contentRect = contentObj.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.sizeDelta = new Vector2(0, 0);
                
                var contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
                contentLayout.childAlignment = TextAnchor.UpperCenter;
                contentLayout.childControlHeight = false;
                contentLayout.childControlWidth = true;
                contentLayout.childForceExpandHeight = false;
                contentLayout.childForceExpandWidth = true;
                contentLayout.spacing = 5;
                contentLayout.padding = new RectOffset(10, 10, 10, 10);
                
                var contentSizeFitter = contentObj.AddComponent<ContentSizeFitter>();
                contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                
                scrollView.content = contentRect;
                
                // Inicialmente oculto
                playerListPanel.SetActive(false);
                
                Logger.LogInfo("PlayerListUI creada exitosamente.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al crear PlayerListUI: {ex.Message}");
            }
        }
        
        private void ToggleVisibility()
        {
            if (playerListPanel != null)
            {
                isVisible = !isVisible;
                playerListPanel.SetActive(isVisible);
                
                if (isVisible)
                {
                    RefreshPlayerList();
                }
            }
        }
        
        private void RefreshPlayerList()
        {
            try
            {
                // Limpiar entradas existentes
                foreach (var entry in playerEntries.Values)
                {
                    if (entry != null)
                        Destroy(entry);
                }
                playerEntries.Clear();
                
                // Agregar jugador local
                CreatePlayerEntry(SteamUser.GetSteamID(), "Tú", true);
                
                // Agregar jugadores conectados
                var connectedPlayers = NetworkEventManager.GetConnectedPlayersWithNames();
                foreach (var player in connectedPlayers)
                {
                    CreatePlayerEntry(player.Key, player.Value, false);
                }
                
                // Agregar miembros del lobby si estamos en uno
                if (LobbyManager.IsInLobby)
                {
                    var lobbyMembers = LobbyManager.GetLobbyMembers();
                    foreach (var member in lobbyMembers)
                    {
                        if (member != SteamUser.GetSteamID() && !connectedPlayers.ContainsKey(member))
                        {
                            string memberName = SteamFriends.GetFriendPersonaName(member);
                            CreatePlayerEntry(member, $"{memberName} (Lobby)", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al refrescar lista de jugadores: {ex.Message}");
            }
        }
        
        private void CreatePlayerEntry(CSteamID steamID, string playerName, bool isLocal)
        {
            try
            {
                GameObject entryObj = new GameObject($"PlayerEntry_{steamID}");
                entryObj.transform.SetParent(playerListContent, false);
                
                var entryRect = entryObj.AddComponent<RectTransform>();
                entryRect.sizeDelta = new Vector2(0, 30);
                
                var entryImage = entryObj.AddComponent<Image>();
                entryImage.color = isLocal ? new Color(0.2f, 0.6f, 0.2f, 0.5f) : new Color(0.2f, 0.2f, 0.6f, 0.5f);
                
                GameObject textObj = new GameObject("PlayerName");
                textObj.transform.SetParent(entryObj.transform, false);
                
                var textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 0);
                textRect.offsetMax = new Vector2(-10, 0);
                
                var text = textObj.AddComponent<Text>();
                text.text = playerName;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleLeft;
                
                // Agregar información adicional si no es local
                if (!isLocal)
                {
                    var sessionDuration = NetworkEventManager.GetPlayerSessionDuration(steamID);
                    if (sessionDuration.HasValue)
                    {
                        text.text += $" ({sessionDuration.Value:mm\\:ss})";
                    }
                }
                
                playerEntries[steamID] = entryObj;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al crear entrada de jugador {steamID}: {ex.Message}");
            }
        }
        
        // Event handlers
        private void OnPlayerConnected(CSteamID steamID)
        {
            if (isVisible)
            {
                RefreshPlayerList();
            }
        }
        
        private void OnPlayerDisconnected(CSteamID steamID)
        {
            if (isVisible)
            {
                RefreshPlayerList();
            }
        }
        
        private void OnPlayerJoinedLobby(CSteamID steamID, string playerName)
        {
            if (isVisible)
            {
                RefreshPlayerList();
            }
        }
        
        private void OnPlayerLeftLobby(CSteamID steamID, string playerName)
        {
            if (isVisible)
            {
                RefreshPlayerList();
            }
        }
    }
}