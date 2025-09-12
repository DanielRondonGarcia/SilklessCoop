using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using System.Linq;
using BepInEx.Logging;

namespace SilklessCoop
{
    /// <summary>
    /// Maneja la sincronización del inventario entre jugadores
    /// Incluye objetos, geo, llaves, y otros elementos del inventario
    /// </summary>
    public class InventorySync : MonoBehaviour
    {
        private ManualLogSource _logger;
        private ModConfig _config;
        private SteamConnector _connector;
        private GameSync _gameSync;
        
        // Sistema de inventario
        private PlayerData _playerData;
        private Dictionary<string, bool> _lastInventoryState;
        private int _lastGeoCount;
        private Dictionary<string, int> _lastItemCounts;
        
        // Datos pendientes
        private List<string> _pendingInventoryData;
        
        // Control de sincronización
        private float _lastInventorySyncTime;
        private const float INVENTORY_SYNC_INTERVAL = 2.0f; // Sincronizar cada 2 segundos
        
        private bool _initialized = false;
        
        void Start()
        {
            _connector = FindObjectOfType<SteamConnector>();
            _gameSync = FindObjectOfType<GameSync>();
            
            _lastInventoryState = new Dictionary<string, bool>();
            _lastItemCounts = new Dictionary<string, int>();
            _pendingInventoryData = new List<string>();
        }
        
        internal void Initialize(ManualLogSource logger, ModConfig config)
        {
            _logger = logger;
            _config = config;
            
            // Asegurar que las colecciones estén inicializadas
            if (_lastInventoryState == null)
                _lastInventoryState = new Dictionary<string, bool>();
            if (_lastItemCounts == null)
                _lastItemCounts = new Dictionary<string, int>();
            if (_pendingInventoryData == null)
                _pendingInventoryData = new List<string>();
            
            try
            {
                InitializeInventorySync();
                _initialized = true;
                _logger?.LogInfo("InventorySync inicializado correctamente");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error inicializando InventorySync: {ex.Message}");
            }
        }
        
        private void InitializeInventorySync()
        {
            try
            {
                // Obtener referencia a PlayerData usando reflexión
                var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                if (playerDataType != null)
                {
                    var instanceProperty = playerDataType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        _playerData = instanceProperty.GetValue(null) as PlayerData;
                        if (_playerData != null)
                        {
                            _logger?.LogInfo("PlayerData obtenido exitosamente para InventorySync");
                            CaptureInitialInventoryState();
                        }
                        else
                        {
                            _logger?.LogWarning("PlayerData instance es null, reintentando más tarde");
                        }
                    }
                    else
                    {
                        _logger?.LogWarning("No se encontró la propiedad 'instance' en PlayerData");
                    }
                }
                else
                {
                    _logger?.LogWarning("No se pudo encontrar el tipo PlayerData");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error en InitializeInventorySync: {ex.Message}");
            }
        }
        
        private void CaptureInitialInventoryState()
        {
            if (_playerData == null) return;
            
            try
            {
                // Capturar estado inicial del geo
                _lastGeoCount = _playerData.geo;
                
                // Capturar estado inicial de objetos importantes
                var importantItems = new string[]
                {
                    "hasLantern", "hasDash", "hasWalljump", "hasDoubleJump", "hasSuperDash",
                    "hasAcidArmour", "hasMap", "hasQuill", "gotCharm_1", "gotCharm_2",
                    "dreamNailUpgraded", "hasDreamNail", "hasKingsBrand", "hasVoidHeart"
                };
                
                foreach (string item in importantItems)
                {
                    var field = _playerData.GetType().GetField(item, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        _lastInventoryState[item] = (bool)field.GetValue(_playerData);
                    }
                }
                
                _logger?.LogInfo($"Estado inicial del inventario capturado: {_lastInventoryState.Count} objetos, {_lastGeoCount} geo");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error capturando estado inicial del inventario: {ex.Message}");
            }
        }
        
