using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;
using BepInEx.Logging;

namespace SilklessCoop.Core
{
    /// <summary>
    /// Maneja la creación y gestión de lobbies para multijugador
    /// </summary>
    public static class LobbyManager
    {
        private static ManualLogSource Logger => SilklessCoopPlugin.Logger;
        
        private static CSteamID currentLobbyID = CSteamID.Nil;
        private static bool isInitialized = false;
        
        /// <summary>
        /// Indica si estamos actualmente en un lobby
        /// </summary>
        public static bool IsInLobby => currentLobbyID != CSteamID.Nil;
        private static bool isLobbyOwner = false;
        
        // Callbacks de Steam
        private static Callback<LobbyCreated_t> lobbyCreatedCallback;
        private static Callback<LobbyEnter_t> lobbyEnterCallback;
        private static Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
        private static Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
        
        // Eventos públicos
        public static event Action<CSteamID> OnLobbyCreated;
        public static event Action<CSteamID> OnLobbyJoined;
        public static event Action OnLobbyLeft;
        public static event Action<CSteamID, string> OnPlayerJoinedLobby;
        public static event Action<CSteamID, string> OnPlayerLeftLobby;
        
        public static void Initialize()
        {
            Logger.LogInfo("Inicializando LobbyManager...");
            
            try
            {
                // Registrar callbacks de Steam
                lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
                lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnterCallback);
                lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);
                lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCallback);
                
                Logger.LogInfo("LobbyManager inicializado correctamente.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al inicializar LobbyManager: {ex.Message}");
            }
        }
        
        public static void Shutdown()
        {
            Logger.LogInfo("Cerrando LobbyManager...");
            
            try
            {
                // Salir del lobby actual si estamos en uno
                if (IsInLobby)
                {
                    LeaveLobby();
                }
                
                // Limpiar callbacks
                lobbyCreatedCallback?.Dispose();
                lobbyEnterCallback?.Dispose();
                lobbyChatUpdateCallback?.Dispose();
                lobbyDataUpdateCallback?.Dispose();
                
                // Limpiar eventos
                OnLobbyCreated = null;
                OnLobbyJoined = null;
                OnLobbyLeft = null;
                OnPlayerJoinedLobby = null;
                OnPlayerLeftLobby = null;
                
                Logger.LogInfo("LobbyManager cerrado correctamente.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al cerrar LobbyManager: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crea un nuevo lobby
        /// </summary>
        public static void CreateLobby(int maxPlayers = 4)
        {
            try
            {
                if (IsInLobby)
                {
                    Logger.LogWarning("Ya estás en un lobby. Sal del lobby actual antes de crear uno nuevo.");
                    return;
                }
                
                Logger.LogInfo($"Creando lobby para {maxPlayers} jugadores...");
                SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al crear lobby: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Une a un lobby existente
        /// </summary>
        public static void JoinLobby(CSteamID lobbyID)
        {
            try
            {
                if (IsInLobby)
                {
                    Logger.LogWarning("Ya estás en un lobby. Sal del lobby actual antes de unirte a otro.");
                    return;
                }
                
                Logger.LogInfo($"Uniéndose al lobby {lobbyID}...");
                SteamMatchmaking.JoinLobby(lobbyID);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al unirse al lobby {lobbyID}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sale del lobby actual
        /// </summary>
        public static void LeaveLobby()
        {
            try
            {
                if (!IsInLobby)
                {
                    Logger.LogWarning("No estás en ningún lobby.");
                    return;
                }
                
                Logger.LogInfo($"Saliendo del lobby {currentLobbyID}...");
                SteamMatchmaking.LeaveLobby(currentLobbyID);
                
                currentLobbyID = CSteamID.Nil;
                isLobbyOwner = false;
                
                OnLobbyLeft?.Invoke();
                SilklessCoopPlugin.ShowOnScreenMessage("Has salido del lobby", 2f);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al salir del lobby: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene la lista de jugadores en el lobby actual
        /// </summary>
        public static List<CSteamID> GetLobbyMembers()
        {
            var members = new List<CSteamID>();
            
            if (!IsInLobby)
                return members;
                
            try
            {
                int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);
                for (int i = 0; i < memberCount; i++)
                {
                    CSteamID memberID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);
                    members.Add(memberID);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al obtener miembros del lobby: {ex.Message}");
            }
            
            return members;
        }
        
        // Callbacks de Steam
        private static void OnLobbyCreatedCallback(LobbyCreated_t callback)
        {
            if (callback.m_eResult == EResult.k_EResultOK)
            {
                currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
                isLobbyOwner = true;
                
                Logger.LogInfo($"Lobby creado exitosamente: {currentLobbyID}");
                SilklessCoopPlugin.ShowOnScreenMessage($"Lobby creado: {currentLobbyID}", 3f);
                
                // Configurar datos del lobby
                SteamMatchmaking.SetLobbyData(currentLobbyID, "game", "SilklessCoop");
                SteamMatchmaking.SetLobbyData(currentLobbyID, "version", SilklessCoopPlugin.Version);
                
                OnLobbyCreated?.Invoke(currentLobbyID);
            }
            else
            {
                Logger.LogError($"Error al crear lobby: {callback.m_eResult}");
                SilklessCoopPlugin.ShowOnScreenMessage("Error al crear lobby", 3f);
            }
        }
        
        private static void OnLobbyEnterCallback(LobbyEnter_t callback)
        {
            currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            isLobbyOwner = SteamMatchmaking.GetLobbyOwner(currentLobbyID) == SteamUser.GetSteamID();
            
            Logger.LogInfo($"Unido al lobby: {currentLobbyID}");
            SilklessCoopPlugin.ShowOnScreenMessage($"Unido al lobby: {currentLobbyID}", 3f);
            
            OnLobbyJoined?.Invoke(currentLobbyID);
        }
        
        private static void OnLobbyChatUpdateCallback(LobbyChatUpdate_t callback)
        {
            CSteamID lobbyID = new CSteamID(callback.m_ulSteamIDLobby);
            CSteamID userID = new CSteamID(callback.m_ulSteamIDUserChanged);
            
            if (lobbyID != currentLobbyID)
                return;
                
            string playerName = SteamFriends.GetFriendPersonaName(userID);
            
            if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                Logger.LogInfo($"Jugador unido al lobby: {playerName} ({userID})");
                OnPlayerJoinedLobby?.Invoke(userID, playerName);
            }
            else if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
            {
                Logger.LogInfo($"Jugador salió del lobby: {playerName} ({userID})");
                OnPlayerLeftLobby?.Invoke(userID, playerName);
            }
        }
        
        private static void OnLobbyDataUpdateCallback(LobbyDataUpdate_t callback)
        {
            // Manejar actualizaciones de datos del lobby si es necesario
        }
        
        // Propiedades públicas
        public static bool IsLobbyOwner => isLobbyOwner;
        public static CSteamID CurrentLobbyID => currentLobbyID;
    }
}