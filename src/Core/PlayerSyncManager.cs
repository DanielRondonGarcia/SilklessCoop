using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Steamworks;
using BepInEx.Logging;
using SilklessCoop.Networking;

namespace SilklessCoop.Core
{
    public static class PlayerSyncManager
    {
        private static ManualLogSource Logger => SilklessCoopPlugin.Logger;
        private static Dictionary<CSteamID, PlayerData> remotePlayers = new Dictionary<CSteamID, PlayerData>();
        private static Dictionary<CSteamID, GameObject> remotePlayerObjects = new Dictionary<CSteamID, GameObject>();
        private static PlayerData localPlayerData = new PlayerData();
        private static float lastSyncTime = 0f;
        private static float syncInterval = 1f / 20f; // 20 Hz
        
        public static bool IsInitialized { get; private set; } = false;
        
        public static void Initialize()
        {
            if (IsInitialized) return;
            
            try
            {
                remotePlayers.Clear();
                remotePlayerObjects.Clear();
                localPlayerData = new PlayerData();
                lastSyncTime = 0f;
                
                // Configurar el intervalo de sincronización basado en la configuración
                if (SilklessCoopPlugin.TickRate != null)
                {
                    syncInterval = 1f / SilklessCoopPlugin.TickRate.Value;
                }
                
                IsInitialized = true;
                Logger.LogInfo("PlayerSyncManager inicializado correctamente.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al inicializar PlayerSyncManager: {ex.Message}");
            }
        }
        
        public static void Shutdown()
        {
            if (!IsInitialized) return;
            
            try
            {
                // Limpiar objetos de jugadores remotos
                foreach (var playerObj in remotePlayerObjects.Values)
                {
                    if (playerObj != null)
                    {
                        UnityEngine.Object.Destroy(playerObj);
                    }
                }
                
                remotePlayers.Clear();
                remotePlayerObjects.Clear();
                localPlayerData = new PlayerData();
                
                IsInitialized = false;
                Logger.LogInfo("PlayerSyncManager desconectado.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al desconectar PlayerSyncManager: {ex.Message}");
            }
        }
        
        public static void Update()
        {
            if (!IsInitialized || !SilklessCoopPlugin.IsMultiplayerEnabled) return;
            
            try
            {
                // Actualizar datos del jugador local
                UpdateLocalPlayerData();
                
                // Enviar datos del jugador local a otros jugadores
                if (Time.time - lastSyncTime >= syncInterval)
                {
                    SendLocalPlayerData();
                    lastSyncTime = Time.time;
                }
                
                // Actualizar posiciones de jugadores remotos
                UpdateRemotePlayerObjects();
                
                // Limpiar jugadores desconectados
                CleanupDisconnectedPlayers();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en PlayerSyncManager.Update: {ex.Message}");
            }
        }
        
        private static void UpdateLocalPlayerData()
        {
            try
            {
                // Buscar el objeto del jugador local
                var heroController = GameObject.FindFirstObjectByType<HeroController>();
                if (heroController == null) 
                {
                    Logger.LogWarning("[DEBUG] HeroController no encontrado en UpdateLocalPlayerData");
                    return;
                }
                
                var transform = heroController.transform;
                var rigidbody = heroController.GetComponent<Rigidbody2D>();
                
                // Actualizar datos básicos
                localPlayerData.steamID = SteamUser.GetSteamID();
                localPlayerData.position = transform.position;
                localPlayerData.velocity = rigidbody != null ? rigidbody.velocity : Vector2.zero;
                localPlayerData.facingRight = transform.localScale.x > 0;
                localPlayerData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                localPlayerData.timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                
                Logger.LogInfo($"[DEBUG] Datos locales actualizados - Pos: {localPlayerData.position}, Escena: {localPlayerData.currentScene}, SteamID: {localPlayerData.steamID}");
                
                // Intentar obtener más datos del estado del jugador
                try
                {
                    // Estos campos pueden no estar disponibles dependiendo del juego
                    // Se manejan con try-catch para evitar errores
                    var playerData = heroController.GetComponent<PlayerData>();
                    if (playerData != null)
                    {
                        // Aquí se podrían obtener más datos específicos del juego
                    }
                }
                catch
                {
                    // Ignorar errores al acceder a componentes específicos del juego
                }
            }
            catch (Exception ex)
            {
                if (SilklessCoopPlugin.PrintDebugOutput?.Value == true)
                {
                    Logger.LogWarning($"Error al actualizar datos del jugador local: {ex.Message}");
                }
            }
        }
        