        void Update()
        {
            if (!_initialized) return;
            
            // Si PlayerData es null, intentar reinicializar
            if (_playerData == null)
            {
                if (Time.time - _lastInventorySyncTime >= 5.0f) // Reintentar cada 5 segundos
                {
                    InitializeInventorySync();
                    _lastInventorySyncTime = Time.time;
                }
                return;
            }
            
            try
            {
                if (Time.time - _lastInventorySyncTime >= INVENTORY_SYNC_INTERVAL)
                {
                    SyncInventory();
                    _lastInventorySyncTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error en Update de InventorySync: {ex.Message}");
            }
        }
        
        private void SyncInventory()
        {
            try
            {
                // Verificación adicional de seguridad
                if (_playerData == null)
                {
                    _logger?.LogWarning("PlayerData es null en SyncInventory, saltando sincronización");
                    return;
                }
                
                bool hasChanges = false;
                var inventoryUpdate = new Dictionary<string, object>();
                
                // Verificar cambios en geo
                int currentGeo = _playerData.geo;
                if (currentGeo != _lastGeoCount)
                {
                    inventoryUpdate["geo"] = currentGeo;
                    _lastGeoCount = currentGeo;
                    hasChanges = true;
                }
                
                // Verificar cambios en objetos del inventario
                if (_lastInventoryState != null)
                {
                    foreach (var kvp in _lastInventoryState.ToList())
                {
                    var field = _playerData.GetType().GetField(kvp.Key, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null)
                    {
                        bool currentValue = (bool)field.GetValue(_playerData);
                        if (currentValue != kvp.Value)
                        {
                            inventoryUpdate[kvp.Key] = currentValue;
                            _lastInventoryState[kvp.Key] = currentValue;
                            hasChanges = true;
                        }
                    }
                }
                }
                
                if (hasChanges)
                {
                    string inventoryData = SerializeInventoryData(inventoryUpdate);
                    _pendingInventoryData.Add(inventoryData);
                    _logger?.LogInfo($"Cambios de inventario detectados: {inventoryUpdate.Count} elementos");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sincronizando inventario: {ex.Message}");
            }
        }
        
        private string SerializeInventoryData(Dictionary<string, object> inventoryUpdate)
        {
            try
            {
                var parts = new List<string>();
                foreach (var kvp in inventoryUpdate)
                {
                    parts.Add($"{kvp.Key}:{kvp.Value}");
                }
                return string.Join("|", parts);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error serializando datos de inventario: {ex.Message}");
                return "";
            }
        }
        
        public string GetPendingData()
        {
            try
            {
                if (_pendingInventoryData.Count == 0) return "";
                
                var dataToSend = new List<string>();
                
                // Agregar datos de inventario
                foreach (string inventoryData in _pendingInventoryData)
                {
                    dataToSend.Add($"INVENTORY::{inventoryData}");
                }
                
                // Limpiar datos pendientes
                _pendingInventoryData.Clear();
                
                return dataToSend.Count > 0 ? string.Join("\n", dataToSend) : "";
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error obteniendo datos pendientes de inventario: {ex.Message}");
                return "";
            }
        }
        
        public void ApplyReceivedData(string data)
        {
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                
                string[] parts = data.Split('|');
                foreach (string part in parts)
                {
                    if (part.Contains(":"))
                    {
                        string[] itemParts = part.Split(':');
                        if (itemParts.Length == 2)
                        {
                            ApplyInventoryUpdate(itemParts[0], itemParts[1]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error aplicando datos de inventario recibidos: {ex.Message}");
            }
        }
        
        private void ApplyInventoryUpdate(string itemName, string value)
        {
            try
            {
                if (_playerData == null) return;
                
                if (itemName == "geo")
                {
                    if (int.TryParse(value, out int geoValue))
                    {
                        _playerData.geo = geoValue;
                        _lastGeoCount = geoValue;
                        _logger?.LogInfo($"Geo actualizado a: {geoValue}");
                    }
                }
                else
                {
                    var field = _playerData.GetType().GetField(itemName, BindingFlags.Public | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        if (bool.TryParse(value, out bool boolValue))
                        {
                            field.SetValue(_playerData, boolValue);
                            _lastInventoryState[itemName] = boolValue;
                            _logger?.LogInfo($"Objeto {itemName} actualizado a: {boolValue}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error aplicando actualización de inventario para {itemName}: {ex.Message}");
            }
        }
        
        public void Reset()
        {
            try
            {
                _lastInventoryState.Clear();
                _lastItemCounts.Clear();
                _pendingInventoryData.Clear();
                _lastGeoCount = 0;
                _lastInventorySyncTime = 0f;
                _initialized = false;
                
                _logger?.LogInfo("InventorySync reseteado");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error reseteando InventorySync: {ex.Message}");
            }
        }
    }
}