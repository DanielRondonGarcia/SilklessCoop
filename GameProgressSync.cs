using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SilklessCoop
{
    /// <summary>
    /// Snapshot del progreso de un jugador para validación
    /// </summary>
    public class PlayerProgressSnapshot
    {
        public string PlayerId { get; set; }
        public Dictionary<string, object> Progress { get; set; } = new Dictionary<string, object>();
        public DateTime LastUpdate { get; set; }
        public int GameTimeMinutes { get; set; } // Tiempo de juego aproximado
        
        public bool HasPrerequisite(string prerequisite)
        {
            return Progress.ContainsKey(prerequisite) && 
                   Progress[prerequisite] is bool boolValue && boolValue;
        }
        
        public int GetResourceAmount(string resource)
        {
            if (Progress.ContainsKey(resource) && Progress[resource] is int amount)
                return amount;
            return 0;
        }
    }
    
    /// <summary>
    /// Sistema de sincronización de progreso del juego para modo cooperativo completo
    /// Sincroniza elementos como objetos recolectados, puertas abiertas, habilidades, etc.
    /// </summary>
    internal class GameProgressSync : MonoBehaviour
    {
        public ManualLogSource Logger;
        public ModConfig Config;
        
        private Connector _connector;
        private Dictionary<string, object> _lastSyncedProgress = new Dictionary<string, object>();
        private float _lastSyncTime = 0f;
        private const float SYNC_INTERVAL = 2.0f; // Sincronizar cada 2 segundos
        private string _pendingProgressData;
        
        // Sistema de snapshots y validación de progreso
        private Dictionary<string, PlayerProgressSnapshot> _playerSnapshots = new Dictionary<string, PlayerProgressSnapshot>();
        private Dictionary<string, List<string>> _progressPrerequisites = new Dictionary<string, List<string>>();
        
        // Elementos del progreso del juego a sincronizar
        private readonly string[] _syncableProgressKeys = {
            // Moneda y recursos
            "geo",
            "silk",
            
            // Habilidades y mejoras
            "hasWalljump",
            "hasDoubleJump",
            "hasDash",
            "hasSuperdash",
            "hasAcidArmour",
            "hasKingsIdol",
            
            // Objetos y herramientas
            "hasLantern",
            "hasQuill",
            "hasMap",
            "hasCompass",
            
            // Progreso de áreas
            "openedDoor_", // Prefijo para puertas abiertas
            "visitedArea_", // Prefijo para áreas visitadas
            "unlockedGate_", // Prefijo para puertas desbloqueadas
            
            // Objetos recolectados
            "collectedItem_", // Prefijo para objetos recolectados
            "foundSecret_", // Prefijo para secretos encontrados
            
            // Progreso de misiones
            "questCompleted_", // Prefijo para misiones completadas
            "questStarted_", // Prefijo para misiones iniciadas
            "npcTalkedTo_", // Prefijo para NPCs con los que se ha hablado
        };
        
        private void Start()
        {
            _connector = GetComponent<Connector>();
            InitializeProgressPrerequisites();
            if (Config.PrintDebugOutput)
                Logger.LogInfo("GameProgressSync initialized");
        }
        
        /// <summary>
        /// Inicializa los prerequisitos para diferentes elementos del progreso
        /// </summary>
        private void InitializeProgressPrerequisites()
        {
            // Habilidades básicas - requieren progreso mínimo
            _progressPrerequisites["hasWalljump"] = new List<string> { "hasLantern" }; // Requiere exploración básica
            _progressPrerequisites["hasDash"] = new List<string> { "hasLantern" }; // Requiere exploración básica
            
            // Habilidades avanzadas
            _progressPrerequisites["hasDoubleJump"] = new List<string> { "hasWalljump", "hasDash" };
            _progressPrerequisites["hasSuperdash"] = new List<string> { "hasDash", "hasWalljump" }; // Más estricto
            
            // Herramientas - progresión más lógica
            _progressPrerequisites["hasLantern"] = new List<string>(); // Verdaderamente básico
            _progressPrerequisites["hasQuill"] = new List<string> { "hasLantern" };
            _progressPrerequisites["hasMap"] = new List<string> { "hasQuill" };
            _progressPrerequisites["hasCompass"] = new List<string> { "hasMap" };
            
            // Recursos - límites basados en progreso
            // Geo: máximo razonable basado en el tiempo de juego
            // Silk: requiere ciertas habilidades para acceder a áreas con seda
        }
        
        private void Update()
        {
            if (!_connector || !_connector.Active || !Config.SyncGameProgress) return;
            
            // Sincronizar progreso periódicamente
            if (Time.time - _lastSyncTime > SYNC_INTERVAL)
            {
                SyncGameProgress();
                _lastSyncTime = Time.time;
            }
        }
        
        /// <summary>
        /// Sincroniza el progreso del juego con otros jugadores
        /// </summary>
        private void SyncGameProgress()
        {
            try
            {
                var currentProgress = GetCurrentGameProgress();
                
                // Verificar si hay cambios en el progreso
                if (HasProgressChanged(currentProgress))
                {
                    string progressData = SerializeProgress(currentProgress);
                    // El envío se maneja a través del sistema GameSync existente
                    // Almacenamos los datos para que sean enviados en el próximo tick
                    _pendingProgressData = $"PROGRESS::{progressData}";
                    
                    _lastSyncedProgress = new Dictionary<string, object>(currentProgress);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Prepared game progress for sending: {progressData}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error syncing game progress: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene el progreso actual del juego desde PlayerData
        /// </summary>
        private Dictionary<string, object> GetCurrentGameProgress()
        {
            var progress = new Dictionary<string, object>();
            
            try
            {
                // Intentar acceder a PlayerData a través de reflexión
                var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                if (playerDataType != null)
                {
                    var instanceProperty = playerDataType.GetProperty("instance", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (instanceProperty != null)
                    {
                        var playerDataInstance = instanceProperty.GetValue(null);
                        
                        if (playerDataInstance != null)
                        {
                            // Obtener campos específicos del progreso
                            foreach (var key in _syncableProgressKeys)
                            {
                                if (key.EndsWith("_")) // Es un prefijo
                                {
                                    // Buscar todos los campos que empiecen con este prefijo
                                    var fields = playerDataType.GetFields();
                                    foreach (var field in fields)
                                    {
                                        if (field.Name.StartsWith(key.TrimEnd('_')))
                                        {
                                            var value = field.GetValue(playerDataInstance);
                                            progress[field.Name] = value;
                                        }
                                    }
                                }
                                else
                                {
                                    // Campo específico
                                    var field = playerDataType.GetField(key);
                                    if (field != null)
                                    {
                                        var value = field.GetValue(playerDataInstance);
                                        progress[key] = value;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not access PlayerData directly: {ex.Message}");
                // Fallback: usar valores por defecto o métodos alternativos
            }
            
            return progress;
        }
        
        /// <summary>
        /// Verifica si el progreso ha cambiado desde la última sincronización
        /// </summary>
        private bool HasProgressChanged(Dictionary<string, object> currentProgress)
        {
            if (_lastSyncedProgress.Count == 0) return true;
            
            foreach (var kvp in currentProgress)
            {
                if (!_lastSyncedProgress.ContainsKey(kvp.Key) || 
                    !_lastSyncedProgress[kvp.Key].Equals(kvp.Value))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Serializa el progreso del juego a una cadena
        /// </summary>
        private string SerializeProgress(Dictionary<string, object> progress)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{");
                bool first = true;
                foreach (var kvp in progress)
                {
                    if (!first) sb.Append(",");
                    sb.Append($"\"{kvp.Key}\":\"{kvp.Value}\"");
                    first = false;
                }
                sb.Append("}");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error serializing progress: {ex.Message}");
                return "{}";
            }
        }
        
        /// <summary>
        /// Aplica el progreso recibido de otro jugador
        /// </summary>
        public void ApplyReceivedProgress(string progressData)
        {
            try
            {
                var receivedProgress = ParseSimpleJson(progressData);
                
                if (receivedProgress != null && receivedProgress.Count > 0)
                {
                    ApplyProgressToGame(receivedProgress);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Applied received progress: {progressData}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying received progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Parser simple para JSON básico
        /// </summary>
        private Dictionary<string, object> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, object>();
            
            if (string.IsNullOrEmpty(json) || !json.StartsWith("{") || !json.EndsWith("}"))
                return result;
                
            json = json.Substring(1, json.Length - 2); // Remove { }
            
            if (string.IsNullOrEmpty(json))
                return result;
                
            var pairs = json.Split(',');
            foreach (var pair in pairs)
            {
                var keyValue = pair.Split(':');
                if (keyValue.Length == 2)
                {
                    var key = keyValue[0].Trim().Trim('"');
                    var value = keyValue[1].Trim().Trim('"');
                    result[key] = value;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Aplica el progreso al juego actual con validación
        /// </summary>
        private void ApplyProgressToGame(Dictionary<string, object> progress)
        {
            try
            {
                // Obtener el progreso actual del jugador local
                var currentPlayerProgress = GetCurrentGameProgress();
                var localSnapshot = new PlayerProgressSnapshot
                {
                    PlayerId = "local",
                    Progress = currentPlayerProgress,
                    LastUpdate = DateTime.Now
                };
                
                // Intentar acceder a PlayerData para aplicar cambios
                var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                if (playerDataType != null)
                {
                    var instanceProperty = playerDataType.GetProperty("instance", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (instanceProperty != null)
                    {
                        var playerDataInstance = instanceProperty.GetValue(null);
                        
                        if (playerDataInstance != null)
                        {
                            foreach (var kvp in progress)
                            {
                                var field = playerDataType.GetField(kvp.Key);
                                if (field != null && ShouldSyncField(kvp.Key, kvp.Value))
                                {
                                    var currentValue = field.GetValue(playerDataInstance);
                                    
                                    // Validar si el progreso es válido para este jugador
                                    if (IsProgressValid(kvp.Key, kvp.Value, localSnapshot) && 
                                        IsProgressBetter(currentValue, kvp.Value))
                                    {
                                        // Convertir el valor al tipo correcto antes de establecerlo
                                        var convertedValue = ConvertValueToType(kvp.Value, field.FieldType);
                                        field.SetValue(playerDataInstance, convertedValue);
                                        
                                        if (Config.PrintDebugOutput)
                                            Logger.LogInfo($"Updated {kvp.Key}: {currentValue} -> {convertedValue}");
                                    }
                                    else if (Config.PrintDebugOutput)
                                    {
                                        Logger.LogInfo($"Skipped {kvp.Key}: invalid progress or prerequisites not met");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying progress to game: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determina si un campo debe sincronizarse
        /// </summary>
        private bool ShouldSyncField(string fieldName, object value)
        {
            // No sincronizar ciertos campos sensibles o específicos del jugador
            var excludedFields = new[] { "playerName", "playTime", "currentScene" };
            
            foreach (var excluded in excludedFields)
            {
                if (fieldName.Contains(excluded))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Valida si un progreso es válido para el jugador actual
        /// </summary>
        private bool IsProgressValid(string fieldName, object value, PlayerProgressSnapshot playerSnapshot)
        {
            try
            {
                // Validar habilidades específicas que causan problemas
                if (fieldName == "hasSuperdash" && value is bool && (bool)value)
                {
                    if (!playerSnapshot.HasPrerequisite("hasDash") || !playerSnapshot.HasPrerequisite("hasWalljump"))
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Superdash blocked: requires both dash and walljump");
                        return false;
                    }
                }
                
                if (fieldName == "hasWalljump" && value is bool && (bool)value)
                {
                    if (!playerSnapshot.HasPrerequisite("hasLantern"))
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Walljump blocked: requires lantern (basic exploration)");
                        return false;
                    }
                }
                
                // Validar prerequisitos para habilidades
                if (_progressPrerequisites.ContainsKey(fieldName))
                {
                    var prerequisites = _progressPrerequisites[fieldName];
                    foreach (var prerequisite in prerequisites)
                    {
                        if (!playerSnapshot.HasPrerequisite(prerequisite))
                        {
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Progress {fieldName} blocked: missing prerequisite {prerequisite}");
                            return false;
                        }
                    }
                }
                
                // Validar límites de recursos
                if (fieldName == "geo")
                {
                    return ValidateGeoAmount(value, playerSnapshot);
                }
                
                if (fieldName == "silk")
                {
                    return ValidateSilkAmount(value, playerSnapshot);
                }
                
                // Por defecto, permitir el progreso si no hay reglas específicas
                return true;
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogWarning($"Error validating progress {fieldName}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Valida si la cantidad de geo es razonable
        /// </summary>
        private bool ValidateGeoAmount(object value, PlayerProgressSnapshot playerSnapshot)
        {
            int targetGeo;
            if (value is int directGeo)
            {
                targetGeo = directGeo;
            }
            else
            {
                var converted = ConvertValueToType(value, typeof(int));
                if (!(converted is int convertedGeo))
                    return false;
                targetGeo = convertedGeo;
            }
            
            int currentGeo = playerSnapshot.GetResourceAmount("geo");
            
            // Límites más estrictos basados en progreso
            int maxGeoAllowed = 100; // Muy conservador por defecto
            
            // Aumentar límite basado en progreso real
            if (playerSnapshot.HasPrerequisite("hasLantern"))
                maxGeoAllowed = 300;
            if (playerSnapshot.HasPrerequisite("hasDash"))
                maxGeoAllowed = 600;
            if (playerSnapshot.HasPrerequisite("hasWalljump"))
                maxGeoAllowed = 1000;
            if (playerSnapshot.HasPrerequisite("hasDoubleJump"))
                maxGeoAllowed = 2000;
            
            // Rechazar cantidades excesivas
            if (targetGeo > maxGeoAllowed)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Geo amount {targetGeo} exceeds limit {maxGeoAllowed} for current progress");
                return false;
            }
            
            // Permitir solo incrementos graduales (máximo 200 geo por sincronización)
            int maxIncrement = 200;
            if (targetGeo > currentGeo + maxIncrement)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Geo increment too large: {currentGeo} -> {targetGeo} (max +{maxIncrement})");
                return false;
            }
            
            // Permitir decrementos (pérdida de geo por muerte)
            return true;
        }
        
        /// <summary>
        /// Valida si la cantidad de seda es razonable
        /// </summary>
        private bool ValidateSilkAmount(object value, PlayerProgressSnapshot playerSnapshot)
        {
            int targetSilk;
            if (value is int directSilk)
            {
                targetSilk = directSilk;
            }
            else
            {
                var converted = ConvertValueToType(value, typeof(int));
                if (!(converted is int convertedSilk))
                    return false;
                targetSilk = convertedSilk;
            }
            
            int currentSilk = playerSnapshot.GetResourceAmount("silk");
            
            // La seda requiere acceso a ciertas áreas
            if (!playerSnapshot.HasPrerequisite("hasLantern") && targetSilk > 0)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Silk not available without lantern");
                return false;
            }
            
            // Permitir incrementos razonables
            int maxIncrement = 50;
            if (targetSilk > currentSilk + maxIncrement)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Silk increment too large: {currentSilk} -> {targetSilk}");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Determina si un valor representa más progreso que otro
        /// </summary>
        private bool IsProgressBetter(object currentValue, object newValue)
        {
            try
            {
                // Convertir newValue de string si es necesario
                object convertedNewValue = ConvertValueToType(newValue, currentValue?.GetType());
                
                // Para booleanos, true es mejor que false (representa progreso)
                if (currentValue is bool currentBool && convertedNewValue is bool newBool)
                {
                    return !currentBool && newBool;
                }
                
                // Para números, mayor es mejor (más recursos, más progreso)
                if (currentValue is int currentInt && convertedNewValue is int newInt)
                {
                    return newInt > currentInt;
                }
                
                if (currentValue is float currentFloat && convertedNewValue is float newFloat)
                {
                    return newFloat > currentFloat;
                }
                
                // Por defecto, aceptar el nuevo valor
                return true;
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogWarning($"Error comparing values: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Convierte un valor string al tipo apropiado
        /// </summary>
        private object ConvertValueToType(object value, Type targetType)
        {
            if (value == null || targetType == null)
                return value;
                
            // Si ya es del tipo correcto, devolverlo
            if (targetType.IsAssignableFrom(value.GetType()))
                return value;
                
            // Si es string, intentar convertir
            if (value is string stringValue)
            {
                if (targetType == typeof(bool))
                {
                    if (bool.TryParse(stringValue, out bool boolResult))
                        return boolResult;
                }
                else if (targetType == typeof(int))
                {
                    if (int.TryParse(stringValue, out int intResult))
                        return intResult;
                }
                else if (targetType == typeof(float))
                {
                    if (float.TryParse(stringValue, out float floatResult))
                        return floatResult;
                }
            }
            
            return value;
        }
        
        /// <summary>
        /// Resetea el sistema de sincronización
        /// </summary>
        public void Reset()
        {
            _lastSyncedProgress.Clear();
            _lastSyncTime = 0f;
            _pendingProgressData = null;
            
            if (Config.PrintDebugOutput)
                Logger.LogInfo("GameProgressSync reset");
        }

        /// <summary>
        /// Obtiene datos de progreso pendientes para envío
        /// </summary>
        public string GetPendingProgressData()
        {
            string data = _pendingProgressData;
            _pendingProgressData = null; // Clear after reading
            return data;
        }
    }
}