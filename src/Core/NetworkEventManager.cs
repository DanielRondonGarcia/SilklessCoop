using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using BepInEx.Logging;

namespace SilklessCoop.Core
{
    /// <summary>
    /// Maneja eventos de red como conexión y desconexión de jugadores
    /// </summary>
    public static class NetworkEventManager
    {
        private static ManualLogSource Logger => SilklessCoopPlugin.Logger;
        
        // Eventos públicos para que otros sistemas puedan suscribirse
        public static event Action<CSteamID> OnPlayerConnected;
        public static event Action<CSteamID> OnPlayerDisconnected;
        public static event Action<CSteamID, string> OnPlayerJoinedLobby;
        public static event Action<CSteamID, string> OnPlayerLeftLobby;
        
        private static Dictionary<CSteamID, DateTime> connectedPlayers = new Dictionary<CSteamID, DateTime>();
        private static Dictionary<CSteamID, string> playerNames = new Dictionary<CSteamID, string>();
        
        public static void Initialize()
        {
            Logger.LogInfo("Inicializando NetworkEventManager...");
            
            // Limpiar datos previos
            connectedPlayers.Clear();
            playerNames.Clear();
            
            Logger.LogInfo("NetworkEventManager inicializado correctamente.");
        }
        
        public static void Shutdown()
        {
            Logger.LogInfo("Cerrando NetworkEventManager...");
            
            // Limpiar eventos y datos
            OnPlayerConnected = null;
            OnPlayerDisconnected = null;
            OnPlayerJoinedLobby = null;
            OnPlayerLeftLobby = null;
            
            connectedPlayers.Clear();
            playerNames.Clear();
            
            Logger.LogInfo("NetworkEventManager cerrado correctamente.");
        }
        
        /// <summary>
        /// Notifica que un jugador se ha conectado
        /// </summary>
        public static void NotifyPlayerConnected(CSteamID steamID)
        {
            try
            {
                string playerName = SteamFriends.GetFriendPersonaName(steamID);
                
                if (!connectedPlayers.ContainsKey(steamID))
                {
                    connectedPlayers[steamID] = DateTime.Now;
                    playerNames[steamID] = playerName;
                    
                    Logger.LogInfo($"Jugador conectado: {playerName} ({steamID})");
                    
                    // Mostrar mensaje en pantalla
                    SilklessCoopPlugin.ShowOnScreenMessage($"Jugador conectado: {playerName}", 3f);
                    
                    // Disparar evento
                    OnPlayerConnected?.Invoke(steamID);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al notificar conexión de jugador {steamID}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Notifica que un jugador se ha desconectado
        /// </summary>
        public static void NotifyPlayerDisconnected(CSteamID steamID)
        {
            try
            {
                if (connectedPlayers.ContainsKey(steamID))
                {
                    string playerName = playerNames.ContainsKey(steamID) ? playerNames[steamID] : steamID.ToString();
                    DateTime connectionTime = connectedPlayers[steamID];
                    TimeSpan sessionDuration = DateTime.Now - connectionTime;
                    
                    Logger.LogInfo($"Jugador desconectado: {playerName} ({steamID}) - Duración de sesión: {sessionDuration:mm\\:ss}");
                    
                    // Mostrar mensaje en pantalla
                    SilklessCoopPlugin.ShowOnScreenMessage($"Jugador desconectado: {playerName}", 3f);
                    
                    // Limpiar datos del jugador
                    connectedPlayers.Remove(steamID);
                    playerNames.Remove(steamID);
                    
                    // Disparar evento
                    OnPlayerDisconnected?.Invoke(steamID);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al notificar desconexión de jugador {steamID}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene el nombre de un jugador conectado
        /// </summary>
        public static string GetPlayerName(CSteamID steamID)
        {
            if (playerNames.ContainsKey(steamID))
            {
                return playerNames[steamID];
            }
            
            // Intentar obtener el nombre de Steam
            try
            {
                return SteamFriends.GetFriendPersonaName(steamID);
            }
            catch
            {
                return steamID.ToString();
            }
        }
        
        /// <summary>
        /// Obtiene la duración de la sesión de un jugador
        /// </summary>
        public static TimeSpan? GetPlayerSessionDuration(CSteamID steamID)
        {
            if (connectedPlayers.ContainsKey(steamID))
            {
                return DateTime.Now - connectedPlayers[steamID];
            }
            return null;
        }
        
        /// <summary>
        /// Obtiene la lista de jugadores conectados con sus nombres
        /// </summary>
        public static Dictionary<CSteamID, string> GetConnectedPlayersWithNames()
        {
            return new Dictionary<CSteamID, string>(playerNames);
        }
        
        /// <summary>
        /// Verifica si un jugador está conectado
        /// </summary>
        public static bool IsPlayerConnected(CSteamID steamID)
        {
            return connectedPlayers.ContainsKey(steamID);
        }
    }
}