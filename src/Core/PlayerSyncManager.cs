using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Steamworks;
using BepInEx.Logging;
using SilklessCoop.Networking;
using SilklessCoop.Core;

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
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                
                GameObject playerObj;
                if (!remotePlayerObjects.ContainsKey(playerID) || remotePlayerObjects[playerID] == null)
                {
                    playerObj = CreateRemotePlayerObject(playerID, data);
                    if (playerObj == null) return; // Could not create the object
                    remotePlayerObjects[playerID] = playerObj;
                }
                else
                {
                    playerObj = remotePlayerObjects[playerID];
                }

                var avatar = playerObj.GetComponent<PlayerAvatar>();
                if (avatar != null)
                {
                    // Show/hide based on scene
                    if (data.currentScene != currentScene)
                    {
                        avatar.SetVisible(false);
                        return;
                    }

                    avatar.SetVisible(true);
                    avatar.UpdateState(data.position, data.facingRight);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating remote player object: {ex.Message}");
            }
        }
        
        private static GameObject CreateRemotePlayerObject(CSteamID playerID, PlayerData data)
        {
            try
            {
                var heroController = GameObject.FindFirstObjectByType<HeroController>();
                if (heroController == null)
                {
                    Logger.LogWarning($"[DEBUG] HeroController not found, cannot create remote player object.");
                    return null;
                }

                // Find the local player's tk2dSprite component
                var localSprite = heroController.GetComponentInChildren<tk2dSprite>(true);
                if (localSprite == null)
                {
                    Logger.LogWarning($"[DEBUG] tk2dSprite not found on local player, cannot create remote player object.");
                    return null;
                }
                
                var playerObj = new GameObject($"RemotePlayer_{playerID}");
                playerObj.transform.position = data.position;

                // Add and initialize the PlayerAvatar component
                var avatar = playerObj.AddComponent<PlayerAvatar>();
                avatar.Initialize(playerID);

                // Copy the sprite from the local player
                var remoteSprite = avatar.GetSprite();
                if (remoteSprite != null)
                {
                    remoteSprite.CopyFrom(localSprite);
                    var color = remoteSprite.color;
                    if (SilklessCoopPlugin.PlayerOpacity != null)
                    {
                        color.a = SilklessCoopPlugin.PlayerOpacity.Value;
                    }
                    remoteSprite.color = color;
                    remoteSprite.Build();
                }

                Logger.LogInfo($"[DEBUG] Remote player object created successfully for {playerID}");
                return playerObj;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating remote player object: {ex.Message}");
                return null;
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