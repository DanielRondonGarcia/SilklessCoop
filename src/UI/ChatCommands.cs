using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Steamworks;
using SilklessCoop.Core;
using BepInEx.Logging;

namespace SilklessCoop.UI
{
    /// <summary>
    /// Sistema de comandos de chat para gestionar lobbies y funciones de red
    /// </summary>
    public class ChatCommands : MonoBehaviour
    {
        private static ManualLogSource Logger => SilklessCoopPlugin.Logger;
        
        private bool isConsoleOpen = false;
        private string currentInput = "";
        private Vector2 scrollPosition = Vector2.zero;
        private List<string> chatHistory = new List<string>();
        private List<string> commandHistory = new List<string>();
        private int commandHistoryIndex = -1;
        
        private const int MAX_CHAT_LINES = 50;
        private const int MAX_COMMAND_HISTORY = 20;
        
        private void Start()
        {
            AddChatMessage("SilklessCoop Chat Commands iniciado. Presiona F1 para abrir/cerrar.");
            AddChatMessage("Comandos disponibles: /help, /create, /join <id>, /leave, /list, /players");
        }
        
        private void Update()
        {
            // Alternar consola con F1 (solo si no está siendo manejado en OnGUI)
            if (Input.GetKeyDown(KeyCode.F1) && !isConsoleOpen)
            {
                ToggleConsole();
            }
            
            // Manejar input cuando la consola está abierta
            if (isConsoleOpen)
            {
                HandleConsoleInput();
            }
        }
        
        private void OnGUI()
        {
            if (!isConsoleOpen) return;
            
            // Manejar eventos de teclado primero
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    if (!string.IsNullOrWhiteSpace(currentInput))
                    {
                        ProcessCommand(currentInput.Trim());
                        
                        // Agregar a historial de comandos
                        commandHistory.Add(currentInput);
                        if (commandHistory.Count > MAX_COMMAND_HISTORY)
                        {
                            commandHistory.RemoveAt(0);
                        }
                        commandHistoryIndex = commandHistory.Count;
                        
                        currentInput = "";
                    }
                    Event.current.Use();
                    return;
                }
                
                if (Event.current.keyCode == KeyCode.Escape || Event.current.keyCode == KeyCode.F1)
                {
                    ToggleConsole();
                    Event.current.Use();
                    return;
                }
                
