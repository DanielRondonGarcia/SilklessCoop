using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Linq;
using SilklessCoop.Networking;
using SilklessCoop.UI;

namespace SilklessCoop
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class SilklessCoopPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.silklesscoop.mod";
        public const string PluginName = "SilklessCoop";
        public const string PluginVersion = "1.0.0";
        
        public static string Version => PluginVersion;
        
        internal static new ManualLogSource Logger;
        private Harmony harmony;
        
        // Multiplayer state
        public static bool IsMultiplayerEnabled { get; private set; } = false;
        private static bool wasKeyPressed = false;
        
        // Configuration entries
        public static ConfigEntry<KeyCode> ToggleKey;
        public static ConfigEntry<string> ConnectionType;
        public static ConfigEntry<int> TickRate;
        public static ConfigEntry<bool> SyncCompasses;
        public static ConfigEntry<bool> PrintDebugOutput;
        public static ConfigEntry<float> PlayerOpacity;
        public static ConfigEntry<bool> ShowPlayerColorPins;
        public static ConfigEntry<float> ActiveCompassOpacity;
        public static ConfigEntry<float> InactiveCompassOpacity;
        public static ConfigEntry<string> ServerIPAddress;
        public static ConfigEntry<int> ServerPort;
        
        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            
            // Initialize configuration
            InitializeConfig();
            
            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            
            // Inicializar UI
            InitializeUI();
            
            Logger.LogInfo("SilklessCoop patches applied successfully!");
        }
        
        private void InitializeConfig()
        {
            // General Settings
            ToggleKey = Config.Bind("General", "Toggle Key", KeyCode.F5, "Key to enable/disable multiplayer");
            ConnectionType = Config.Bind("General", "Connection Type", "Steam P2P", "Connection type: Steam P2P or Standalone");
            TickRate = Config.Bind("General", "Tick Rate", 20, "Update frequency in Hz");
            SyncCompasses = Config.Bind("General", "Sync Compasses", true, "Show other players on map");
            PrintDebugOutput = Config.Bind("General", "Print Debug Output", false, "Enable debug logging");
            
            // Visual Settings
            PlayerOpacity = Config.Bind("Visual", "Player Opacity", 0.7f, "Transparency of other players (0.0 - 1.0)");
            ShowPlayerColorPins = Config.Bind("Visual", "Show Player Color Pins", true, "Colored indicators above players");
            ActiveCompassOpacity = Config.Bind("Visual", "Active Compass Opacity", 1.0f, "Map icon transparency when active");
            InactiveCompassOpacity = Config.Bind("Visual", "Inactive Compass Opacity", 0.5f, "Map icon transparency when inactive");
            
            // Server Settings
            ServerIPAddress = Config.Bind("Server", "Server IP Address", "127.0.0.1", "IP address of the standalone server");
            ServerPort = Config.Bind("Server", "Server Port", 7777, "Port for server connection");
            
            Logger.LogInfo("Configuration initialized successfully!");
        }
        
        private void Update()
        {
            // Detectar presión de la tecla F5
            bool isKeyCurrentlyPressed = Input.GetKey(ToggleKey.Value);
            
            // Solo activar cuando se presiona la tecla (no cuando se mantiene presionada)
            if (isKeyCurrentlyPressed && !wasKeyPressed)
            {
                ToggleMultiplayer();
            }
            
            wasKeyPressed = isKeyCurrentlyPressed;
            
            // Actualizar el sistema de red si está habilitado
            if (IsMultiplayerEnabled)
            {
                SteamNetworkManager.Update();
            }
        }
        
        public static void ToggleMultiplayer()
        {
            IsMultiplayerEnabled = !IsMultiplayerEnabled;
            
            if (IsMultiplayerEnabled)
            {
                Logger.LogInfo("[SilklessCoop] Modo multijugador ACTIVADO! Presiona F5 para desactivar.");
                ShowOnScreenMessage("Modo Multijugador ACTIVADO", Color.green);
                // Aquí se inicializaría el sistema de red
                InitializeNetworking();
            }
            else
            {
                Logger.LogInfo("[SilklessCoop] Modo multijugador DESACTIVADO.");
                ShowOnScreenMessage("Modo Multijugador DESACTIVADO", Color.red);
                // Aquí se desconectaría el sistema de red
                ShutdownNetworking();
            }
        }
        
        private static void InitializeNetworking()
        {
            Logger.LogInfo("Inicializando sistema de red...");
            
            if (SteamNetworkManager.Initialize())
            {
                Logger.LogInfo("Sistema de red Steam inicializado correctamente.");
            }
            else
            {
                Logger.LogError("Error al inicializar el sistema de red Steam.");
                ShowOnScreenMessage("Error: No se pudo conectar a Steam", Color.red);
            }
        }
        
        private static void ShutdownNetworking()
        {
            Logger.LogInfo("Cerrando sistema de red...");
            SteamNetworkManager.Shutdown();
        }
        
        public static void ShowOnScreenMessage(string message, Color color)
        {
            ShowOnScreenMessage(message, color, 3.0f);
        }
        
        public static void ShowOnScreenMessage(string message, float duration)
        {
            ShowOnScreenMessage(message, Color.white, duration);
        }
        
        public static void ShowOnScreenMessage(string message, Color color, float duration)
        {
            // Crear un mensaje temporal en pantalla
            var messageObj = new GameObject("MultiplayerStatusMessage");
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            
            if (canvas != null)
            {
                messageObj.transform.SetParent(canvas.transform, false);
                
                var rectTransform = messageObj.AddComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(400, 60);
                rectTransform.anchoredPosition = new Vector2(0, 200); // Parte superior central
                
                var text = messageObj.AddComponent<UnityEngine.UI.Text>();
                text.text = message;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 20;
                text.color = color;
                text.alignment = TextAnchor.MiddleCenter;
                text.fontStyle = FontStyle.Bold;
                
                // Destruir el mensaje después del tiempo especificado
                UnityEngine.Object.Destroy(messageObj, duration);
            }
        }
        
        private void OnDestroy()
        {
            // Cerrar sistemas de red
            SteamNetworkManager.Shutdown();
            
            harmony?.UnpatchSelf();
        }
        
        private void InitializeUI()
        {
            try
            {
                // Crear objeto para la UI de lista de jugadores
                GameObject playerListUIObj = new GameObject("PlayerListUI");
                DontDestroyOnLoad(playerListUIObj);
                playerListUIObj.AddComponent<PlayerListUI>();
                
                // Crear objeto para los comandos de chat
                GameObject chatCommandsObj = new GameObject("ChatCommands");
                DontDestroyOnLoad(chatCommandsObj);
                chatCommandsObj.AddComponent<ChatCommands>();
                
                Logger.LogInfo("UI inicializada exitosamente.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error al inicializar UI: {ex.Message}");
            }
        }
    }
    
    [HarmonyPatch]
    public class MenuPatch
    {
        [HarmonyPatch(typeof(UnityEngine.SceneManagement.SceneManager), "LoadScene", typeof(string))]
        [HarmonyPostfix]
        public static void LoadScene_Postfix(string sceneName)
        {
            SilklessCoopPlugin.Logger.LogInfo($"Scene loaded: {sceneName}");
            
            // Solo crear el botón en el menú principal visible (Menu_Title)
            if (sceneName == "Menu_Title")
            {
                SilklessCoopPlugin.Logger.LogInfo($"Main menu scene detected: {sceneName} - attempting to add coop button");
                // Usar corrutina para esperar a que la escena se cargue completamente
                UnityEngine.Object.FindFirstObjectByType<MonoBehaviour>()?.StartCoroutine(AddCoopButtonCoroutine());
            }
            else if (sceneName.ToLower().Contains("menu") || 
                     sceneName.ToLower().Contains("main") ||
                     sceneName.ToLower().Contains("title") ||
                     sceneName.ToLower().Contains("start"))
            {
                SilklessCoopPlugin.Logger.LogInfo($"Menu-related scene detected: {sceneName} - skipping (not main menu)");
            }
        }
        
        private static IEnumerator AddCoopButtonCoroutine()
        {
            // Esperar un frame para que la escena se cargue completamente
            yield return null;
            AddCoopButton();
        }
        
        public static void AddCoopButton()
        {
            SilklessCoopPlugin.Logger.LogInfo("Adding coop button to menu");
            
            // Buscar botones de referencia con múltiples nombres posibles
            var settingsButton = GameObject.Find("Settings Button") ?? 
                                GameObject.Find("Options Button") ??
                                GameObject.Find("Settings") ??
                                GameObject.Find("Options");
            
            if (settingsButton == null)
            {
                // Buscar por componentes Button
                var allButtons = UnityEngine.Object.FindObjectsOfType<Button>();
                SilklessCoopPlugin.Logger.LogInfo($"Found {allButtons.Length} buttons in scene");
                
                foreach (var button in allButtons)
                {
                    var buttonName = button.name.ToLower();
                    var gameObjectName = button.gameObject.name.ToLower();
                    SilklessCoopPlugin.Logger.LogInfo($"Button found: {button.name} - GameObject: {button.gameObject.name}");
                    
                    if (buttonName.Contains("setting") || buttonName.Contains("option") ||
                        buttonName.Contains("config") || buttonName.Contains("preferences") ||
                        gameObjectName.Contains("setting") || gameObjectName.Contains("option") ||
                        gameObjectName.Contains("config") || gameObjectName.Contains("preferences"))
                    {
                        settingsButton = button.gameObject;
                        SilklessCoopPlugin.Logger.LogInfo($"Using button as reference: {button.name}");
                        break;
                    }
                }
                
                // Si no encontramos botón de configuración, usar cualquier botón como referencia
                if (settingsButton == null && allButtons.Length > 0)
                {
                    settingsButton = allButtons[0].gameObject;
                    SilklessCoopPlugin.Logger.LogInfo($"Using first available button as reference: {allButtons[0].name}");
                }
            }
            
            if (settingsButton != null)
            {
                SilklessCoopPlugin.Logger.LogInfo($"Found reference button: {settingsButton.name}");
                SilklessCoopPlugin.Logger.LogInfo($"Reference button parent: {settingsButton.transform.parent?.name ?? "null"}");
                SilklessCoopPlugin.Logger.LogInfo($"Reference button active: {settingsButton.activeInHierarchy}");
                
                // Crear el botón de coop
                var coopButton = UnityEngine.Object.Instantiate(settingsButton, settingsButton.transform.parent);
                coopButton.name = "Coop Button";
                SilklessCoopPlugin.Logger.LogInfo($"Coop button instantiated: {coopButton.name}");
                
                // Asegurar que el botón esté activo
                coopButton.SetActive(true);
                SilklessCoopPlugin.Logger.LogInfo($"Coop button set active: {coopButton.activeInHierarchy}");
                
                // Posicionar el botón
                var rectTransform = coopButton.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    var settingsRect = settingsButton.GetComponent<RectTransform>();
                    var originalPos = settingsRect.anchoredPosition;
                    var newPos = originalPos + new Vector2(0, -80); // Aumentar separación
                    rectTransform.anchoredPosition = newPos;
                    SilklessCoopPlugin.Logger.LogInfo($"Button positioned - Original: {originalPos}, New: {newPos}");
                }
                else
                {
                    SilklessCoopPlugin.Logger.LogWarning("Could not find RectTransform on coop button");
                }
                
                // Cambiar el texto
                var textComponent = coopButton.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = "Coop";
                    SilklessCoopPlugin.Logger.LogInfo($"Button text set to: {textComponent.text}");
                }
                else
                {
                    SilklessCoopPlugin.Logger.LogWarning("Could not find Text component on coop button");
                }
                
                // Configurar el botón
                var buttonComponent = coopButton.GetComponent<Button>();
                if (buttonComponent != null)
                {
                    buttonComponent.onClick.RemoveAllListeners();
                    buttonComponent.onClick.AddListener(() => {
                        SilklessCoopPlugin.Logger.LogInfo("Coop button clicked!");
                        // Aquí iría la lógica del coop
                    });
                    SilklessCoopPlugin.Logger.LogInfo("Button click handler configured");
                }
                else
                {
                    SilklessCoopPlugin.Logger.LogWarning("Could not find Button component on coop button");
                }
                
                SilklessCoopPlugin.Logger.LogInfo($"Coop button created successfully! Final state - Active: {coopButton.activeInHierarchy}, Position: {rectTransform?.anchoredPosition}");
            }
            else
            {
                SilklessCoopPlugin.Logger.LogWarning("Could not find any button to use as reference");
                
                // Listar todos los GameObjects en la escena para debug
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                SilklessCoopPlugin.Logger.LogInfo($"Total GameObjects in scene: {allObjects.Length}");
                
                // Mostrar GameObjects que podrían ser botones o elementos de UI
                var uiObjects = allObjects.Where(obj => 
                    obj.name.ToLower().Contains("button") ||
                    obj.name.ToLower().Contains("menu") ||
                    obj.name.ToLower().Contains("ui") ||
                    obj.GetComponent<Button>() != null ||
                    obj.GetComponent<UnityEngine.UI.Text>() != null
                ).Take(15);
                
                SilklessCoopPlugin.Logger.LogInfo("Relevant UI GameObjects found:");
                foreach (var obj in uiObjects)
                {
                    var hasButton = obj.GetComponent<Button>() != null ? " [HAS BUTTON]" : "";
                    var hasText = obj.GetComponent<UnityEngine.UI.Text>() != null ? " [HAS TEXT]" : "";
                    SilklessCoopPlugin.Logger.LogInfo($"  - {obj.name}{hasButton}{hasText}");
                }
                
                // Intentar crear el botón de todas formas si hay algún Canvas
                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    SilklessCoopPlugin.Logger.LogInfo($"Found Canvas: {canvas.name}, attempting to create button anyway");
                    CreateCoopButtonOnCanvas(canvas);
                }
            }
        }
        
        private static void CreateCoopButtonOnCanvas(Canvas canvas)
        {
            SilklessCoopPlugin.Logger.LogInfo("Creating coop button directly on canvas");
            
            // Crear un GameObject para el botón
            var buttonObj = new GameObject("Coop Button");
            buttonObj.transform.SetParent(canvas.transform, false);
            
            // Agregar RectTransform
            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 50);
            rectTransform.anchoredPosition = new Vector2(0, -100); // Posición central, un poco abajo
            
            // Agregar Image component para el fondo del botón
            var image = buttonObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Gris oscuro semi-transparente
            
            // Agregar Button component
            var button = buttonObj.AddComponent<Button>();
            button.onClick.AddListener(() => {
                SilklessCoopPlugin.Logger.LogInfo("Coop button clicked!");
                // Aquí iría la lógica del coop
            });
            
            // Crear texto para el botón
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            var textRectTransform = textObj.AddComponent<RectTransform>();
            textRectTransform.sizeDelta = rectTransform.sizeDelta;
            textRectTransform.anchoredPosition = Vector2.zero;
            
            var text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.text = "Coop";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            
            SilklessCoopPlugin.Logger.LogInfo("Coop button created successfully on canvas!");
        }
    }
}