        private static void SendLocalPlayerData()
        {
            if (!localPlayerData.IsValid()) 
            {
                Logger.LogWarning($"[DEBUG] Datos locales no válidos - SteamID: {localPlayerData.steamID}, Escena: {localPlayerData.currentScene}");
                return;
            }
            
            try
            {
                var data = localPlayerData.ToBytes();
                if (data.Length == 0) 
                {
                    Logger.LogWarning("[DEBUG] Error al serializar datos locales - datos vacíos");
                    return;
                }
                
                var connectedPlayers = SteamNetworkManager.GetConnectedPlayers();
                Logger.LogInfo($"[DEBUG] Enviando datos a {connectedPlayers.Count} jugadores conectados");
                
                foreach (var playerID in connectedPlayers)
                {
                    bool success = SteamNetworkManager.SendPacket(playerID, data, EP2PSend.k_EP2PSendUnreliable);
                    Logger.LogInfo($"[DEBUG] Envío a {playerID}: {(success ? "EXITOSO" : "FALLIDO")}");
                }
                
                if (SilklessCoopPlugin.PrintDebugOutput?.Value == true)
                {
                    Logger.LogInfo($"Datos del jugador enviados a {connectedPlayers.Count} jugadores.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al enviar datos del jugador: {ex.Message}");
            }
        }
        
        public static void OnPlayerDataReceived(CSteamID senderID, byte[] data)
        {
            try
            {
                Logger.LogInfo($"[DEBUG] Datos recibidos de {senderID} - Tamaño: {data.Length} bytes");
                
                var playerData = PlayerData.FromBytes(data);
                if (!playerData.IsValid()) 
                {
                    Logger.LogWarning($"[DEBUG] Datos recibidos no válidos de {senderID} - SteamID: {playerData.steamID}, Escena: {playerData.currentScene}");
                    return;
                }
                
                Logger.LogInfo($"[DEBUG] Datos válidos recibidos de {senderID} - Pos: {playerData.position}, Escena: {playerData.currentScene}");
                
                // Actualizar datos del jugador remoto
                remotePlayers[senderID] = playerData;
                
                // Crear o actualizar objeto visual del jugador
                UpdateRemotePlayerObject(senderID, playerData);
                
                if (SilklessCoopPlugin.PrintDebugOutput?.Value == true)
                {
                    Logger.LogInfo($"Datos recibidos del jugador {senderID}: {playerData.position}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al procesar datos del jugador recibidos: {ex.Message}");
            }
        }
        
        private static void UpdateRemotePlayerObject(CSteamID playerID, PlayerData data)
        {
            try
            {
                // Verificar si estamos en la misma escena
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                Logger.LogInfo($"[DEBUG] Actualizando objeto remoto {playerID} - Escena actual: {currentScene}, Escena jugador: {data.currentScene}");
                
                if (data.currentScene != currentScene)
                {
                    Logger.LogInfo($"[DEBUG] Jugador {playerID} está en escena diferente - ocultando objeto");
                    // Ocultar jugador si está en otra escena
                    if (remotePlayerObjects.ContainsKey(playerID))
                    {
                        remotePlayerObjects[playerID].SetActive(false);
                    }
                    return;
                }
                
                GameObject playerObj;
                if (!remotePlayerObjects.ContainsKey(playerID))
                {
                    Logger.LogInfo($"[DEBUG] Creando nuevo objeto para jugador remoto {playerID}");
                    // Crear nuevo objeto para el jugador remoto
                    playerObj = CreateRemotePlayerObject(playerID, data);
                    remotePlayerObjects[playerID] = playerObj;
                    Logger.LogInfo($"[DEBUG] Objeto creado para {playerID}: {(playerObj != null ? "EXITOSO" : "FALLIDO")}");
                }
                else
                {
                    Logger.LogInfo($"[DEBUG] Actualizando objeto existente para {playerID}");
                    playerObj = remotePlayerObjects[playerID];
                    playerObj.SetActive(true);
                }
                
                // Actualizar posición y estado
                if (playerObj != null)
                {
                    playerObj.transform.position = data.position;
                    
                    // Actualizar escala para la dirección
                    var scale = playerObj.transform.localScale;
                    scale.x = data.facingRight ? Mathf.Abs(scale.x) : -Mathf.Abs(scale.x);
                    playerObj.transform.localScale = scale;
                    
                    // Actualizar transparencia
                    var renderer = playerObj.GetComponent<SpriteRenderer>();
                    if (renderer != null && SilklessCoopPlugin.PlayerOpacity != null)
                    {
                        var color = renderer.color;
                        color.a = SilklessCoopPlugin.PlayerOpacity.Value;
                        renderer.color = color;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al actualizar objeto del jugador remoto: {ex.Message}");
            }
        }
        
        private static GameObject CreateRemotePlayerObject(CSteamID playerID, PlayerData data)
        {
            try
            {
                Logger.LogInfo($"[DEBUG] Iniciando creación de objeto remoto para {playerID}");
                
                // Buscar el objeto del jugador local como referencia
                var heroController = GameObject.FindFirstObjectByType<HeroController>();
                if (heroController == null)
                {
                    Logger.LogWarning($"[DEBUG] No se encontró HeroController - creando objeto simple para {playerID}");
                    return CreateSimplePlayerObject(playerID, data);
                }
                
                Logger.LogInfo($"[DEBUG] HeroController encontrado - creando objeto completo para {playerID}");
                
                // Crear una copia simplificada del jugador
                var playerObj = new GameObject($"RemotePlayer_{playerID}");
                playerObj.transform.position = data.position;
                Logger.LogInfo($"[DEBUG] GameObject creado en posición {data.position}");
                
                // Buscar el sprite renderer del jugador local (puede estar en el objeto o en hijos)
                var localRenderer = heroController.GetComponent<SpriteRenderer>();
                if (localRenderer == null)
                {
                    Logger.LogInfo($"[DEBUG] SpriteRenderer no encontrado en HeroController, buscando en hijos");
                    localRenderer = heroController.GetComponentInChildren<SpriteRenderer>();
                }
                
                if (localRenderer != null)
                {
                    Logger.LogInfo($"[DEBUG] SpriteRenderer encontrado - Sprite: {localRenderer.sprite?.name}, Layer: {localRenderer.sortingLayerName}");
                    var renderer = playerObj.AddComponent<SpriteRenderer>();
                    renderer.sprite = localRenderer.sprite;
                    renderer.color = data.playerColor;
                    renderer.sortingLayerName = localRenderer.sortingLayerName;
                    renderer.sortingOrder = localRenderer.sortingOrder - 1; // Detrás del jugador local
                    
                    // Asegurar que el sprite sea visible
                    renderer.enabled = true;
                    
                    Logger.LogInfo($"[DEBUG] SpriteRenderer configurado - Color: {data.playerColor}, Sprite: {localRenderer.sprite?.name}, Enabled: {renderer.enabled}");
                }
                else
                {
                    Logger.LogWarning($"[DEBUG] No se encontró SpriteRenderer en HeroController ni en sus hijos - creando sprite por defecto");
                    
                    // Crear un sprite simple como fallback
                    var renderer = playerObj.AddComponent<SpriteRenderer>();
                    
                    // Crear un sprite simple (cuadrado blanco)
                    var texture = new Texture2D(32, 32);
                    for (int x = 0; x < 32; x++)
                    {
                        for (int y = 0; y < 32; y++)
                        {
                            texture.SetPixel(x, y, Color.white);
                        }
                    }
                    texture.Apply();
                    
                    var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
                    renderer.sprite = sprite;
                    renderer.color = data.playerColor;
                    renderer.sortingOrder = 100; // Asegurar que sea visible
                    
                    Logger.LogInfo($"[DEBUG] Sprite por defecto creado - Color: {data.playerColor}");
                }
                
                // Agregar indicador de color si está habilitado
                if (SilklessCoopPlugin.ShowPlayerColorPins?.Value == true)
                {
                    Logger.LogInfo($"[DEBUG] Creando pin de color para {playerID}");
                    CreatePlayerColorPin(playerObj, data.playerColor);
                }
                
                Logger.LogInfo($"[DEBUG] Objeto de jugador remoto creado exitosamente para {playerID}");
                return playerObj;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al crear objeto de jugador remoto: {ex.Message}");
                return CreateSimplePlayerObject(playerID, data);
            }
        }
        
        private static GameObject CreateSimplePlayerObject(CSteamID playerID, PlayerData data)
        {
            Logger.LogInfo($"[DEBUG] Creando objeto simple para {playerID}");
            
            var playerObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObj.name = $"RemotePlayer_{playerID}";
            playerObj.transform.position = data.position;
            playerObj.transform.localScale = new Vector3(0.8f, 1.5f, 0.8f); // Más grande y visible
            
            var renderer = playerObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Crear un material más brillante
                var material = new Material(Shader.Find("Sprites/Default"));
                material.color = data.playerColor;
                renderer.material = material;
                renderer.sortingOrder = 100; // Asegurar que sea visible
                
                Logger.LogInfo($"[DEBUG] Material configurado - Color: {data.playerColor}");
            }
            
            // Remover collider para evitar interferencias
            var collider = playerObj.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }
            
            Logger.LogInfo($"[DEBUG] Objeto simple creado exitosamente en posición {data.position}");
            return playerObj;
        }
        
        private static void CreatePlayerColorPin(GameObject playerObj, Color color)
        {
            try
            {
                var pinObj = new GameObject("ColorPin");
                pinObj.transform.SetParent(playerObj.transform);
                pinObj.transform.localPosition = new Vector3(0, 2f, 0); // Encima del jugador
                
                var renderer = pinObj.AddComponent<SpriteRenderer>();
                
                // Crear un sprite simple para el pin
                var texture = new Texture2D(16, 16);
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(8, 8));
                        texture.SetPixel(x, y, distance <= 6 ? color : Color.clear);
                    }
                }
                texture.Apply();
                
                var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
                renderer.sprite = sprite;
                renderer.sortingOrder = 100; // Encima de todo
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al crear pin de color: {ex.Message}");
            }
        }
        