                // Navegar historial de comandos
                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    if (commandHistoryIndex > 0)
                    {
                        commandHistoryIndex--;
                        currentInput = commandHistory[commandHistoryIndex];
                    }
                    Event.current.Use();
                    return;
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    if (commandHistoryIndex < commandHistory.Count - 1)
                    {
                        commandHistoryIndex++;
                        currentInput = commandHistory[commandHistoryIndex];
                    }
                    else
                    {
                        commandHistoryIndex = commandHistory.Count;
                        currentInput = "";
                    }
                    Event.current.Use();
                    return;
                }
            }
            
            // Configurar estilo
            GUI.skin.textField.fontSize = 14;
            GUI.skin.label.fontSize = 12;
            GUI.skin.box.fontSize = 12;
            
            // Área de la consola
            float consoleHeight = Screen.height * 0.4f;
            Rect consoleRect = new Rect(10, 10, Screen.width - 20, consoleHeight);
            
            GUI.Box(consoleRect, "Chat Commands - F1/ESC para cerrar, Enter para enviar");
            
            // Área de chat history
            Rect chatRect = new Rect(consoleRect.x + 5, consoleRect.y + 25, consoleRect.width - 10, consoleRect.height - 55);
            
            GUILayout.BeginArea(chatRect);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(chatRect.height));
            
            foreach (string line in chatHistory)
            {
                GUILayout.Label(line);
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            
            // Campo de input
            Rect inputRect = new Rect(consoleRect.x + 5, consoleRect.y + consoleRect.height - 25, consoleRect.width - 10, 20);
            
            GUI.SetNextControlName("ChatInput");
            currentInput = GUI.TextField(inputRect, currentInput);
            
            // Enfocar el campo de input
            if (Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("ChatInput");
            }
        }
        
        private void ToggleConsole()
        {
            isConsoleOpen = !isConsoleOpen;
            
            if (isConsoleOpen)
            {
                currentInput = "";
                // Scroll al final
                scrollPosition.y = float.MaxValue;
            }
        }
        
        private void HandleConsoleInput()
        {
            // La lógica de input se maneja ahora en OnGUI para mejor integración
            // Este método se mantiene por compatibilidad pero ya no es necesario
        }
        
        private void ProcessCommand(string command)
        {
            AddChatMessage($"> {command}");
            
            if (!command.StartsWith("/"))
            {
                AddChatMessage("Los comandos deben empezar con '/'. Usa /help para ver comandos disponibles.");
                return;
            }
            
            string[] parts = command.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            
            string cmd = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();
            
            try
            {
                switch (cmd)
                {
                    case "help":
                        ShowHelp();
                        break;
                        
                    case "create":
                        CreateLobby(args);
                        break;
                        
                    case "join":
                        JoinLobby(args);
                        break;
                        
                    case "leave":
                        LeaveLobby();
                        break;
                        
                    case "list":
                        ListLobbies();
                        break;
                        
                    case "players":
                        ListPlayers();
                        break;
                        
                    case "status":
                        ShowStatus();
                        break;
                        
                    case "clear":
                        ClearChat();
                        break;
                        
                    default:
                        AddChatMessage($"Comando desconocido: {cmd}. Usa /help para ver comandos disponibles.");
                        break;
                }
            }
            catch (Exception ex)
            {
                AddChatMessage($"Error al ejecutar comando: {ex.Message}");
                Logger.LogError($"Error en comando {cmd}: {ex.Message}");
            }
        }
        
        private void ShowHelp()
        {
            AddChatMessage("=== Comandos Disponibles ===");
            AddChatMessage("/help - Muestra esta ayuda");
            AddChatMessage("/create [max_jugadores] - Crea un nuevo lobby (2-16 jugadores, por defecto 4)");
            AddChatMessage("/join <id> - Se une a un lobby por ID");
            AddChatMessage("/leave - Sale del lobby actual");
            AddChatMessage("/list - Lista lobbies disponibles");
            AddChatMessage("/players - Muestra jugadores conectados");
            AddChatMessage("/status - Muestra estado actual de conexión");
            AddChatMessage("/clear - Limpia el chat");
            AddChatMessage("F1 - Abrir/cerrar consola de comandos");
            AddChatMessage("Tab - Mostrar/ocultar lista de jugadores");
        }
        
        private void CreateLobby(string[] args)
        {
            int maxPlayers = 4; // Valor por defecto
            
            if (args.Length > 0 && int.TryParse(args[0], out int parsedMaxPlayers))
            {
                maxPlayers = Math.Max(2, Math.Min(16, parsedMaxPlayers)); // Limitar entre 2 y 16
            }
            
            AddChatMessage($"Creando lobby para {maxPlayers} jugadores...");
            LobbyManager.CreateLobby(maxPlayers);
        }
        
        private void JoinLobby(string[] args)
        {
            if (args.Length == 0)
            {
                AddChatMessage("Uso: /join <lobby_id>");
                return;
            }
            
            if (ulong.TryParse(args[0], out ulong lobbyId))
            {
                CSteamID steamLobbyId = new CSteamID(lobbyId);
                AddChatMessage($"Intentando unirse al lobby {lobbyId}...");
                LobbyManager.JoinLobby(steamLobbyId);
            }
            else
            {
                AddChatMessage("ID de lobby inválido. Debe ser un número.");
            }
        }
        
        private void LeaveLobby()
        {
            if (LobbyManager.IsInLobby)
            {
                AddChatMessage("Saliendo del lobby...");
                LobbyManager.LeaveLobby();
            }
            else
            {
                AddChatMessage("No estás en ningún lobby.");
            }
        }
        
        private void ListLobbies()
        {
            AddChatMessage("Buscando lobbies disponibles...");
            // Nota: Steam no permite listar lobbies fácilmente sin filtros específicos
            // En una implementación real, necesitarías usar SteamMatchmaking.RequestLobbyList()
            AddChatMessage("Funcionalidad de listado de lobbies no implementada aún.");
            AddChatMessage("Usa el ID de lobby directamente con /join <id>");
        }
        
        private void ListPlayers()
        {
            AddChatMessage("=== Jugadores Conectados ===");
            
            // Jugador local
            string localName = SteamFriends.GetPersonaName();
            AddChatMessage($"• {localName} (Tú) - {SteamUser.GetSteamID()}");
            
            // Jugadores conectados
            var connectedPlayers = NetworkEventManager.GetConnectedPlayersWithNames();
            if (connectedPlayers.Count > 0)
            {
                foreach (var player in connectedPlayers)
                {
                    var duration = NetworkEventManager.GetPlayerSessionDuration(player.Key);
                    string durationStr = duration.HasValue ? $" ({duration.Value:mm\\:ss})" : "";
                    AddChatMessage($"• {player.Value}{durationStr} - {player.Key}");
                }
            }
            
            // Miembros del lobby
            if (LobbyManager.IsInLobby)
            {
                var lobbyMembers = LobbyManager.GetLobbyMembers();
                var connectedIds = connectedPlayers.Keys.ToHashSet();
                
                foreach (var member in lobbyMembers)
                {
                    if (member != SteamUser.GetSteamID() && !connectedIds.Contains(member))
                    {
                        string memberName = SteamFriends.GetFriendPersonaName(member);
                        AddChatMessage($"• {memberName} (En lobby) - {member}");
                    }
                }
            }
            
            int totalPlayers = 1 + connectedPlayers.Count;
            AddChatMessage($"Total: {totalPlayers} jugador(es) conectado(s)");
        }
        
        private void ShowStatus()
        {
            AddChatMessage("=== Estado de Conexión ===");
            AddChatMessage($"Steam ID: {SteamUser.GetSteamID()}");
            AddChatMessage($"Nombre: {SteamFriends.GetPersonaName()}");
            
            if (LobbyManager.IsInLobby)
            {
                AddChatMessage($"En lobby: Sí (ID: {LobbyManager.CurrentLobbyID})");
                AddChatMessage($"Propietario del lobby: {(LobbyManager.IsLobbyOwner ? "Sí" : "No")}");
                
                var members = LobbyManager.GetLobbyMembers();
                AddChatMessage($"Miembros del lobby: {members.Count}");
            }
            else
            {
                AddChatMessage("En lobby: No");
            }
            
            var connectedPlayers = NetworkEventManager.GetConnectedPlayersWithNames();
            AddChatMessage($"Jugadores conectados directamente: {connectedPlayers.Count}");
        }
        
        private void ClearChat()
        {
            chatHistory.Clear();
            AddChatMessage("Chat limpiado.");
        }
        
        private void AddChatMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            chatHistory.Add($"[{timestamp}] {message}");
            
            // Limitar líneas de chat
            if (chatHistory.Count > MAX_CHAT_LINES)
            {
                chatHistory.RemoveAt(0);
            }
            
            // Auto-scroll al final
            scrollPosition.y = float.MaxValue;
            
            Logger.LogInfo($"Chat: {message}");
        }
        
        // Métodos públicos para agregar mensajes desde otros sistemas
        public static void AddMessage(string message)
        {
            var instance = FindFirstObjectByType<ChatCommands>();
            if (instance != null)
            {
                instance.AddChatMessage(message);
            }
        }
        
        public static void AddSystemMessage(string message)
        {
            AddMessage($"[SISTEMA] {message}");
        }
    }
}