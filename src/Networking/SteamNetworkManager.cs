using System;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using BepInEx.Logging;
using SilklessCoop.Core;

namespace SilklessCoop.Networking
{
    public class SteamNetworkManager
    {
        private static ManualLogSource Logger => SilklessCoopPlugin.Logger;
        private static bool isInitialized = false;
        private static bool isConnected = false;
        private static List<CSteamID> connectedPlayers = new List<CSteamID>();
        
        // Callbacks de Steam
        private static Callback<P2PSessionRequest_t> p2pSessionRequestCallback;
        private static Callback<P2PSessionConnectFail_t> p2pSessionConnectFailCallback;
        
        public static bool IsInitialized => isInitialized;
        public static bool IsConnected => isConnected;
        public static int ConnectedPlayersCount => connectedPlayers.Count;
        
        public static bool Initialize()
        {
            try
            {
                if (isInitialized)
                {
                    Logger.LogWarning("Steam Network Manager ya está inicializado.");
                    return true;
                }
                
                // Verificar que Steam esté ejecutándose
                if (!SteamAPI.IsSteamRunning())
                {
                    Logger.LogError("Steam no está ejecutándose. No se puede inicializar el networking.");
                    return false;
                }
                
                // Verificar si Steam API ya está inicializado por el juego
                // El juego debería haber inicializado Steam API automáticamente
                try
                {
                    // Intentar obtener el Steam ID del usuario para verificar que Steam está funcionando
                    var steamID = SteamUser.GetSteamID();
                    if (steamID == CSteamID.Nil)
                    {
                        Logger.LogError("No se pudo obtener Steam ID del usuario.");
                        return false;
                    }
                    Logger.LogInfo($"Steam ID del usuario: {steamID}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error al verificar Steam API: {ex.Message}");
                    return false;
                }
                
                // Configurar callbacks
                p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
                p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);
                
                // Inicializar el sistema de sincronización de jugadores
                PlayerSyncManager.Initialize();
                
                // Inicializar sistema de eventos de red
                NetworkEventManager.Initialize();
                
                // Inicializar sistema de lobby
                LobbyManager.Initialize();
                
                // Suscribirse a eventos del lobby para conectar automáticamente a jugadores
                LobbyManager.OnPlayerJoinedLobby += OnPlayerJoinedLobby;
                LobbyManager.OnPlayerLeftLobby += OnPlayerLeftLobby;
                
                isInitialized = true;
                Logger.LogInfo("Steam Network Manager inicializado correctamente.");
                
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al inicializar Steam Network Manager: {ex.Message}");
                return false;
            }
        }
        
        public static void Shutdown()
        {
            try
            {
                if (!isInitialized)
                    return;
                
                // Desconectar de todos los jugadores
                DisconnectFromAllPlayers();
                
                // Desuscribirse de eventos del lobby
                LobbyManager.OnPlayerJoinedLobby -= OnPlayerJoinedLobby;
                LobbyManager.OnPlayerLeftLobby -= OnPlayerLeftLobby;
                
                // Limpiar callbacks
                p2pSessionRequestCallback?.Dispose();
                p2pSessionConnectFailCallback?.Dispose();
                
                // Desconectar el sistema de sincronización de jugadores
                PlayerSyncManager.Shutdown();
                
                // Cerrar sistema de eventos de red
                NetworkEventManager.Shutdown();
                
                // Cerrar sistema de lobby
                LobbyManager.Shutdown();
                
                isInitialized = false;
                isConnected = false;
                
                Logger.LogInfo("Steam Network Manager desconectado.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al desconectar Steam Network Manager: {ex.Message}");
            }
        }
        