        private static void UpdateRemotePlayerObjects()
        {
            // Interpolación suave de posiciones para jugadores remotos
            foreach (var kvp in remotePlayerObjects)
            {
                var playerID = kvp.Key;
                var playerObj = kvp.Value;
                
                if (playerObj == null || !remotePlayers.ContainsKey(playerID)) continue;
                
                var data = remotePlayers[playerID];
                
                // Verificar si los datos no son muy antiguos
                if (data.GetAge() > 5f) // 5 segundos
                {
                    playerObj.SetActive(false);
                    continue;
                }
                
                // Interpolación suave hacia la posición objetivo
                var targetPos = data.position;
                var currentPos = playerObj.transform.position;
                var lerpSpeed = 10f * Time.deltaTime;
                
                playerObj.transform.position = Vector3.Lerp(currentPos, targetPos, lerpSpeed);
            }
        }
        
        private static void CleanupDisconnectedPlayers()
        {
            var connectedPlayers = SteamNetworkManager.GetConnectedPlayers();
            var playersToRemove = new List<CSteamID>();
            
            foreach (var playerID in remotePlayers.Keys)
            {
                if (!connectedPlayers.Contains(playerID))
                {
                    playersToRemove.Add(playerID);
                }
            }
            
            foreach (var playerID in playersToRemove)
            {
                RemovePlayer(playerID);
            }
        }
        
        public static void RemovePlayer(CSteamID playerID)
        {
            try
            {
                if (remotePlayerObjects.ContainsKey(playerID))
                {
                    var playerObj = remotePlayerObjects[playerID];
                    if (playerObj != null)
                    {
                        UnityEngine.Object.Destroy(playerObj);
                    }
                    remotePlayerObjects.Remove(playerID);
                }
                
                if (remotePlayers.ContainsKey(playerID))
                {
                    remotePlayers.Remove(playerID);
                }
                
                Logger.LogInfo($"Jugador {playerID} removido del sistema de sincronización.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al remover jugador: {ex.Message}");
            }
        }
        
        public static List<CSteamID> GetConnectedPlayerIDs()
        {
            return new List<CSteamID>(remotePlayers.Keys);
        }
        
        public static PlayerData GetPlayerData(CSteamID playerID)
        {
            return remotePlayers.ContainsKey(playerID) ? remotePlayers[playerID] : null;
        }
        
        public static PlayerData GetLocalPlayerData()
        {
            return localPlayerData;
        }
    }
}