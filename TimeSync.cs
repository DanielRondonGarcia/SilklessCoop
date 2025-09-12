using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SilklessCoop
{
    /// <summary>
    /// Sistema de sincronización de tiempo de juego para modo cooperativo
    /// Sincroniza el tiempo de juego, año del juego y eventos temporales
    /// </summary>
    internal class TimeSync : MonoBehaviour
    {
        private ManualLogSource _logger;
        private ModConfig _config;
        
        private Connector _connector;
        private GameSync _gameSync;
        
        // Sistema de tiempo del juego
        private float _gameTime = 0f;
        private int _gameYear = 1;
        private float _lastTimeSync = 0f;
        private const float TIME_SYNC_INTERVAL = 5.0f; // Sincronizar cada 5 segundos
        
        // Datos pendientes para envío
        private string _pendingTimeData;
        
        private bool _initialized = false;
        
        private void Start()
        {
            _connector = GetComponent<Connector>();
            _gameSync = GetComponent<GameSync>();
        }
        
        private void Update()
        {
            if (!_initialized)
            {
                InitializeTimeSync();
                return;
            }
            
            if (_connector == null || !_connector.Active)
                return;
                
            // Actualizar tiempo de juego
            _gameTime += Time.deltaTime;
            
            // Sincronizar tiempo
            if (Time.time - _lastTimeSync >= TIME_SYNC_INTERVAL)
            {
                SyncGameTime();
                _lastTimeSync = Time.time;
            }
        }
        
        /// <summary>
        /// Inicializa el sistema de sincronización de tiempo
        /// </summary>
        internal void Initialize(ManualLogSource logger, ModConfig config)
        {
            _logger = logger;
            _config = config;
        }
        
        /// <summary>
        /// Inicializa el sistema de sincronización de tiempo
        /// </summary>
        private void InitializeTimeSync()
        {
            try
            {
                // Intentar obtener el tiempo del juego usando reflexión
                GetGameTimeFromSaveData();
                
                _initialized = true;
                
                if (_config.PrintDebugOutput)
                    _logger.LogInfo("TimeSync initialized successfully");
            }
            catch (Exception ex)
            {
                if (_config.PrintDebugOutput)
                    _logger.LogError($"Error initializing TimeSync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene el tiempo del juego desde los datos de guardado
        /// </summary>
        private void GetGameTimeFromSaveData()
        {
            try
            {
                // Buscar el GameManager
                var gameManagerType = Type.GetType("GameManager");
                if (gameManagerType == null)
                {
                    _logger?.LogWarning("No se pudo encontrar el tipo GameManager");
                    return;
                }
                
                var gameManager = FindObjectOfType(gameManagerType) as UnityEngine.Object;
                if (gameManager == null)
                {
                    // Buscar usando reflexión
                    var instanceProperty = gameManagerType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        gameManager = instanceProperty.GetValue(null) as UnityEngine.Object;
                    }
                    
                    if (gameManager == null)
                    {
                        _logger?.LogWarning("GameManager no encontrado, reintentando más tarde");
                        return;
                    }
                }
                
                if (gameManager != null)
                {
                    // Intentar obtener datos de tiempo
                    var playerDataField = gameManager.GetType().GetField("playerData", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (playerDataField != null)
                    {
                        var playerData = playerDataField.GetValue(gameManager);
                        if (playerData != null)
                        {
                            // Buscar campos de tiempo
                            var playTimeField = playerData.GetType().GetField("playTime", BindingFlags.Public | BindingFlags.Instance);
                            if (playTimeField != null && playTimeField.FieldType == typeof(float))
                            {
                                _gameTime = (float)playTimeField.GetValue(playerData);
                            }
                            
                            // Buscar año del juego (si existe)
                            var gameYearField = playerData.GetType().GetField("gameYear", BindingFlags.Public | BindingFlags.Instance);
                            if (gameYearField != null && gameYearField.FieldType == typeof(int))
                            {
                                _gameYear = (int)gameYearField.GetValue(playerData);
                            }
                        }
                    }
                }
                
                if (_config.PrintDebugOutput)
                    _logger.LogInfo($"Game time initialized: {_gameTime:F1}s, Year: {_gameYear}");
            }
            catch (Exception ex)
            {
                if (_config.PrintDebugOutput)
                    _logger.LogError($"Error getting game time: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Busca un tipo por nombre en todos los assemblies cargados
        /// </summary>
        private Type FindTypeByName(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null) return type;
                    
                    // Buscar en todos los tipos del assembly
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName) return t;
                    }
                }
                catch (Exception)
                {
                    // Ignorar errores de acceso a assemblies
                }
            }
            return null;
        }
        
        /// <summary>
        /// Sincroniza el tiempo del juego
        /// </summary>
        private void SyncGameTime()
        {
            try
            {
                string timeData = $"{_gameTime:F1}|{_gameYear}";
                _pendingTimeData = timeData;
                
                if (_config.PrintDebugOutput)
                    _logger.LogInfo($"Game time queued for sync: {_gameTime:F1}s, Year: {_gameYear}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error syncing game time: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene datos pendientes para envío
        /// </summary>
        public string GetPendingData()
        {
            try
            {
                if (!string.IsNullOrEmpty(_pendingTimeData))
                {
                    string data = $"TIME::{_pendingTimeData}";
                    _pendingTimeData = null;
                    return data;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting pending time data: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Aplica datos recibidos de otros jugadores
        /// </summary>
        public void ApplyReceivedData(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                
                string[] parts = data.Split('|');
                if (parts.Length >= 2)
                {
                    if (float.TryParse(parts[0], out float receivedTime) && 
                        int.TryParse(parts[1], out int receivedYear))
                    {
                        // Solo actualizar si el tiempo recibido es mayor (evitar retrocesos)
                        if (receivedTime > _gameTime)
                        {
                            _gameTime = receivedTime;
                        }
                        
                        // Sincronizar año
                        if (receivedYear != _gameYear)
                        {
                            _gameYear = receivedYear;
                            
                            if (_config.PrintDebugOutput)
                                _logger.LogInfo($"Game year synchronized to: {_gameYear}");
                        }
                        
                        if (_config.PrintDebugOutput)
                            _logger.LogInfo($"Received time update: {receivedTime:F1}s, Year: {receivedYear}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying received time data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resetea el sistema de sincronización de tiempo
        /// </summary>
        public void Reset()
        {
            _gameTime = 0f;
            _gameYear = 1;
            _pendingTimeData = null;
            
            if (_config.PrintDebugOutput)
                _logger.LogInfo("TimeSync reset");
        }
        
        /// <summary>
        /// Obtiene el tiempo actual del juego
        /// </summary>
        public float GetGameTime()
        {
            return _gameTime;
        }
        
        /// <summary>
        /// Obtiene el año actual del juego
        /// </summary>
        public int GetGameYear()
        {
            return _gameYear;
        }
    }
}