        public static bool ConnectToPlayer(CSteamID steamID)
        {
            try
            {
                if (!isInitialized)
                {
                    Logger.LogError("Network Manager no está inicializado.");
                    return false;
                }
                
                if (connectedPlayers.Contains(steamID))
                {
                    Logger.LogWarning($"Ya conectado al jugador {steamID}");
                    return true;
                }
                
                // Intentar establecer conexión P2P
                bool success = SteamNetworking.SendP2PPacket(steamID, new byte[] { 0x01 }, 1, EP2PSend.k_EP2PSendReliable, 0);
                
                if (success)
                {
                    connectedPlayers.Add(steamID);
                    isConnected = true;
                    Logger.LogInfo($"Conectado al jugador {steamID}");
                    return true;
                }
                else
                {
                    Logger.LogError($"Error al conectar con el jugador {steamID}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al conectar con jugador: {ex.Message}");
                return false;
            }
        }
        
        public static void DisconnectFromPlayer(CSteamID steamID)
        {
            try
            {
                if (connectedPlayers.Contains(steamID))
                {
                    SteamNetworking.CloseP2PSessionWithUser(steamID);
                    connectedPlayers.Remove(steamID);
                    Logger.LogInfo($"Desconectado del jugador {steamID}");
                    
                    // Notificar al sistema de eventos
                    NetworkEventManager.NotifyPlayerDisconnected(steamID);
                    
                    if (connectedPlayers.Count == 0)
                    {
                        isConnected = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al desconectar del jugador: {ex.Message}");
            }
        }
        
        public static void DisconnectPlayer(CSteamID steamID)
        {
            try
            {
                if (connectedPlayers.Contains(steamID))
                {
                    connectedPlayers.Remove(steamID);
                    SteamNetworking.CloseP2PSessionWithUser(steamID);
                    Logger.LogInfo($"Jugador desconectado: {steamID}");
                    
                    // Notificar al sistema de eventos
                    NetworkEventManager.NotifyPlayerDisconnected(steamID);
                    
                    if (connectedPlayers.Count == 0)
                    {
                        isConnected = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al desconectar jugador {steamID}: {ex.Message}");
            }
        }
        
        public static void DisconnectFromAllPlayers()
        {
            try
            {
                foreach (var steamID in connectedPlayers.ToArray())
                {
                    DisconnectFromPlayer(steamID);
                }
                
                connectedPlayers.Clear();
                isConnected = false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al desconectar de todos los jugadores: {ex.Message}");
            }
        }
        
        public static bool SendDataToPlayer(CSteamID steamID, byte[] data)
        {
            try
            {
                if (!isInitialized || !connectedPlayers.Contains(steamID))
                    return false;
                
                return SteamNetworking.SendP2PPacket(steamID, data, (uint)data.Length, EP2PSend.k_EP2PSendUnreliable, 0);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al enviar datos: {ex.Message}");
                return false;
            }
        }
        
        public static bool SendDataToAllPlayers(byte[] data)
        {
            try
            {
                bool allSuccess = true;
                foreach (var steamID in connectedPlayers)
                {
                    if (!SendDataToPlayer(steamID, data))
                        allSuccess = false;
                }
                return allSuccess;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al enviar datos a todos los jugadores: {ex.Message}");
                return false;
            }
        }
        
        public static void Update()
        {
            try
            {
                if (!isInitialized)
                    return;
                
                // Procesar paquetes P2P entrantes
                ProcessIncomingPackets();
                
                // Actualizar sincronización de jugadores
                PlayerSyncManager.Update();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en Update de Steam Network Manager: {ex.Message}");
            }
        }
        
        private static void ProcessIncomingPackets()
        {
            uint packetSize;
            CSteamID steamIDRemote;
            
            while (SteamNetworking.IsP2PPacketAvailable(out packetSize, 0))
            {
                byte[] data = new byte[packetSize];
                uint bytesRead;
                
                if (SteamNetworking.ReadP2PPacket(data, packetSize, out bytesRead, out steamIDRemote, 0))
                {
                    // Procesar el paquete recibido
                    ProcessReceivedPacket(steamIDRemote, data, (int)bytesRead);
                }
            }
        }
        
        private static void ProcessReceivedPacket(CSteamID fromSteamID, byte[] data, int length)
        {
            try
            {
                // Enviar datos al PlayerSyncManager para procesamiento
                PlayerSyncManager.OnPlayerDataReceived(fromSteamID, data);
                
                if (SilklessCoopPlugin.PrintDebugOutput?.Value == true)
                {
                    Logger.LogDebug($"Paquete recibido de {fromSteamID}: {length} bytes");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al procesar paquete: {ex.Message}");
            }
        }
        
        // Callbacks de Steam
        private static void OnP2PSessionRequest(P2PSessionRequest_t callback)
        {
            try
            {
                Logger.LogInfo($"Solicitud de sesión P2P de {callback.m_steamIDRemote}");
                
                // Aceptar automáticamente las solicitudes de sesión
                // En una implementación más robusta, podrías querer validar esto
                SteamNetworking.AcceptP2PSessionWithUser(callback.m_steamIDRemote);
                
                if (!connectedPlayers.Contains(callback.m_steamIDRemote))
                {
                    connectedPlayers.Add(callback.m_steamIDRemote);
                    isConnected = true;
                    Logger.LogInfo($"Nuevo jugador conectado: {callback.m_steamIDRemote}");
                    
                    // Notificar al sistema de eventos
                    NetworkEventManager.NotifyPlayerConnected(callback.m_steamIDRemote);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en callback de solicitud P2P: {ex.Message}");
            }
        }
        
        private static void OnP2PSessionConnectFail(P2PSessionConnectFail_t callback)
        {
            try
            {
                Logger.LogError($"Fallo en conexión P2P con {callback.m_steamIDRemote}: {callback.m_eP2PSessionError}");
                
                // Remover de la lista si estaba conectado
                if (connectedPlayers.Contains(callback.m_steamIDRemote))
                {
                    DisconnectFromPlayer(callback.m_steamIDRemote);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en callback de fallo P2P: {ex.Message}");
            }
        }
        
        // Manejadores de eventos del lobby
        private static void OnPlayerJoinedLobby(CSteamID steamID, string playerName)
        {
            try
            {
                // No conectar a nosotros mismos
                if (steamID == SteamUser.GetSteamID())
                    return;
                    
                Logger.LogInfo($"[DEBUG] Jugador {playerName} ({steamID}) se unió al lobby - conectando automáticamente");
                
                // Conectar automáticamente al jugador
                bool success = ConnectToPlayer(steamID);
                Logger.LogInfo($"[DEBUG] Conexión automática a {steamID}: {(success ? "EXITOSA" : "FALLIDA")}");
                
                if (success)
                {
                    NetworkEventManager.NotifyPlayerConnected(steamID);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al conectar automáticamente al jugador {steamID}: {ex.Message}");
            }
        }
        
        private static void OnPlayerLeftLobby(CSteamID steamID, string playerName)
        {
            try
            {
                Logger.LogInfo($"[DEBUG] Jugador {playerName} ({steamID}) salió del lobby - desconectando");
                
                // Desconectar del jugador
                DisconnectFromPlayer(steamID);
                NetworkEventManager.NotifyPlayerDisconnected(steamID);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al desconectar del jugador {steamID}: {ex.Message}");
            }
        }
        
        public static List<CSteamID> GetConnectedPlayers()
        {
            return new List<CSteamID>(connectedPlayers);
        }
        
        public static bool SendPacket(CSteamID targetSteamID, byte[] data, EP2PSend sendType = EP2PSend.k_EP2PSendReliable)
        {
            try
            {
                if (!isInitialized || !isConnected)
                {
                    Logger.LogWarning("Intentando enviar paquete sin estar conectado.");
                    return false;
                }
                
                if (data == null || data.Length == 0)
                {
                    Logger.LogWarning("Intentando enviar paquete vacío.");
                    return false;
                }
                
                bool result = SteamNetworking.SendP2PPacket(targetSteamID, data, (uint)data.Length, sendType, 0);
                
                if (!result)
                {
                    Logger.LogWarning($"Error al enviar paquete a {targetSteamID}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al enviar paquete: {ex.Message}");
                return false;
            }
        }
    }
}