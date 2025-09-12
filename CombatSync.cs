using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace SilklessCoop
{
    /// <summary>
    /// Sistema de sincronización de combate, salud y enemigos para modo cooperativo
    /// Sincroniza salud del jugador, posiciones de enemigos, daño y estados de combate
    /// </summary>
    internal class CombatSync : MonoBehaviour
    {
        public ManualLogSource Logger;
        public ModConfig Config;
        
        private Connector _connector;
        private GameSync _gameSync;
        private AudioSync _audioSync = null;
        private EnemyHealthBar _enemyHealthBar = null;
        private GameProgressSync _progressSync = null;
        
        // Sistema de salud de múltiples jugadores
        private Dictionary<string, PlayerCharacter> _playerCharacters = new Dictionary<string, PlayerCharacter>();
        private string _hostCharacterId = null;
        private string _guestCharacterId = null;
        
        // Sistema de enemigos con optimización adaptativa
        private Dictionary<string, EnemyData> _trackedEnemies = new Dictionary<string, EnemyData>();
        private Dictionary<string, EnemyData> _remoteEnemies = new Dictionary<string, EnemyData>();
        private float _lastEnemySyncTime = 0f;
        private const float ENEMY_SYNC_INTERVAL_ACTIVE = 0.15f; // 6.7 veces por segundo cuando hay actividad
        private const float ENEMY_SYNC_INTERVAL_IDLE = 0.5f; // 2 veces por segundo cuando no hay actividad
        private float _currentEnemySyncInterval = 0.5f;
        
        // Sistema específico de jefes optimizado
        private Dictionary<string, BossData> _activeBosses = new Dictionary<string, BossData>();
        private Dictionary<string, BossData> _remoteBosses = new Dictionary<string, BossData>();
        private float _lastBossSyncTime = 0f;
        private const float BOSS_SYNC_INTERVAL = 0.1f; // 10 veces por segundo para jefes (crítico)
        
        // Sistema de objetos del mundo (destructibles, interactivos)
        private Dictionary<string, WorldObjectData> _trackedWorldObjects = new Dictionary<string, WorldObjectData>();
        private Dictionary<string, WorldObjectData> _remoteWorldObjects = new Dictionary<string, WorldObjectData>();
        private float _lastWorldObjectSyncTime = 0f;
        private const float WORLD_OBJECT_SYNC_INTERVAL = 1.0f; // 1 vez por segundo para objetos menos críticos
        
        // Sistema de daño y combate optimizado
        private Dictionary<string, DamageEvent> _pendingDamageEvents = new Dictionary<string, DamageEvent>();
        private float _lastCombatSyncTime = 0f;
        private const float COMBAT_SYNC_INTERVAL = 0.1f; // 10 veces por segundo para combate (crítico)
        
        // Optimización: Umbrales de cambio para evitar spam
        private const float POSITION_CHANGE_THRESHOLD = 0.1f; // Mínimo cambio de posición para sincronizar
        private const float HEALTH_CHANGE_THRESHOLD = 1f; // Mínimo cambio de salud para sincronizar
        private const float ROTATION_CHANGE_THRESHOLD = 5f; // Mínimo cambio de rotación en grados
        
        // Optimización: Batching de actualizaciones
        private const int MAX_UPDATES_PER_FRAME = 5; // Máximo número de actualizaciones por frame
        private int _updatesThisFrame = 0;
        
        // Optimización: Detección de actividad
        private float _lastActivityTime = 0f;
        private const float ACTIVITY_TIMEOUT = 3f; // Tiempo sin actividad para reducir frecuencia
        
        // Datos pendientes para envío
        private string _pendingHealthData;
        private List<string> _pendingEnemyData = new List<string>();
        private List<string> _pendingCombatEvents = new List<string>();
        private List<string> _pendingBossData = new List<string>();
        private List<string> _pendingWorldObjectData = new List<string>();
        
        // Sistema de tracking de jugadores para activación automática de jefes
        private Dictionary<string, Vector3> _playersInBossArea = new Dictionary<string, Vector3>();
        private float _lastPlayerTrackingTime = 0f;
        private const float PLAYER_TRACKING_INTERVAL = 0.5f; // 2 veces por segundo
        private const float BOSS_AREA_RADIUS = 15f; // Radio para detectar jugadores cerca de jefes
        private float _lastBossActivationCheck = 0f;
        private const float BOSS_ACTIVATION_CHECK_INTERVAL = 1f; // 1 vez por segundo
        
        // Arquitectura Host-Cliente: Variables de tiempo y estado
        private float _lastWorldStateBroadcast = 0f;
        private float _lastPlayerInputSent = 0f;
        private int _worldStateSequenceNumber = 0;
        
        // Sistema de GameObjects remotos
        private Dictionary<string, GameObject> _remotePlayerObjects = new Dictionary<string, GameObject>();
        private Dictionary<string, Color> _playerColors = new Dictionary<string, Color>();
        private List<Color> _availableColors = new List<Color>
        {
            new Color(0.2f, 0.8f, 1.0f, 0.8f), // Azul claro
            new Color(1.0f, 0.4f, 0.4f, 0.8f), // Rojo claro
            new Color(0.4f, 1.0f, 0.4f, 0.8f), // Verde claro
            new Color(1.0f, 0.8f, 0.2f, 0.8f), // Amarillo
            new Color(1.0f, 0.4f, 1.0f, 0.8f), // Magenta
            new Color(0.4f, 1.0f, 1.0f, 0.8f), // Cian
        };
        private int _nextColorIndex = 0;
        
        private bool _initialized = false;

        /// <summary>
        /// Métodos de parsing seguro para evitar excepciones
        /// </summary>
        private float SafeParseFloat(string value, string fieldName)
        {
            if (float.TryParse(value, out float result))
                return result;
            
            Logger.LogWarning($"Failed to parse float for {fieldName}: '{value}', using default 0");
            return 0f;
        }
        
        private int SafeParseInt(string value, string fieldName)
        {
            if (int.TryParse(value, out int result))
                return result;
            
            Logger.LogWarning($"Failed to parse int for {fieldName}: '{value}', using default 0");
            return 0;
        }
        
        private bool SafeParseBool(string value, string fieldName)
        {
            if (bool.TryParse(value, out bool result))
                return result;
            
            Logger.LogWarning($"Failed to parse bool for {fieldName}: '{value}', using default false");
            return false;
        }

        private void Start()
        {
            _connector = GetComponent<Connector>();
            _gameSync = GetComponent<GameSync>();
            _audioSync = GetComponent<AudioSync>();
            _progressSync = GetComponent<GameProgressSync>();
            
            // Inicializar sistema de barras de vida de enemigos
            _enemyHealthBar = gameObject.AddComponent<EnemyHealthBar>();
            _enemyHealthBar.Logger = Logger;
            _enemyHealthBar.Config = Config;
        }
        
        private void Update()
        {
            if (!_initialized)
            {
                InitializeCombatSync();
                return;
            }
            
            if (_connector == null || !_connector.Active)
                return;
            
            // Arquitectura Host-Cliente: Separar lógica según el rol
            if (_connector.IsHost)
            {
                UpdateHost();
            }
            else if (_connector.IsClient)
            {
                UpdateClient();
            }
        }
        
        /// <summary>
        /// Lógica de actualización para el HOST (controla el mundo)
        /// </summary>
        private void UpdateHost()
        {
            // Optimización: Resetear contador de actualizaciones por frame
            _updatesThisFrame = 0;
            
            // Solo el HOST procesa toda la lógica del mundo
            
            // Optimización: Detectar actividad y ajustar intervalos
            UpdateActivityDetection();
            
            // Sincronizar salud del jugador (siempre crítico)
            if (_updatesThisFrame < MAX_UPDATES_PER_FRAME)
            {
                SyncPlayerHealth();
                _updatesThisFrame++;
            }
            
            // Procesar enemigos con intervalo adaptativo
            if (Time.time - _lastEnemySyncTime >= _currentEnemySyncInterval && _updatesThisFrame < MAX_UPDATES_PER_FRAME)
            {
                ProcessEnemies();
                _lastEnemySyncTime = Time.time;
                _updatesThisFrame++;
            }
            
            // Procesar combate (alta prioridad)
            if (Time.time - _lastCombatSyncTime >= COMBAT_SYNC_INTERVAL && _updatesThisFrame < MAX_UPDATES_PER_FRAME)
            {
                ProcessCombat();
                _lastCombatSyncTime = Time.time;
                _updatesThisFrame++;
            }
            
            // Procesar objetos del mundo (baja prioridad)
            if (Time.time - _lastWorldObjectSyncTime >= WORLD_OBJECT_SYNC_INTERVAL && _updatesThisFrame < MAX_UPDATES_PER_FRAME)
            {
                ProcessWorldObjects();
                _lastWorldObjectSyncTime = Time.time;
                _updatesThisFrame++;
            }
            
            // Tracking de jugadores para activación automática de jefes
            if (Time.time - _lastPlayerTrackingTime >= PLAYER_TRACKING_INTERVAL)
            {
                UpdatePlayerProximityToBosses();
                _lastPlayerTrackingTime = Time.time;
            }
            
            // Verificar activación automática de jefes
            if (Time.time - _lastBossActivationCheck >= BOSS_ACTIVATION_CHECK_INTERVAL)
            {
                CheckAndActivateInactiveBosses();
                _lastBossActivationCheck = Time.time;
            }
            
            // Procesar jefes
            if (Time.time - _lastBossSyncTime >= BOSS_SYNC_INTERVAL)
            {
                ProcessBosses(); // Renombrado de SyncBosses
                _lastBossSyncTime = Time.time;
            }
            
            // Enviar estado del mundo a todos los clientes
            if (Time.time - _lastWorldStateBroadcast >= (1f / WorldStateConfig.WORLD_STATE_FREQUENCY))
            {
                BroadcastWorldState();
                _lastWorldStateBroadcast = Time.time;
            }
        }
        
        /// <summary>
        /// Lógica de actualización para el CLIENTE (solo recibe estado)
        /// </summary>
        private void UpdateClient()
        {
            // Los clientes solo envían input del jugador al host
            if (Time.time - _lastPlayerInputSent >= (1f / WorldStateConfig.PLAYER_INPUT_FREQUENCY))
            {
                SendPlayerInput();
                _lastPlayerInputSent = Time.time;
            }
            
            // Los clientes NO procesan lógica del mundo
            // Solo aplican el estado recibido del host
        }
        
        /// <summary>
        /// Inicializa el sistema de sincronización de combate para múltiples personajes
        /// </summary>
        private void InitializeCombatSync()
        {
            try
            {
                // Buscar Hornet (personaje principal)
                GameObject hornetController = GameObject.Find("Hero_Hornet");
                if (!hornetController)
                    hornetController = GameObject.Find("Hero_Hornet(Clone)");
                    
                // Intentar acceder a PlayerData usando reflexión
                var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                object playerDataInstance = null;
                
                if (playerDataType != null)
                {
                    var instanceProperty = playerDataType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        playerDataInstance = instanceProperty.GetValue(null);
                    }
                }
                
                // Inicializar jugadores según el rol
                if (hornetController != null)
                {
                    if (_connector.IsHost)
                    {
                        // HOST: Inicializar como Hornet
                        var hornetCharacter = new PlayerCharacter
                        {
                            Id = "hornet_host",
                            Name = "Hornet",
                            Type = CharacterType.Hornet,
                            Controller = hornetController,
                            PlayerData = playerDataInstance,
                            IsHost = true,
                            IsActive = true
                        };
                        
                        _playerCharacters["hornet_host"] = hornetCharacter;
                        _hostCharacterId = "hornet_host";
                        
                        if (Config.PrintDebugOutput)
                        {
                            Logger.LogInfo("🏠 HOST: Hornet initialized successfully - ACTIVE");
                            Logger.LogInfo($"🏠 HOST: Ready to receive clients at {WorldStateConfig.WORLD_STATE_FREQUENCY}Hz");
                        }
                    }
                    else if (_connector.IsClient)
                    {
                        // CLIENT: Inicializar como Knight
                        var knightCharacter = new PlayerCharacter
                        {
                            Id = "knight_client",
                            Name = "The_Knight",
                            Type = CharacterType.Knight,
                            Controller = hornetController,
                            PlayerData = playerDataInstance,
                            IsHost = false,
                            IsActive = true // Cliente activo para enviar input
                        };
                        
                        _playerCharacters["knight_client"] = knightCharacter;
                        _guestCharacterId = "knight_client";
                        
                        if (Config.PrintDebugOutput)
                        {
                            Logger.LogInfo("🎮 CLIENT: Knight initialized successfully - ACTIVE");
                            Logger.LogInfo($"🎮 CLIENT: Ready to send input at {WorldStateConfig.PLAYER_INPUT_FREQUENCY}Hz");
                        }
                    }
                }
                
                // Marcar como inicializado si al menos un personaje está disponible
                if (_playerCharacters.Count > 0)
                {
                    _initialized = true;
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"CombatSync initialized with {_playerCharacters.Count} character(s) as {_connector.Role}");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error initializing CombatSync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cambia entre personajes disponibles
        /// </summary>
        public void SwitchCharacter()
        {
            try
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"SwitchCharacter called. Initialized: {_initialized}, Character count: {_playerCharacters.Count}");
                    
                if (!_initialized || _playerCharacters.Count < 2)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Cannot switch character: initialized={_initialized}, count={_playerCharacters.Count}");
                    return;
                }
                
                // Encontrar el personaje activo actual
                var currentActive = _playerCharacters.Values.FirstOrDefault(c => c.IsActive);
                if (currentActive == null)
                {
                    // Si no hay personaje activo, activar el primero
                    var firstCharacter = _playerCharacters.Values.First();
                    firstCharacter.IsActive = true;
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Activated {firstCharacter.Name} as first character");
                    
                    // Aplicar el cambio visual del sprite
                    ApplyCharacterVisualChange(firstCharacter);
                    return;
                }
                
                // Desactivar el personaje actual
                currentActive.IsActive = false;
                
                // Encontrar el siguiente personaje
                var characterList = _playerCharacters.Values.ToList();
                int currentIndex = characterList.IndexOf(currentActive);
                int nextIndex = (currentIndex + 1) % characterList.Count;
                
                var nextCharacter = characterList[nextIndex];
                nextCharacter.IsActive = true;
                
                // Aplicar el cambio visual del sprite
                ApplyCharacterVisualChange(nextCharacter);
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Switched from {currentActive.Name} to {nextCharacter.Name}");
                    
                // Notificar al AudioSync sobre el cambio
                if (_audioSync != null)
                {
                    // Aquí se podría agregar lógica específica para el AudioSync si es necesario
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error switching character: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sincroniza la salud de todos los personajes activos
        /// </summary>
        private void SyncPlayerHealth()
        {
            try
            {
                foreach (var character in _playerCharacters.Values)
                {
                    if (!character.IsActive || character.PlayerData == null) continue;
                    
                    var playerDataType = character.PlayerData.GetType();
                    
                    // Obtener salud actual
                    var healthField = playerDataType.GetField("health");
                    var maxHealthField = playerDataType.GetField("maxHealth");
                    
                    if (healthField != null && maxHealthField != null)
                    {
                        int currentHealth = (int)healthField.GetValue(character.PlayerData);
                        int maxHealth = (int)maxHealthField.GetValue(character.PlayerData);
                        
                        // Verificar si la salud ha cambiado
                        if (currentHealth != character.LastHealth || maxHealth != character.LastMaxHealth)
                        {
                            character.LastHealth = currentHealth;
                            character.LastMaxHealth = maxHealth;
                            
                            // Actualizar posición del personaje
                            if (character.Controller != null)
                            {
                                character.LastPosition = character.Controller.transform.position;
                            }
                            
                            // Preparar datos para envío con identificación del personaje
                            string characterType = character.IsHost ? "HOST" : "GUEST";
                            _pendingHealthData = $"HEALTH::{character.Id}::{characterType}::{character.Type}::{currentHealth}::{maxHealth}::{character.LastPosition.x}::{character.LastPosition.y}::{character.LastPosition.z}";
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"{character.Name} health changed: {currentHealth}/{maxHealth} at {character.LastPosition}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error syncing player health: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa enemigos en la escena (solo HOST)
        /// </summary>
        private void ProcessEnemies()
        {
            try
            {
                // Buscar todos los GameObjects que podrían ser enemigos
                var potentialEnemies = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains("Enemy") || 
                                go.name.Contains("Boss") || 
                                go.name.Contains("Gruz") ||
                                go.name.Contains("Crawler") ||
                                go.name.Contains("Fly") ||
                                go.tag == "Enemy")
                    .ToList();
                
                var currentEnemies = new Dictionary<string, EnemyData>();
                
                foreach (var enemy in potentialEnemies)
                {
                    if (enemy == null) continue;
                    
                    string enemyId = $"{enemy.name}_{enemy.GetInstanceID()}";
                    int currentHealth = GetEnemyHealth(enemy);
                    bool isActive = enemy.activeInHierarchy;
                    bool isDead = !isActive || currentHealth <= 0;
                    
                    var enemyData = new EnemyData
                    {
                        Id = enemyId,
                        Name = enemy.name,
                        Position = enemy.transform.position,
                        IsActive = isActive,
                        Health = currentHealth,
                        IsDead = isDead,
                        DeathTime = isDead ? Time.time : 0f
                    };
                    
                    currentEnemies[enemyId] = enemyData;
                }
                
                // Verificar cambios en enemigos
                if (HasEnemyDataChanged(currentEnemies))
                {
                    _trackedEnemies = currentEnemies;
                    string enemyData = SerializeEnemyData(currentEnemies);
                    _pendingEnemyData.Add(enemyData);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Enemy data changed, tracking {currentEnemies.Count} enemies");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error syncing enemies: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa objetos del mundo (destructibles, interactivos) - solo HOST
        /// </summary>
        private void ProcessWorldObjects()
        {
            try
            {
                if (Time.time - _lastWorldObjectSyncTime < WORLD_OBJECT_SYNC_INTERVAL)
                    return;
                
                _lastWorldObjectSyncTime = Time.time;
                
                // Buscar objetos destructibles y interactivos
                var potentialWorldObjects = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains("Breakable") ||
                                go.name.Contains("Destructible") ||
                                go.name.Contains("Shiny") ||
                                go.name.Contains("Geo") ||
                                go.name.Contains("Chest") ||
                                go.name.Contains("Switch") ||
                                go.name.Contains("Door") ||
                                go.name.Contains("Lever") ||
                                go.name.Contains("Crystal") ||
                                go.name.Contains("Rock") ||
                                go.tag == "Destructible" ||
                                go.tag == "Interactive")
                    .ToList();
                
                var currentWorldObjects = new Dictionary<string, WorldObjectData>();
                
                foreach (var obj in potentialWorldObjects)
                {
                    if (obj == null) continue;
                    
                    string objectId = $"{obj.name}_{obj.GetInstanceID()}";
                    bool isActive = obj.activeInHierarchy;
                    bool isDestroyed = !isActive;
                    
                    // Determinar tipo de objeto
                    string objectType = "unknown";
                    if (obj.name.Contains("Breakable") || obj.name.Contains("Destructible") || obj.name.Contains("Crystal") || obj.name.Contains("Rock"))
                        objectType = "destructible";
                    else if (obj.name.Contains("Shiny") || obj.name.Contains("Geo"))
                        objectType = "collectible";
                    else if (obj.name.Contains("Switch") || obj.name.Contains("Lever"))
                        objectType = "switch";
                    else if (obj.name.Contains("Door"))
                        objectType = "door";
                    else if (obj.name.Contains("Chest"))
                        objectType = "chest";
                    
                    var worldObjectData = new WorldObjectData
                    {
                        Id = objectId,
                        Name = obj.name,
                        Position = obj.transform.position,
                        IsActive = isActive,
                        IsDestroyed = isDestroyed,
                        IsInteracted = false, // TODO: Detectar interacción
                        ObjectType = objectType
                    };
                    
                    currentWorldObjects[objectId] = worldObjectData;
                }
                
                // Verificar cambios en objetos del mundo
                if (HasWorldObjectDataChanged(currentWorldObjects))
                {
                    // Registrar eventos del mundo con GameProgressSync
                    RegisterWorldEventsWithProgressSync(currentWorldObjects);
                    
                    _trackedWorldObjects = currentWorldObjects;
                    string worldObjectData = SerializeWorldObjectData(currentWorldObjects);
                    _pendingWorldObjectData.Add(worldObjectData);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"World object data changed, tracking {currentWorldObjects.Count} objects");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error syncing world objects: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Registra eventos del mundo con el sistema de progreso
        /// </summary>
        private void RegisterWorldEventsWithProgressSync(Dictionary<string, WorldObjectData> currentWorldObjects)
        {
            if (_progressSync == null) return;
            
            try
            {
                foreach (var kvp in currentWorldObjects)
                {
                    var worldObject = kvp.Value;
                    var previousObject = _trackedWorldObjects.ContainsKey(kvp.Key) ? _trackedWorldObjects[kvp.Key] : null;
                    
                    // Detectar cambios de estado
                    if (previousObject != null)
                    {
                        // Objeto destruido
                        if (!previousObject.IsDestroyed && worldObject.IsDestroyed)
                        {
                            string eventKey = $"destroyed_{worldObject.ObjectType}_{worldObject.Name}";
                            _progressSync.RegisterWorldEvent("destroyed", $"{worldObject.ObjectType}_{worldObject.Name}", true);
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Registered destruction event: {eventKey}");
                        }
                        
                        // Objeto desactivado (coleccionado)
                        if (previousObject.IsActive && !worldObject.IsActive && worldObject.ObjectType == "collectible")
                        {
                            string eventKey = $"collected_{worldObject.Name}";
                            _progressSync.RegisterWorldEvent("collected", worldObject.Name, true);
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Registered collection event: {eventKey}");
                        }
                        
                        // Interacción con switches/doors
                        if (!previousObject.IsInteracted && worldObject.IsInteracted)
                        {
                            string eventKey = $"interacted_{worldObject.ObjectType}_{worldObject.Name}";
                            _progressSync.RegisterWorldEvent("interacted", $"{worldObject.ObjectType}_{worldObject.Name}", true);
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Registered interaction event: {eventKey}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error registering world events: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa eventos de combate (solo HOST)
        /// </summary>
        private void ProcessCombat()
        {
            try
            {
                // Detectar eventos de combate y sincronizar audio
                DetectCombatEvents();
                
                if (_pendingDamageEvents.Count > 0)
                {
                    string combatData = SerializeCombatData(_pendingDamageEvents);
                    _pendingCombatEvents.Add(combatData);
                    _pendingDamageEvents.Clear();
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo("Combat events synchronized");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error syncing combat: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detecta eventos de combate y sincroniza audio
        /// </summary>
        private void DetectCombatEvents()
        {
            try
            {
                if (_audioSync == null) return;
                
                // Detectar ataques de todos los personajes activos
                foreach (var character in _playerCharacters.Values)
                {
                    if (!character.IsActive || character.Controller == null) continue;
                    
                    var audioSources = character.Controller.GetComponentsInChildren<AudioSource>();
                    foreach (var source in audioSources)
                    {
                        if (source.isPlaying && source.clip != null)
                        {
                            string clipName = source.clip.name.ToLower();
                            string characterPrefix = character.IsHost ? "host" : "guest";
                            string characterName = character.Type.ToString().ToLower();
                            
                            // Detectar sonidos de ataque específicos por personaje
                            if (IsAttackSound(clipName, character.Type))
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"{characterPrefix}_{characterName}_attack_{clipName}",
                                    source.clip,
                                    character.Controller.transform.position,
                                    source.volume,
                                    source.pitch
                                );
                            }
                            
                            // Detectar sonidos de daño recibido
                            if (IsDamageSound(clipName, character.Type))
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"{characterPrefix}_{characterName}_damage_{clipName}",
                                    source.clip,
                                    character.Controller.transform.position,
                                    source.volume,
                                    source.pitch
                                );
                            }
                            
                            // Detectar sonidos específicos del personaje
                            if (IsCharacterSpecificSound(clipName, character.Type))
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"{characterPrefix}_{characterName}_special_{clipName}",
                                    source.clip,
                                    character.Controller.transform.position,
                                    source.volume,
                                    source.pitch
                                );
                            }
                        }
                    }
                }
                
                // Detectar ataques de enemigos
                foreach (var enemy in _trackedEnemies.Values)
                {
                    var enemyObject = FindObjectsOfType<GameObject>()
                        .FirstOrDefault(go => go.GetInstanceID().ToString() == enemy.Id.Split('_').LastOrDefault());
                    
                    if (enemyObject != null)
                    {
                        var audioSources = enemyObject.GetComponentsInChildren<AudioSource>();
                        foreach (var source in audioSources)
                        {
                            if (source.isPlaying && source.clip != null)
                            {
                                string clipName = source.clip.name.ToLower();
                                
                                // Detectar sonidos de ataque de enemigos
                                if (clipName.Contains("attack") || clipName.Contains("roar") || 
                                    clipName.Contains("charge") || clipName.Contains("strike"))
                                {
                                    _audioSync.RegisterAudioEvent(
                                        $"enemy_attack_{enemy.Name}_{clipName}",
                                        source.clip,
                                        enemyObject.transform.position,
                                        source.volume,
                                        source.pitch
                                    );
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error detecting combat events: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa jefes activos (solo HOST)
        /// </summary>
        private void ProcessBosses()
        {
            try
            {
                // Buscar todos los jefes activos
                var potentialBosses = FindObjectsOfType<GameObject>()
                    .Where(go => (go.name.Contains("Boss") || 
                                 go.name.Contains("Gruz Mother") ||
                                 go.name.Contains("False Knight") ||
                                 go.name.Contains("Hornet") ||
                                 go.name.Contains("Mantis Lord")) && 
                                go.activeInHierarchy)
                    .ToList();
                
                var currentBosses = new Dictionary<string, BossData>();
                
                foreach (var boss in potentialBosses)
                {
                    if (boss == null || !boss.activeInHierarchy) continue;
                    
                    string bossId = $"{boss.name}_{boss.GetInstanceID()}";
                    var bossData = new BossData
                    {
                        Id = bossId,
                        Name = boss.name,
                        Position = boss.transform.position,
                        IsActive = boss.activeInHierarchy,
                        Health = GetEnemyHealth(boss),
                        MaxHealth = GetEnemyMaxHealth(boss),
                        CurrentPhase = GetBossPhase(boss),
                        IsInCombat = IsBossInCombat(boss),
                        LastAttackTime = GetBossLastAttackTime(boss),
                        CurrentAttack = GetBossCurrentAttack(boss)
                    };
                    
                    currentBosses[bossId] = bossData;
                    
                    // Notificar al AudioSync sobre actividad del jefe
                    if (_audioSync != null && bossData.IsInCombat)
                    {
                        NotifyBossActivity(boss, bossData);
                    }
                }
                
                // Verificar cambios en jefes
                if (HasBossDataChanged(currentBosses))
                {
                    _activeBosses = currentBosses;
                    string bossData = SerializeBossData(currentBosses);
                    _pendingBossData.Add(bossData);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Boss data changed, tracking {currentBosses.Count} bosses");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error syncing bosses: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Notifica al AudioSync sobre actividad del jefe
        /// </summary>
        private void NotifyBossActivity(GameObject boss, BossData bossData)
        {
            try
            {
                if (_audioSync == null) return;
                
                // Detectar cambios importantes que requieren sincronización de audio
                if (_activeBosses.ContainsKey(bossData.Id))
                {
                    var previousData = _activeBosses[bossData.Id];
                    
                    // Cambio de fase
                    if (previousData.CurrentPhase != bossData.CurrentPhase)
                    {
                        var audioSources = boss.GetComponentsInChildren<AudioSource>();
                        foreach (var source in audioSources)
                        {
                            if (source.isPlaying && source.clip != null)
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"boss_phase_{bossData.Name}",
                                    source.clip,
                                    boss.transform.position,
                                    source.volume,
                                    source.pitch
                                );
                            }
                        }
                    }
                    
                    // Nuevo ataque
                    if (previousData.CurrentAttack != bossData.CurrentAttack && !string.IsNullOrEmpty(bossData.CurrentAttack))
                    {
                        var audioSources = boss.GetComponentsInChildren<AudioSource>();
                        foreach (var source in audioSources)
                        {
                            if (source.isPlaying && source.clip != null)
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"boss_attack_{bossData.Name}",
                                    source.clip,
                                    boss.transform.position,
                                    source.volume,
                                    source.pitch
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error notifying boss activity: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene la salud de un enemigo usando reflexión
        /// </summary>
        private int GetEnemyHealth(GameObject enemy)
        {
            try
            {
                // Buscar componentes que podrían contener salud
                var healthManager = enemy.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    var healthField = healthManager.GetType().GetField("hp");
                    if (healthField != null)
                    {
                        return (int)healthField.GetValue(healthManager);
                    }
                }
                
                return -1; // Salud desconocida
            }
            catch
            {
                return -1;
            }
        }
        
        /// <summary>
        /// Obtiene la salud máxima de un enemigo usando reflexión
        /// </summary>
        private int GetEnemyMaxHealth(GameObject enemy)
        {
            try
            {
                var healthManager = enemy.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    var maxHpField = healthManager.GetType().GetField("hp", BindingFlags.Public | BindingFlags.Instance);
                    if (maxHpField != null)
                    {
                        return (int)maxHpField.GetValue(healthManager);
                    }
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }
        
        /// <summary>
        /// Obtiene la fase actual del jefe
        /// </summary>
        private int GetBossPhase(GameObject boss)
        {
            try
            {
                // Buscar componentes específicos de fases de jefes
                var components = boss.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    // Buscar campos relacionados con fases
                    var phaseField = type.GetField("phase", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                   type.GetField("currentPhase", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                   type.GetField("Phase", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (phaseField != null && phaseField.FieldType == typeof(int))
                    {
                        return (int)phaseField.GetValue(component);
                    }
                }
                return 1; // Fase por defecto
            }
            catch
            {
                return 1;
            }
        }
        
        /// <summary>
        /// Verifica si el jefe está en combate
        /// </summary>
        private bool IsBossInCombat(GameObject boss)
        {
            try
            {
                var components = boss.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    // Buscar campos relacionados con estado de combate
                    var combatField = type.GetField("inCombat", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                    type.GetField("isActive", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                    type.GetField("activated", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (combatField != null && combatField.FieldType == typeof(bool))
                    {
                        return (bool)combatField.GetValue(component);
                    }
                }
                
                // Verificar si tiene un HealthManager activo
                var healthManager = boss.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    var hp = GetEnemyHealth(boss);
                    var maxHp = GetEnemyMaxHealth(boss);
                    return hp > 0 && hp < maxHp; // En combate si ha perdido vida
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Obtiene el tiempo del último ataque del jefe
        /// </summary>
        private float GetBossLastAttackTime(GameObject boss)
        {
            try
            {
                var components = boss.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    var attackTimeField = type.GetField("lastAttackTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                        type.GetField("attackTimer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (attackTimeField != null && attackTimeField.FieldType == typeof(float))
                    {
                        return (float)attackTimeField.GetValue(component);
                    }
                }
                return Time.time;
            }
            catch
            {
                return Time.time;
            }
        }
        
        /// <summary>
        /// Obtiene el ataque actual del jefe
        /// </summary>
        private string GetBossCurrentAttack(GameObject boss)
        {
            try
            {
                var components = boss.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    var attackField = type.GetField("currentAttack", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                    type.GetField("attackType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                                    type.GetField("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (attackField != null && attackField.FieldType == typeof(string))
                    {
                        return (string)attackField.GetValue(component) ?? "";
                    }
                    
                    if (attackField != null && attackField.FieldType.IsEnum)
                    {
                        return attackField.GetValue(component)?.ToString() ?? "";
                    }
                }
                return "";
            }
            catch
            {
                return "";
            }
        }
        
        /// <summary>
        /// Verifica si los datos de jefes han cambiado
        /// </summary>
        private bool HasBossDataChanged(Dictionary<string, BossData> currentBosses)
        {
            if (_activeBosses.Count != currentBosses.Count)
                return true;
                
            foreach (var kvp in currentBosses)
            {
                if (!_activeBosses.ContainsKey(kvp.Key))
                    return true;
                    
                var tracked = _activeBosses[kvp.Key];
                var current = kvp.Value;
                
                // Usar umbrales de optimización para reducir spam de datos
                if (HasPositionChanged(tracked.Position, current.Position) ||
                    tracked.IsActive != current.IsActive ||
                    HasHealthChanged(tracked.Health, current.Health) ||
                    tracked.CurrentPhase != current.CurrentPhase ||
                    tracked.IsInCombat != current.IsInCombat ||
                    tracked.CurrentAttack != current.CurrentAttack)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Serializa datos de jefes para envío
        /// </summary>
        private string SerializeBossData(Dictionary<string, BossData> bosses)
        {
            var parts = new List<string>();
            foreach (var boss in bosses.Values)
            {
                parts.Add($"{boss.Id}|{boss.Name}|{boss.Position.x:F2}|{boss.Position.y:F2}|{boss.IsActive}|{boss.Health}|{boss.MaxHealth}|{boss.CurrentPhase}|{boss.IsInCombat}|{boss.LastAttackTime:F2}|{boss.CurrentAttack}");
            }
            return string.Join(";", parts);
        }
        
        /// <summary>
        /// Verifica si los datos de enemigos han cambiado
        /// </summary>
        private bool HasEnemyDataChanged(Dictionary<string, EnemyData> currentEnemies)
        {
            if (_trackedEnemies.Count != currentEnemies.Count)
                return true;
                
            foreach (var kvp in currentEnemies)
            {
                if (!_trackedEnemies.ContainsKey(kvp.Key))
                    return true;
                    
                var tracked = _trackedEnemies[kvp.Key];
                var current = kvp.Value;
                
                // Usar umbrales de optimización para reducir spam de datos
                if (HasPositionChanged(tracked.Position, current.Position) ||
                    tracked.IsActive != current.IsActive ||
                    HasHealthChanged(tracked.Health, current.Health) ||
                    tracked.IsDead != current.IsDead)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Serializa datos de enemigos para envío
        /// </summary>
        private string SerializeEnemyData(Dictionary<string, EnemyData> enemies)
        {
            var parts = new List<string>();
            foreach (var enemy in enemies.Values)
            {
                parts.Add($"{enemy.Id}|{enemy.Name}|{enemy.Position.x:F2}|{enemy.Position.y:F2}|{enemy.IsActive}|{enemy.Health}|{enemy.IsDead}|{enemy.DeathTime:F2}");
            }
            return string.Join(";", parts);
        }
        
        /// <summary>
        /// Verifica si los datos de objetos del mundo han cambiado
        /// </summary>
        private bool HasWorldObjectDataChanged(Dictionary<string, WorldObjectData> currentWorldObjects)
        {
            if (_trackedWorldObjects.Count != currentWorldObjects.Count)
                return true;
                
            foreach (var kvp in currentWorldObjects)
            {
                if (!_trackedWorldObjects.ContainsKey(kvp.Key))
                    return true;
                    
                var tracked = _trackedWorldObjects[kvp.Key];
                var current = kvp.Value;
                
                if (Vector3.Distance(tracked.Position, current.Position) > 0.1f ||
                    tracked.IsActive != current.IsActive ||
                    tracked.IsDestroyed != current.IsDestroyed ||
                    tracked.IsInteracted != current.IsInteracted ||
                    tracked.ObjectType != current.ObjectType)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Serializa datos de objetos del mundo para envío
        /// </summary>
        private string SerializeWorldObjectData(Dictionary<string, WorldObjectData> worldObjects)
        {
            var parts = new List<string>();
            foreach (var obj in worldObjects.Values)
            {
                parts.Add($"{obj.Id}|{obj.Name}|{obj.Position.x:F2}|{obj.Position.y:F2}|{obj.IsActive}|{obj.IsDestroyed}|{obj.IsInteracted}|{obj.ObjectType}");
            }
            return string.Join(";", parts);
        }
        
        /// <summary>
        /// Serializa datos de combate para envío
        /// </summary>
        private string SerializeCombatData(Dictionary<string, DamageEvent> damageEvents)
        {
            var parts = new List<string>();
            foreach (var dmg in damageEvents.Values)
            {
                parts.Add($"{dmg.SourceId}|{dmg.TargetId}|{dmg.Damage}|{dmg.Timestamp}");
            }
            return string.Join(";", parts);
        }
        
        /// <summary>
        /// Aplica datos de salud recibidos de otros jugadores
        /// </summary>
        public void ApplyReceivedHealthData(string playerId, string healthData)
        {
            try
            {
                var parts = healthData.Split(new string[] { "::" }, StringSplitOptions.None);
                if (parts.Length >= 9 && parts[0] == "HEALTH")
                {
                    string characterId = parts[1];
                    string characterTypeStr = parts[2];
                    string characterTypeName = parts[3];
                    int health = int.Parse(parts[4]);
                    int maxHealth = int.Parse(parts[5]);
                    float posX = float.Parse(parts[6]);
                    float posY = float.Parse(parts[7]);
                    float posZ = float.Parse(parts[8]);
                    
                    Vector3 position = new Vector3(posX, posY, posZ);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Player {playerId} ({characterTypeName}) health: {health}/{maxHealth} at {position}");
                        
                    // Aquí se podría actualizar la UI o efectos visuales para mostrar la salud del otro jugador
                }
                else if (parts.Length >= 3 && parts[0] == "HEALTH")
                {
                    // Compatibilidad con formato anterior
                    int health = int.Parse(parts[1]);
                    int maxHealth = int.Parse(parts[2]);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Player {playerId} health (legacy): {health}/{maxHealth}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying health data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica datos de enemigos recibidos de otros jugadores
        /// </summary>
        public void ApplyReceivedEnemyData(string playerId, string enemyData)
        {
            try
            {
                if (string.IsNullOrEmpty(enemyData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning($"Received empty enemy data from {playerId}");
                    return;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Processing enemy data from {playerId}: {enemyData}");
                
                var enemies = new Dictionary<string, EnemyData>();
                var enemyParts = enemyData.Split(';');
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Split enemy data into {enemyParts.Length} parts");
                
                foreach (var enemyPart in enemyParts)
                {
                    if (string.IsNullOrEmpty(enemyPart))
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogWarning("Skipping empty enemy part");
                        continue;
                    }
                    
                    var parts = enemyPart.Split('|');
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Enemy part '{enemyPart}' split into {parts.Length} components (expected: 8)");
                    
                    if (parts.Length < 8)
                    {
                        Logger.LogError($"Invalid enemy data format: expected 8 parts, got {parts.Length}. Data: '{enemyPart}'");
                        continue;
                    }
                    
                    try
                    {
                        var enemy = new EnemyData
                        {
                            Id = parts[0] ?? "",
                            Name = parts[1] ?? "",
                            Position = new Vector3(
                                SafeParseFloat(parts[2], "Position.x"),
                                SafeParseFloat(parts[3], "Position.y"),
                                0
                            ),
                            IsActive = SafeParseBool(parts[4], "IsActive"),
                            Health = SafeParseInt(parts[5], "Health"),
                            IsDead = SafeParseBool(parts[6], "IsDead"),
                            DeathTime = SafeParseFloat(parts[7], "DeathTime")
                        };
                        
                        enemies[enemy.Id] = enemy;
                        
                        // Si el enemigo está muerto, aplicar la muerte en el cliente
                        if (enemy.IsDead && !_connector.IsHost)
                        {
                            ApplyEnemyDeath(enemy);
                        }
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Successfully parsed enemy: {enemy.Name} (ID: {enemy.Id}) - Dead: {enemy.IsDead}");
                    }
                    catch (Exception parseEx)
                    {
                        Logger.LogError($"Error parsing individual enemy data '{enemyPart}': {parseEx.Message}");
                        continue;
                    }
                }
                
                _remoteEnemies = enemies;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Received enemy data from {playerId}: {enemies.Count} enemies");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying enemy data from {playerId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene datos pendientes para envío
        /// </summary>
        public string GetPendingData()
        {
            try
            {
                List<string> dataToSend = new List<string>();
                
                // Agregar datos de salud del jugador
                if (!string.IsNullOrEmpty(_pendingHealthData))
                {
                    dataToSend.Add($"HEALTH:{_pendingHealthData}");
                    _pendingHealthData = null;
                }
                
                // Agregar datos de enemigos
                if (_pendingEnemyData.Count > 0)
                {
                    foreach (var enemyData in _pendingEnemyData)
                    {
                        dataToSend.Add($"ENEMY:{enemyData}");
                    }
                    _pendingEnemyData.Clear();
                }
                
                // Agregar eventos de combate
                if (_pendingCombatEvents.Count > 0)
                {
                    foreach (var combatEvent in _pendingCombatEvents)
                    {
                        dataToSend.Add($"COMBAT_EVENT:{combatEvent}");
                    }
                    _pendingCombatEvents.Clear();
                }
                
                // Agregar datos de jefes
                if (_pendingBossData.Count > 0)
                {
                    foreach (var bossData in _pendingBossData)
                    {
                        dataToSend.Add($"BOSS:{bossData}");
                    }
                    _pendingBossData.Clear();
                }
                
                // Agregar datos de objetos del mundo
                if (_pendingWorldObjectData.Count > 0)
                {
                    foreach (var worldObjectData in _pendingWorldObjectData)
                    {
                        dataToSend.Add($"WORLD_OBJECT:{worldObjectData}");
                    }
                    _pendingWorldObjectData.Clear();
                }
                
                if (dataToSend.Count > 0)
                {
                    return $"COMBAT::{string.Join("||", dataToSend)}";
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error getting pending combat data: {ex.Message}");
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
                
                string[] dataParts = data.Split(new string[] { "||" }, StringSplitOptions.None);
                
                foreach (string part in dataParts)
                {
                    string[] components = part.Split(':');
                    if (components.Length < 2) continue;
                    
                    string dataType = components[0];
                    string dataValue = components[1];
                    
                    switch (dataType)
                    {
                        case "HEALTH":
                            ApplyPlayerHealthUpdate(dataValue);
                            break;
                        case "ENEMY":
                            ApplyEnemyUpdate(dataValue);
                            break;
                        case "COMBAT_EVENT":
                            ApplyCombatEvent(dataValue);
                            break;
                        case "BOSS":
                            ApplyBossUpdate(dataValue);
                            break;
                        case "WORLD_OBJECT":
                            ApplyWorldObjectUpdate(dataValue);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying received combat data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica actualización de salud de jugador
        /// </summary>
        private void ApplyPlayerHealthUpdate(string healthData)
        {
            try
            {
                if (float.TryParse(healthData, out float health))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Received player health update: {health}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying player health update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica actualización de enemigo
        /// </summary>
        private void ApplyEnemyUpdate(string enemyData)
        {
            try
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Received enemy update: {enemyData}");
                
                if (string.IsNullOrEmpty(enemyData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("Received empty enemy data");
                    return;
                }
                
                // Parsear datos de enemigo: "enemyId|health|maxHealth|posX|posY|posZ|damaged"
                var parts = enemyData.Split('|');
                if (parts.Length >= 7)
                {
                    string enemyId = parts[0];
                    int health = SafeParseInt(parts[1], "-1");
                    int maxHealth = SafeParseInt(parts[2], "-1");
                    float posX = SafeParseFloat(parts[3], "0");
                    float posY = SafeParseFloat(parts[4], "0");
                    float posZ = SafeParseFloat(parts[5], "0");
                    bool damaged = SafeParseBool(parts[6], "false");
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Enemy update - ID: {enemyId}, Health: {health}/{maxHealth}, Damaged: {damaged}");
                    
                    // Si el enemigo fue dañado, aplicar efecto visual y mostrar barra de vida
                    if (damaged)
                    {
                        ApplyDamageVisualEffect(enemyId, maxHealth - health);
                        
                        // También mostrar barra de vida directamente si tenemos los datos
                        if (_enemyHealthBar != null && health >= 0 && maxHealth > 0)
                        {
                            // Buscar el objeto enemigo
                            GameObject enemyObject = GameObject.Find(enemyId);
                            if (enemyObject == null)
                            {
                                GameObject[] allObjects = FindObjectsOfType<GameObject>();
                                foreach (GameObject obj in allObjects)
                                {
                                    if (obj.name.Contains(enemyId) || obj.name.ToLower().Contains("enemy") || obj.name.ToLower().Contains("boss"))
                                    {
                                        var healthManager = obj.GetComponent("HealthManager");
                                        var damageHero = obj.GetComponent("DamageHero");
                                        if (healthManager != null || damageHero != null)
                                        {
                                            enemyObject = obj;
                                            break;
                                        }
                                    }
                                }
                            }
                            
                            if (enemyObject != null)
                            {
                                _enemyHealthBar.ShowHealthBar(enemyId, enemyObject, health, maxHealth);
                                
                                if (Config.PrintDebugOutput)
                                    Logger.LogInfo($"Showing health bar from enemy update for {enemyObject.name}: {health}/{maxHealth}");
                            }
                        }
                    }
                }
                else
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning($"Invalid enemy data format: {enemyData}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying enemy update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica evento de combate
        /// </summary>
        private void ApplyCombatEvent(string combatData)
        {
            try
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Received combat event: {combatData}");
                
                if (string.IsNullOrEmpty(combatData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("Received empty combat data");
                    return;
                }
                
                // Parsear datos de combate: "enemyId|damage|timestamp"
                var parts = combatData.Split('|');
                if (parts.Length >= 3)
                {
                    string enemyId = parts[0];
                    int damage = SafeParseInt(parts[1], "0");
                    float timestamp = SafeParseFloat(parts[2], "0");
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Combat event - Enemy: {enemyId}, Damage: {damage}, Time: {timestamp}");
                    
                    // Aplicar efecto visual de daño al enemigo
                    ApplyDamageVisualEffect(enemyId, damage);
                }
                else
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning($"Invalid combat data format: {combatData}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying combat event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica actualización de jefe recibida de otro jugador
        /// </summary>
        private void ApplyBossUpdate(string bossData)
        {
            try
            {
                if (string.IsNullOrEmpty(bossData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("Received empty boss data");
                    return;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Processing boss data: {bossData}");
                
                var bosses = new Dictionary<string, BossData>();
                var bossParts = bossData.Split(';');
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Split boss data into {bossParts.Length} parts");
                
                foreach (var bossPart in bossParts)
                {
                    if (string.IsNullOrEmpty(bossPart)) 
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogWarning("Skipping empty boss part");
                        continue;
                    }
                    
                    var parts = bossPart.Split('|');
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Boss part '{bossPart}' split into {parts.Length} components (expected: 11)");
                    
                    if (parts.Length < 11)
                    {
                        Logger.LogError($"Invalid boss data format: expected 11 parts, got {parts.Length}. Data: '{bossPart}'");
                        continue;
                    }
                    
                    try
                    {
                        var boss = new BossData
                        {
                            Id = parts[0] ?? "",
                            Name = parts[1] ?? "",
                            Position = new Vector3(
                                SafeParseFloat(parts[2], "Position.x"), 
                                SafeParseFloat(parts[3], "Position.y"), 
                                0
                            ),
                            IsActive = SafeParseBool(parts[4], "IsActive"),
                            Health = SafeParseInt(parts[5], "Health"),
                            MaxHealth = SafeParseInt(parts[6], "MaxHealth"),
                            CurrentPhase = SafeParseInt(parts[7], "CurrentPhase"),
                            IsInCombat = SafeParseBool(parts[8], "IsInCombat"),
                            LastAttackTime = SafeParseFloat(parts[9], "LastAttackTime"),
                            CurrentAttack = parts[10] ?? ""
                        };
                        
                        bosses[boss.Id] = boss;
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Successfully parsed boss: {boss.Name} (ID: {boss.Id})");
                    }
                    catch (Exception parseEx)
                    {
                        Logger.LogError($"Error parsing individual boss data '{bossPart}': {parseEx.Message}");
                        continue;
                    }
                }
                
                // TODO: Reactivar sincronización de audio con jefes
                // Sincronizar audio si el jefe está en combate - TEMPORALMENTE DESACTIVADO
                /*
                foreach (var boss in bosses.Values)
                {
                    if (_audioSync != null && boss.IsInCombat)
                    {
                        // Buscar el GameObject del jefe para sincronizar audio
                        var bossObject = FindObjectsOfType<GameObject>()
                            .FirstOrDefault(go => go.name.Contains(boss.Name.Split('_')[0]) && go.activeInHierarchy);
                        
                        if (bossObject != null)
                        {
                            NotifyBossActivity(bossObject, boss);
                        }
                    }
                }
                */
                
                _remoteBosses = bosses;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Received boss data: {bosses.Count} bosses");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying boss update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica actualización de objetos del mundo
        /// </summary>
        private void ApplyWorldObjectUpdate(string worldObjectData)
        {
            try
            {
                if (string.IsNullOrEmpty(worldObjectData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("Received empty world object data");
                    return;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Processing world object data: {worldObjectData}");
                
                var worldObjects = new Dictionary<string, WorldObjectData>();
                var objectParts = worldObjectData.Split(';');
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Split world object data into {objectParts.Length} parts");
                
                foreach (var objectPart in objectParts)
                {
                    if (string.IsNullOrEmpty(objectPart))
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogWarning("Skipping empty world object part");
                        continue;
                    }
                    
                    var parts = objectPart.Split('|');
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"World object part '{objectPart}' split into {parts.Length} components (expected: 8)");
                    
                    if (parts.Length < 8)
                    {
                        Logger.LogError($"Invalid world object data format: expected 8 parts, got {parts.Length}. Data: '{objectPart}'");
                        continue;
                    }
                    
                    try
                    {
                        var worldObject = new WorldObjectData
                        {
                            Id = parts[0] ?? "",
                            Name = parts[1] ?? "",
                            Position = new Vector3(
                                SafeParseFloat(parts[2], "Position.x"),
                                SafeParseFloat(parts[3], "Position.y"),
                                0
                            ),
                            IsActive = SafeParseBool(parts[4], "IsActive"),
                            IsDestroyed = SafeParseBool(parts[5], "IsDestroyed"),
                            IsInteracted = SafeParseBool(parts[6], "IsInteracted"),
                            ObjectType = parts[7] ?? "unknown"
                        };
                        
                        worldObjects[worldObject.Id] = worldObject;
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Successfully parsed world object: {worldObject.Name} (ID: {worldObject.Id}) - Type: {worldObject.ObjectType}");
                        
                        // Aplicar estado del objeto si es necesario
                        ApplyWorldObjectState(worldObject);
                    }
                    catch (Exception parseEx)
                    {
                        Logger.LogError($"Error parsing individual world object data '{objectPart}': {parseEx.Message}");
                        continue;
                    }
                }
                
                _remoteWorldObjects = worldObjects;
                
                // Aplicar eventos del mundo a GameProgressSync
                ApplyWorldEventsToProgressSync(worldObjects);
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Received world object data: {worldObjects.Count} objects");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying world object update: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica eventos del mundo al sistema de progreso
        /// </summary>
        private void ApplyWorldEventsToProgressSync(Dictionary<string, WorldObjectData> worldObjects)
        {
            if (_progressSync == null) return;
            
            try
            {
                foreach (var kvp in worldObjects)
                {
                    var worldObject = kvp.Value;
                    
                    // Generar eventos basados en el estado del objeto
                    if (worldObject.IsDestroyed)
                    {
                        string eventKey = $"destroyed_{worldObject.ObjectType}_{worldObject.Name}";
                        _progressSync.RegisterWorldEvent("destroyed", $"{worldObject.ObjectType}_{worldObject.Name}", true);
                    }
                    
                    if (!worldObject.IsActive && worldObject.ObjectType == "collectible")
                    {
                        string eventKey = $"collected_{worldObject.Name}";
                        _progressSync.RegisterWorldEvent("collected", worldObject.Name, true);
                    }
                    
                    if (worldObject.IsInteracted)
                    {
                        string eventKey = $"interacted_{worldObject.ObjectType}_{worldObject.Name}";
                        _progressSync.RegisterWorldEvent("interacted", $"{worldObject.ObjectType}_{worldObject.Name}", true);
                    }
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Applied {worldObjects.Count} world events to progress sync");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error applying world events to progress sync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica el estado de un objeto del mundo
        /// </summary>
        private void ApplyWorldObjectState(WorldObjectData worldObjectData)
        {
            try
            {
                // Buscar el objeto en la escena
                var targetObjects = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains(worldObjectData.Name.Split('_')[0]))
                    .ToList();
                
                foreach (var obj in targetObjects)
                {
                    if (obj == null) continue;
                    
                    string objectId = $"{obj.name}_{obj.GetInstanceID()}";
                    if (objectId == worldObjectData.Id)
                    {
                        // Aplicar estado de destrucción/activación
                        if (worldObjectData.IsDestroyed && obj.activeInHierarchy)
                        {
                            obj.SetActive(false);
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Applied destruction to world object: {worldObjectData.Name} (ID: {worldObjectData.Id})");
                        }
                        else if (!worldObjectData.IsDestroyed && !obj.activeInHierarchy && worldObjectData.IsActive)
                        {
                            obj.SetActive(true);
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Restored world object: {worldObjectData.Name} (ID: {worldObjectData.Id})");
                        }
                        
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying world object state for {worldObjectData.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualiza la proximidad de jugadores a jefes para activación automática
        /// </summary>
        private void UpdatePlayerProximityToBosses()
        {
            try
            {
                _playersInBossArea.Clear();
                
                // Buscar todos los jugadores activos
                var players = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains("Hero") && go.activeInHierarchy)
                    .ToList();
                
                // Buscar todos los jefes (activos e inactivos)
                var allBosses = FindObjectsOfType<GameObject>()
                    .Where(go => (go.name.Contains("Boss") || 
                                 go.name.Contains("Gruz Mother") ||
                                 go.name.Contains("False Knight") ||
                                 go.name.Contains("Hornet") ||
                                 go.name.Contains("Mantis Lord")))
                    .ToList();
                
                foreach (var player in players)
                {
                    foreach (var boss in allBosses)
                    {
                        float distance = Vector3.Distance(player.transform.position, boss.transform.position);
                        if (distance <= BOSS_AREA_RADIUS)
                        {
                            string playerId = $"{player.name}_{player.GetInstanceID()}";
                            _playersInBossArea[playerId] = player.transform.position;
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Player {player.name} detected near boss {boss.name} (distance: {distance:F1})");
                        }
                    }
                }
                
                if (Config.PrintDebugOutput && _playersInBossArea.Count > 0)
                    Logger.LogInfo($"Players in boss area: {_playersInBossArea.Count}");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error updating player proximity to bosses: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verifica y activa jefes inactivos cuando todos los jugadores están presentes
        /// </summary>
        private void CheckAndActivateInactiveBosses()
        {
            try
            {
                if (_playersInBossArea.Count == 0) return;
                
                // Verificar si todos los jugadores conectados están en el área
                var totalPlayers = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains("Hero") && go.activeInHierarchy)
                    .Count();
                
                if (_playersInBossArea.Count < totalPlayers)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Not all players in boss area ({_playersInBossArea.Count}/{totalPlayers})");
                    return;
                }
                
                // Detectar jefes inactivos
                var inactiveBosses = DetectInactiveBosses();
                
                if (inactiveBosses.Count > 0)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"All players present, activating {inactiveBosses.Count} inactive bosses");
                    
                    foreach (var boss in inactiveBosses)
                    {
                        ActivateBoss(boss);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error checking and activating inactive bosses: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detecta jefes inactivos en la escena
        /// </summary>
        private List<GameObject> DetectInactiveBosses()
        {
            var inactiveBosses = new List<GameObject>();
            
            try
            {
                var allBosses = FindObjectsOfType<GameObject>()
                    .Where(go => (go.name.Contains("Boss") || 
                                 go.name.Contains("Gruz Mother") ||
                                 go.name.Contains("False Knight") ||
                                 go.name.Contains("Hornet") ||
                                 go.name.Contains("Mantis Lord")))
                    .ToList();
                
                foreach (var boss in allBosses)
                {
                    if (IsBossInactive(boss))
                    {
                        inactiveBosses.Add(boss);
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Detected inactive boss: {boss.name}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error detecting inactive bosses: {ex.Message}");
            }
            
            return inactiveBosses;
        }
        
        /// <summary>
        /// Verifica si un jefe está inactivo
        /// </summary>
        private bool IsBossInactive(GameObject boss)
        {
            try
            {
                // Verificar si el jefe está en combate
                if (IsBossInCombat(boss))
                    return false;
                
                // Verificar salud del jefe
                int health = GetEnemyHealth(boss);
                int maxHealth = GetEnemyMaxHealth(boss);
                
                if (health <= 0 || health < maxHealth)
                    return false; // Ya está dañado o muerto
                
                // Verificar componentes específicos que indican inactividad
                var healthManager = boss.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    var activeField = healthManager.GetType().GetField("isInvincible");
                    if (activeField != null && (bool)activeField.GetValue(healthManager))
                        return true; // Invencible = inactivo
                }
                
                return false;
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error checking if boss is inactive: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Activa un jefe específico usando reflexión
        /// </summary>
        private void ActivateBoss(GameObject boss)
        {
            try
            {
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Attempting to activate boss: {boss.name}");
                
                // Intentar activar el jefe usando diferentes métodos
                var healthManager = boss.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    // Desactivar invencibilidad
                    var invincibleField = healthManager.GetType().GetField("isInvincible");
                    if (invincibleField != null)
                    {
                        invincibleField.SetValue(healthManager, false);
                    }
                    
                    // Activar el jefe
                    var activateMethod = healthManager.GetType().GetMethod("Activate");
                    if (activateMethod != null)
                    {
                        activateMethod.Invoke(healthManager, null);
                    }
                }
                
                // Activar el GameObject si está desactivado
                if (!boss.activeInHierarchy)
                {
                    boss.SetActive(true);
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Boss {boss.name} activated successfully");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error activating boss {boss.name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verifica si un sonido es de ataque para el tipo de personaje específico
        /// </summary>
        private bool IsAttackSound(string clipName, CharacterType characterType)
        {
            // Sonidos comunes de ataque
            bool isCommonAttack = clipName.Contains("attack") || clipName.Contains("slash") || 
                                 clipName.Contains("hit") || clipName.Contains("swing");
            
            switch (characterType)
            {
                case CharacterType.Hornet:
                    return isCommonAttack || clipName.Contains("needle") || clipName.Contains("thread") ||
                           clipName.Contains("silk") || clipName.Contains("dash_strike");
                           
                case CharacterType.Knight:
                    return isCommonAttack || clipName.Contains("nail") || clipName.Contains("sword") ||
                           clipName.Contains("spell") || clipName.Contains("focus") ||
                           clipName.Contains("soul") || clipName.Contains("void");
                           
                default:
                    return isCommonAttack;
            }
        }
        
        /// <summary>
        /// Verifica si un sonido es de daño para el tipo de personaje específico
        /// </summary>
        private bool IsDamageSound(string clipName, CharacterType characterType)
        {
            // Sonidos comunes de daño
            bool isCommonDamage = clipName.Contains("damage") || clipName.Contains("hurt") || 
                                 clipName.Contains("pain") || clipName.Contains("hit_player");
            
            switch (characterType)
            {
                case CharacterType.Hornet:
                    return isCommonDamage || clipName.Contains("hornet_damage") || clipName.Contains("silk_break");
                    
                case CharacterType.Knight:
                    return isCommonDamage || clipName.Contains("hero_damage") || clipName.Contains("knight_hurt") ||
                           clipName.Contains("vessel_damage");
                           
                default:
                    return isCommonDamage;
            }
        }
        
        /// <summary>
        /// Verifica si un sonido es específico del personaje
        /// </summary>
        private bool IsCharacterSpecificSound(string clipName, CharacterType characterType)
        {
            switch (characterType)
            {
                case CharacterType.Hornet:
                    return clipName.Contains("hornet") || clipName.Contains("needle") ||
                           clipName.Contains("thread") || clipName.Contains("silk") ||
                           clipName.Contains("dash_strike") || clipName.Contains("spike_throw");
                           
                case CharacterType.Knight:
                    return clipName.Contains("hero_") || clipName.Contains("knight") ||
                           clipName.Contains("nail_art") || clipName.Contains("soul_") ||
                           clipName.Contains("void_") || clipName.Contains("focus") ||
                           clipName.Contains("dash") || clipName.Contains("jump") ||
                           clipName.Contains("wall_") || clipName.Contains("wings") ||
                           clipName.Contains("spell") || clipName.Contains("scream") ||
                           clipName.Contains("quake") || clipName.Contains("fireball");
                           
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Actualiza la detección de actividad para optimización adaptativa
        /// </summary>
        private void UpdateActivityDetection()
        {
            // Detectar actividad basada en cambios en enemigos, jefes y combate
            bool hasActivity = false;
            
            // Verificar actividad de enemigos
            if (_trackedEnemies.Count > 0)
            {
                foreach (var enemy in _trackedEnemies.Values)
                {
                    if (enemy.IsActive && !enemy.IsDead)
                    {
                        hasActivity = true;
                        break;
                    }
                }
            }
            
            // Verificar actividad de jefes
            if (!hasActivity && _activeBosses.Count > 0)
            {
                foreach (var boss in _activeBosses.Values)
                {
                    if (boss.IsActive && boss.IsInCombat)
                    {
                        hasActivity = true;
                        break;
                    }
                }
            }
            
            // Verificar eventos de combate pendientes
            if (!hasActivity && _pendingDamageEvents.Count > 0)
            {
                hasActivity = true;
            }
            
            if (hasActivity)
            {
                _lastActivityTime = Time.time;
                _currentEnemySyncInterval = ENEMY_SYNC_INTERVAL_ACTIVE;
            }
            else if (Time.time - _lastActivityTime > ACTIVITY_TIMEOUT)
            {
                _currentEnemySyncInterval = ENEMY_SYNC_INTERVAL_IDLE;
            }
        }
        
        /// <summary>
        /// Verifica si la posición ha cambiado lo suficiente para sincronizar
        /// </summary>
        private bool HasPositionChanged(Vector3 oldPos, Vector3 newPos)
        {
            return Vector3.Distance(oldPos, newPos) >= POSITION_CHANGE_THRESHOLD;
        }
        
        /// <summary>
        /// Verifica si la salud ha cambiado lo suficiente para sincronizar
        /// </summary>
        private bool HasHealthChanged(int oldHealth, int newHealth)
        {
            return Mathf.Abs(oldHealth - newHealth) >= HEALTH_CHANGE_THRESHOLD;
        }
        
        /// <summary>
        /// Verifica si la rotación ha cambiado lo suficiente para sincronizar
        /// </summary>
        private bool HasRotationChanged(float oldRotation, float newRotation)
        {
            return Mathf.Abs(Mathf.DeltaAngle(oldRotation, newRotation)) >= ROTATION_CHANGE_THRESHOLD;
        }
        
        /// <summary>
        /// Resetea el sistema de sincronización
        /// </summary>
        public void Reset()
        {
            _trackedEnemies.Clear();
            _remoteEnemies.Clear();
            _pendingDamageEvents.Clear();
            _playerCharacters.Clear();
            _hostCharacterId = null;
            _guestCharacterId = null;
            _pendingHealthData = null;
            _pendingEnemyData.Clear();
            _pendingCombatEvents.Clear();
            _pendingBossData.Clear();
            _activeBosses.Clear();
            _remoteBosses.Clear();
            _playersInBossArea.Clear();
            
            if (Config.PrintDebugOutput)
                Logger.LogInfo("CombatSync reset");
        }
        
        /// <summary>
        /// Aplica efecto visual de daño a un enemigo
        /// </summary>
        /// <param name="enemyId">ID del enemigo</param>
        /// <param name="damage">Cantidad de daño recibido</param>
        private void ApplyDamageVisualEffect(string enemyId, int damage)
        {
            try
            {
                if (string.IsNullOrEmpty(enemyId))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("Enemy ID is null or empty for damage effect");
                    return;
                }
                
                // Buscar el enemigo por ID o nombre
                GameObject enemyObject = GameObject.Find(enemyId);
                if (enemyObject == null)
                {
                    // Buscar en todos los objetos activos
                    GameObject[] allObjects = FindObjectsOfType<GameObject>();
                    foreach (GameObject obj in allObjects)
                    {
                        if (obj.name.Contains(enemyId) || obj.name.ToLower().Contains("enemy") || obj.name.ToLower().Contains("boss"))
                        {
                            // Verificar si tiene componentes de enemigo
                            var healthManager = obj.GetComponent("HealthManager");
                            var damageHero = obj.GetComponent("DamageHero");
                            if (healthManager != null || damageHero != null)
                            {
                                enemyObject = obj;
                                if (Config.PrintDebugOutput)
                                    Logger.LogInfo($"Found enemy object: {obj.name} for ID: {enemyId}");
                                break;
                            }
                        }
                    }
                }
                
                if (enemyObject == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning($"Enemy object not found for ID: {enemyId}");
                    return;
                }
                
                // Aplicar efecto de flash blanco
                StartCoroutine(ApplyDamageFlashEffect(enemyObject, damage));
                
                // Mostrar barra de vida del enemigo
                if (_enemyHealthBar != null)
                {
                    int currentHealth = GetEnemyHealth(enemyObject);
                    int maxHealth = GetEnemyMaxHealth(enemyObject);
                    
                    if (currentHealth >= 0 && maxHealth > 0)
                    {
                        _enemyHealthBar.ShowHealthBar(enemyId, enemyObject, currentHealth, maxHealth);
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Showing health bar for {enemyObject.name}: {currentHealth}/{maxHealth}");
                    }
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Applied damage visual effect to {enemyObject.name} (damage: {damage})");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying damage visual effect: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Corrutina que aplica el efecto de flash blanco cuando un enemigo recibe daño
        /// </summary>
        /// <param name="enemyObject">El objeto enemigo</param>
        /// <param name="damage">Cantidad de daño</param>
        private IEnumerator ApplyDamageFlashEffect(GameObject enemyObject, int damage)
        {
            if (enemyObject == null) yield break;
            
            // Buscar todos los componentes de sprite en el enemigo
            var spriteRenderers = enemyObject.GetComponentsInChildren<SpriteRenderer>();
            var tk2dSprites = enemyObject.GetComponentsInChildren<tk2dSprite>();
            
            // Almacenar colores originales
            Color[] originalColors = new Color[spriteRenderers.Length];
            Material[] originalMaterials = new Material[tk2dSprites.Length];
            
            // Aplicar efecto blanco
            bool effectApplied = false;
            try
            {
                // Cambiar a blanco los SpriteRenderer
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        originalColors[i] = spriteRenderers[i].color;
                        spriteRenderers[i].color = Color.white;
                    }
                }
                
                // Cambiar a blanco los tk2dSprite
                for (int i = 0; i < tk2dSprites.Length; i++)
                {
                    if (tk2dSprites[i] != null)
                    {
                        var renderer = tk2dSprites[i].GetComponent<Renderer>();
                        if (renderer != null && renderer.material != null)
                        {
                            originalMaterials[i] = renderer.material;
                            
                            // Crear material temporal blanco
                            Material whiteMaterial = new Material(renderer.material.shader);
                            whiteMaterial.mainTexture = renderer.material.mainTexture;
                            whiteMaterial.color = Color.white;
                            renderer.material = whiteMaterial;
                        }
                    }
                }
                
                effectApplied = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying damage flash effect: {ex.Message}");
            }
            
            if (!effectApplied) yield break;
            
            // Duración del flash basada en el daño (mínimo 0.1s, máximo 0.5s)
            float flashDuration = Mathf.Clamp(damage * 0.02f, 0.1f, 0.5f);
            yield return new WaitForSeconds(flashDuration);
            
            // Restaurar colores originales
            try
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        spriteRenderers[i].color = originalColors[i];
                    }
                }
                
                // Restaurar materiales originales
                for (int i = 0; i < tk2dSprites.Length; i++)
                {
                    if (tk2dSprites[i] != null && originalMaterials[i] != null)
                    {
                        var renderer = tk2dSprites[i].GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material = originalMaterials[i];
                        }
                    }
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Damage flash effect completed for {enemyObject.name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error restoring damage flash effect: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica el cambio visual del personaje modificando el sprite
        /// </summary>
        /// <param name="character">El personaje al que cambiar</param>
        private void ApplyCharacterVisualChange(PlayerCharacter character)
        {
            try
            {
                // Buscar el objeto del jugador principal (Hornet) - probar múltiples nombres
                GameObject hornetObject = GameObject.Find("Hero_Hornet(Clone)");
                if (hornetObject == null)
                {
                    hornetObject = GameObject.Find("Hero_Hornet");
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo("Trying alternative name: Hero_Hornet");
                }
                
                if (hornetObject == null)
                {
                    // Buscar cualquier objeto que contenga "Hero" o "Hornet"
                    GameObject[] allObjects = FindObjectsOfType<GameObject>();
                    foreach (GameObject obj in allObjects)
                    {
                        if (obj.name.Contains("Hero") || obj.name.Contains("Hornet"))
                        {
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Found potential player object: {obj.name}");
                            if (obj.GetComponent<tk2dSprite>() != null)
                            {
                                hornetObject = obj;
                                if (Config.PrintDebugOutput)
                                    Logger.LogInfo($"Using player object: {obj.name}");
                                break;
                            }
                        }
                    }
                }
                
                if (hornetObject == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("No suitable player object found for character switch");
                    return;
                }
                
                // Obtener el componente sprite
                var hornetSprite = hornetObject.GetComponent<tk2dSprite>();
                if (hornetSprite == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning("tk2dSprite component not found on Hero_Hornet(Clone)");
                    return;
                }
                
                // Si es el Knight y tenemos sprites personalizados cargados, usarlos
                if (character.Type == CharacterType.Knight)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Attempting to switch to Knight character: {character.Name}");
                    
                    var customLoader = CustomSpriteLoader.Instance;
                    if (customLoader != null)
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo("CustomSpriteLoader instance found");
                        
                        if (customLoader.AreKnightSpritesLoaded())
                        {
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo("Knight sprites are loaded, applying custom sprite");
                            
                            bool success = customLoader.ApplyKnightSpriteToTk2d(hornetSprite, "knight_idle");
                            if (success)
                            {
                                if (Config.PrintDebugOutput)
                                    Logger.LogInfo($"Successfully applied custom Knight sprite: {character.Name}");
                                return;
                            }
                            else
                            {
                                if (Config.PrintDebugOutput)
                                    Logger.LogWarning("Failed to apply custom Knight sprite, falling back to default");
                            }
                        }
                        else
                        {
                            if (Config.PrintDebugOutput)
                                Logger.LogWarning("Knight sprites not loaded yet, using default sprite system");
                        }
                    }
                    else
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogWarning("CustomSpriteLoader instance is null");
                    }
                }
                
                // Usar el sistema de sprites por defecto
                int newSpriteId = GetSpriteIdForCharacter(character.Type);
                if (newSpriteId >= 0)
                {
                    hornetSprite.spriteId = newSpriteId;
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Applied visual change: switched to {character.Name} (spriteId: {newSpriteId})");
                }
                else
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogWarning($"No sprite ID found for character type: {character.Type}");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error applying character visual change: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene el ID del sprite para un tipo de personaje específico
        /// </summary>
        /// <param name="characterType">El tipo de personaje</param>
        /// <returns>El ID del sprite, o -1 si no se encuentra</returns>
        private int GetSpriteIdForCharacter(CharacterType characterType)
        {
            // Estos IDs pueden necesitar ajuste según los sprites disponibles en el juego
            switch (characterType)
            {
                case CharacterType.Hornet:
                    return 0; // ID del sprite de Hornet (por defecto)
                case CharacterType.Knight:
                    return 1; // ID del sprite del Knight - esto puede necesitar ajuste
                default:
                    return -1;
            }
        }
        
        #region Arquitectura Host-Cliente
        
        /// <summary>
        /// Envía el estado completo del mundo a todos los clientes (solo HOST)
        /// </summary>
        private void BroadcastWorldState()
        {
            if (!_connector.IsHost) return;
            
            try
            {
                var worldState = new WorldState();
                
                // Recopilar datos de enemigos
                foreach (var enemy in _trackedEnemies.Values)
                {
                    worldState.Enemies.Add(new NetworkEnemyData(enemy));
                }
                
                // Recopilar datos de jugadores
                foreach (var player in _playerCharacters.Values)
                {
                    worldState.Players.Add(new NetworkPlayerData(player));
                }
                
                // Recopilar eventos de combate
                foreach (var combatEvent in _pendingDamageEvents.Values)
                {
                    worldState.CombatEvents.Add(new NetworkCombatEvent(combatEvent));
                }
                
                // Recopilar datos de jefes
                foreach (var boss in _activeBosses.Values)
                {
                    worldState.Bosses.Add(new NetworkBossData(boss));
                }
                
                // Configurar metadatos del WorldState
                worldState.HostId = _connector.GetName();
                worldState.SequenceNumber = ++_worldStateSequenceNumber;
                worldState.Timestamp = Time.time;
                
                // Crear estado compacto para optimizar ancho de banda
                var compactState = worldState.CreateCompactState();
                
                // Serializar y enviar
                string stateData = WorldStateSerializer.SerializeWorldState(compactState);
                if (!string.IsNullOrEmpty(stateData))
                {
                    _gameSync.SendUpdate($"WORLD_STATE::{stateData}");
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Broadcasted compact world state: {compactState.Enemies.Count} enemies, {compactState.Players.Count} players, {compactState.CombatEvents.Count} combat events");
                }
                
                // Limpiar eventos de combate después de enviar
                _pendingDamageEvents.Clear();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error broadcasting world state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica el estado del mundo recibido del HOST (solo CLIENTE)
        /// </summary>
        public void ApplyWorldState(string stateData)
        {
            if (!_connector.IsClient) return;
            
            try
            {
                var worldState = WorldStateSerializer.DeserializeWorldState(stateData);
                if (worldState == null) 
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError("CLIENT: Failed to deserialize world state");
                    return;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"CLIENT: Applying world state from HOST {worldState.HostId}: {worldState.Enemies.Count} enemies, {worldState.Players.Count} players, {worldState.CombatEvents.Count} combat events");
                
                // Aplicar estado de enemigos
                ApplyEnemyStates(worldState.Enemies);
                
                // Aplicar estado de jugadores
                ApplyPlayerStates(worldState.Players);
                
                // Aplicar eventos de combate
                ApplyCombatEvents(worldState.CombatEvents);
                
                // Aplicar estado de jefes
                ApplyBossStates(worldState.Bosses);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying world state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica el estado de enemigos recibido del HOST
        /// </summary>
        private void ApplyEnemyStates(List<NetworkEnemyData> hostEnemies)
        {
            foreach (var hostEnemy in hostEnemies)
            {
                try
                {
                    // Buscar el enemigo local
                    GameObject localEnemy = GameObject.Find(hostEnemy.Id);
                    if (localEnemy == null)
                    {
                        // Buscar por nombre si no se encuentra por ID
                        var allObjects = FindObjectsOfType<GameObject>();
                        foreach (GameObject obj in allObjects)
                        {
                            if (obj.name.Contains(hostEnemy.Name) || obj.name.Contains(hostEnemy.Id))
                            {
                                var healthManager = obj.GetComponent("HealthManager");
                                if (healthManager != null)
                                {
                                    localEnemy = obj;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (localEnemy != null)
                    {
                        // Aplicar posición
                        localEnemy.transform.position = hostEnemy.Position;
                        
                        // Aplicar estado activo/inactivo
                        localEnemy.SetActive(hostEnemy.IsActive);
                        
                        // Aplicar vida
                        SetEnemyHealth(localEnemy, hostEnemy.Health);
                        
                        // Mostrar barra de vida si fue dañado
                        if (hostEnemy.WasDamaged && _enemyHealthBar != null)
                        {
                            _enemyHealthBar.ShowHealthBar(hostEnemy.Id, localEnemy, hostEnemy.Health, hostEnemy.MaxHealth);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"Error applying enemy state for {hostEnemy.Id}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Aplica el estado de jugadores recibido del HOST
        /// </summary>
        private void ApplyPlayerStates(List<NetworkPlayerData> hostPlayers)
        {
            foreach (var hostPlayer in hostPlayers)
            {
                try
                {
                    // Crear jugador remoto si no existe (solo en CLIENTES)
                    if (!_playerCharacters.ContainsKey(hostPlayer.Id) && _connector.IsClient)
                    {
                        CreateRemotePlayerForClient(hostPlayer);
                    }
                    
                    if (_playerCharacters.ContainsKey(hostPlayer.Id))
                    {
                        var localPlayer = _playerCharacters[hostPlayer.Id];
                        localPlayer.LastPosition = hostPlayer.Position;
                        localPlayer.LastHealth = hostPlayer.Health;
                        localPlayer.LastMaxHealth = hostPlayer.MaxHealth;
                        localPlayer.IsActive = hostPlayer.IsActive;
                        
                        // Actualizar posición simple (sin GameObjects remotos)
                        if (localPlayer.Controller != null)
                        {
                            localPlayer.Controller.transform.position = hostPlayer.Position;
                        }
                        
                        if (Config.PrintDebugOutput && Time.frameCount % 300 == 0) // Log cada 5 segundos aprox
                            Logger.LogInfo($"Updated player {hostPlayer.Id} position: {hostPlayer.Position}");
                    }
                }
                catch (Exception ex)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"Error applying player state for {hostPlayer.Id}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Aplica eventos de combate recibidos del HOST
        /// </summary>
        private void ApplyCombatEvents(List<NetworkCombatEvent> combatEvents)
        {
            foreach (var combatEvent in combatEvents)
            {
                try
                {
                    // Aplicar efecto visual de daño
                    ApplyDamageVisualEffect(combatEvent.TargetId, combatEvent.Damage);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Applied combat event: {combatEvent.SourceId} → {combatEvent.TargetId} ({combatEvent.Damage} damage)");
                }
                catch (Exception ex)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"Error applying combat event: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Aplica el estado de jefes recibido del HOST
        /// </summary>
        private void ApplyBossStates(List<NetworkBossData> hostBosses)
        {
            foreach (var hostBoss in hostBosses)
            {
                try
                {
                    // Buscar el jefe local
                    GameObject localBoss = GameObject.Find(hostBoss.Id);
                    if (localBoss != null)
                    {
                        // Aplicar posición
                        localBoss.transform.position = hostBoss.Position;
                        
                        // Aplicar estado activo/inactivo
                        localBoss.SetActive(hostBoss.IsActive);
                        
                        // Aplicar vida
                        SetEnemyHealth(localBoss, hostBoss.Health);
                    }
                }
                catch (Exception ex)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"Error applying boss state for {hostBoss.Id}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Envía input del jugador al HOST (solo CLIENTE)
        /// </summary>
        private void SendPlayerInput()
        {
            if (!_connector.IsClient) return;
            
            try
            {
                // Obtener el jugador local activo
                var localPlayer = _playerCharacters.Values.FirstOrDefault(p => p.IsActive);
                if (localPlayer == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError("CLIENT: No active local player found for input");
                    return;
                }
                
                if (localPlayer.Controller == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"CLIENT: Player {localPlayer.Id} has no controller");
                    return;
                }
                
                var actions = GetCurrentPlayerActions();
                var input = new PlayerInput
                {
                    PlayerId = localPlayer.Id,
                    Position = localPlayer.Controller.transform.position,
                    Velocity = Vector3.zero, // TODO: Obtener velocidad real
                    Actions = actions ?? new List<string>(),
                    Timestamp = Time.time
                };
                
                // Validar que el input tenga datos válidos
                if (string.IsNullOrEmpty(input.PlayerId))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError("CLIENT: Player input missing PlayerId");
                    return;
                }
                
                string inputData = WorldStateSerializer.SerializePlayerInput(input);
                if (string.IsNullOrEmpty(inputData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError("CLIENT: Failed to serialize player input");
                    return;
                }
                
                _gameSync.SendUpdate($"PLAYER_INPUT::{inputData}");
                
                if (Config.PrintDebugOutput && input.Actions.Count > 0)
                    Logger.LogInfo($"CLIENT: Sent player input: {string.Join(", ", input.Actions)} from {input.PlayerId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error sending player input: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene las acciones actuales del jugador
        /// </summary>
        private List<string> GetCurrentPlayerActions()
        {
            var actions = new List<string>();
            
            // TODO: Detectar acciones del jugador (ataque, movimiento, etc.)
            // Por ahora, detectar input básico
            if (Input.GetKey(KeyCode.Z) || Input.GetMouseButton(0))
            {
                actions.Add("attack");
            }
            
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            {
                actions.Add("move_left");
            }
            
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            {
                actions.Add("move_right");
            }
            
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
            {
                actions.Add("move_up");
            }
            
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
            {
                actions.Add("move_down");
            }
            
            return actions;
        }
        
        /// <summary>
        /// Establece la vida de un enemigo usando reflexión
        /// </summary>
        private void SetEnemyHealth(GameObject enemy, int health)
        {
            try
            {
                var healthManager = enemy.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    var healthField = healthManager.GetType().GetField("hp");
                    if (healthField != null)
                    {
                        healthField.SetValue(healthManager, health);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error setting enemy health: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Aplica la muerte de un enemigo en el cliente
        /// </summary>
        private void ApplyEnemyDeath(EnemyData enemyData)
        {
            try
            {
                // Buscar el enemigo en la escena por nombre e ID
                var potentialEnemies = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains(enemyData.Name.Split('_')[0]))
                    .ToList();
                
                foreach (var enemy in potentialEnemies)
                {
                    if (enemy == null) continue;
                    
                    string enemyId = $"{enemy.name}_{enemy.GetInstanceID()}";
                    if (enemyId == enemyData.Id)
                    {
                        // Establecer salud a 0
                        SetEnemyHealth(enemy, 0);
                        
                        // Desactivar el GameObject
                        enemy.SetActive(false);
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Applied death to enemy: {enemyData.Name} (ID: {enemyData.Id})");
                        
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error applying enemy death: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa input de jugador recibido del CLIENTE (solo HOST)
        /// </summary>
        public void ProcessPlayerInput(string inputData)
        {
            if (!_connector.IsHost) return;
            
            try
            {
                if (string.IsNullOrEmpty(inputData))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError("Received null or empty player input data");
                    return;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"HOST: Received player input data: {inputData.Substring(0, Math.Min(100, inputData.Length))}...");
                
                var input = WorldStateSerializer.DeserializePlayerInput(inputData);
                if (input == null)
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"Failed to deserialize player input: {inputData}");
                    return;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"HOST: Processing input from {input.PlayerId}: {string.Join(", ", input.Actions)} at {input.Position}");
                
                // Crear jugador remoto si no existe
                if (!_playerCharacters.ContainsKey(input.PlayerId))
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"HOST: Creating remote player for {input.PlayerId}");
                    CreateRemotePlayer(input.PlayerId);
                }
                
                // Procesar las acciones del jugador
                if (_playerCharacters.ContainsKey(input.PlayerId))
                {
                    var player = _playerCharacters[input.PlayerId];
                    player.LastPosition = input.Position;
                    
                    // Procesar acciones de ataque
                    if (input.Actions != null && input.Actions.Contains("attack"))
                    {
                        ProcessPlayerAttack(player, input.Position);
                    }
                }
                else
                {
                    if (Config.PrintDebugOutput)
                        Logger.LogError($"HOST: Failed to find or create player {input.PlayerId}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing player input: {ex.Message}\nInput data: {inputData}");
            }
        }
        
        /// <summary>
        /// Procesa un ataque del jugador (solo HOST)
        /// </summary>
        private void ProcessPlayerAttack(PlayerCharacter player, Vector3 position)
        {
            try
            {
                // TODO: Implementar lógica de ataque
                // Por ahora, solo registrar el evento
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Player {player.Id} attacked at position {position}");
                
                // Buscar enemigos cercanos y aplicar daño
                var nearbyEnemies = FindObjectsOfType<GameObject>()
                    .Where(go => go.name.Contains("Enemy") || go.name.Contains("Boss"))
                    .Where(go => Vector3.Distance(go.transform.position, position) < 2f)
                    .ToList();
                
                foreach (var enemy in nearbyEnemies)
                {
                    // Aplicar daño al enemigo
                    int damage = 25; // Daño base
                    int currentHealth = GetEnemyHealth(enemy);
                    int newHealth = Mathf.Max(0, currentHealth - damage);
                    
                    SetEnemyHealth(enemy, newHealth);
                    
                    // Registrar evento de combate
                    string enemyId = $"{enemy.name}_{enemy.GetInstanceID()}";
                    _pendingDamageEvents[enemyId] = new DamageEvent
                    {
                        SourceId = player.Id,
                        TargetId = enemyId,
                        Damage = damage,
                        Timestamp = Time.time
                    };
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Player {player.Id} dealt {damage} damage to {enemyId} (health: {newHealth})");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error processing player attack: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crea un jugador remoto en el HOST cuando recibe input de un CLIENTE
        /// </summary>
        private void CreateRemotePlayer(string playerId)
        {
            try
            {
                // Buscar el controlador de Hornet para usar como base
                GameObject hornetController = GameObject.Find("Hero_Hornet");
                if (!hornetController)
                    hornetController = GameObject.Find("Hero_Hornet(Clone)");
                
                if (hornetController == null)
                {
                    Logger.LogError($"Cannot create remote player {playerId}: No Hornet controller found");
                    return;
                }
                
                // TEMPORAL: Desactivar GameObjects remotos para mejorar rendimiento
                // GameObject remoteGameObject = CreateRemotePlayerGameObject(hornetController, playerId);
                
                // Intentar acceder a PlayerData
                var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                object playerDataInstance = null;
                
                if (playerDataType != null)
                {
                    var instanceProperty = playerDataType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProperty != null)
                    {
                        playerDataInstance = instanceProperty.GetValue(null);
                    }
                }
                
                // Crear jugador remoto simple (sin GameObject visual)
                var remotePlayer = new PlayerCharacter
                {
                    Id = playerId,
                    Name = "Remote_Knight",
                    Type = CharacterType.Knight,
                    Controller = hornetController, // Usar referencia original
                    PlayerData = playerDataInstance,
                    IsHost = false,
                    IsActive = true
                };
                
                _playerCharacters[playerId] = remotePlayer;
                // _remotePlayerObjects[playerId] = remoteGameObject; // Desactivado temporalmente
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"HOST: Created remote player with visual: {playerId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating remote player {playerId}: {ex.Message}");
            }
        }
         
         /// <summary>
         /// Crea un jugador remoto en el CLIENTE para visualizar otros jugadores
         /// </summary>
         private void CreateRemotePlayerForClient(NetworkPlayerData hostPlayer)
         {
             try
             {
                 // No crear si es el jugador local
                 if (hostPlayer.Id == "knight_client") return;
                 
                 // Buscar el controlador de Hornet para usar como referencia
                 GameObject hornetController = GameObject.Find("Hero_Hornet");
                 if (!hornetController)
                     hornetController = GameObject.Find("Hero_Hornet(Clone)");
                 
                 if (hornetController == null)
                 {
                     Logger.LogError($"Cannot create remote player {hostPlayer.Id}: No Hornet controller found");
                     return;
                 }
                 
                 // TEMPORAL: Desactivar GameObjects remotos para mejorar rendimiento
                 // GameObject remoteGameObject = CreateRemotePlayerGameObject(hornetController, hostPlayer.Id);
                 
                 // Crear jugador remoto simple para visualización
                 var remotePlayer = new PlayerCharacter
                 {
                     Id = hostPlayer.Id,
                     Name = hostPlayer.Name,
                     Type = hostPlayer.Type,
                     Controller = hornetController, // Usar referencia original
                     PlayerData = null, // No necesario para visualización
                     IsHost = hostPlayer.IsHost,
                     IsActive = hostPlayer.IsActive,
                     LastPosition = hostPlayer.Position,
                     LastHealth = hostPlayer.Health,
                     LastMaxHealth = hostPlayer.MaxHealth
                 };
                 
                 _playerCharacters[hostPlayer.Id] = remotePlayer;
                 // _remotePlayerObjects[hostPlayer.Id] = remoteGameObject; // Desactivado temporalmente
                 
                 if (Config.PrintDebugOutput)
                     Logger.LogInfo($"CLIENT: Created remote player visualization with GameObject: {hostPlayer.Id} ({hostPlayer.Name})");
             }
             catch (Exception ex)
             {
                 Logger.LogError($"Error creating remote player for client {hostPlayer.Id}: {ex.Message}");
             }
         }
          
          /// <summary>
          /// Crea un GameObject visual para un jugador remoto clonando Hornet
          /// </summary>
          private GameObject CreateRemotePlayerGameObject(GameObject originalHornet, string playerId)
          {
              try
              {
                  // Clonar el GameObject de Hornet
                  GameObject remotePlayer = UnityEngine.Object.Instantiate(originalHornet);
                  remotePlayer.name = $"RemotePlayer_{playerId}";
                  
                  // Asignar color único al jugador
                  Color playerColor = GetPlayerColor(playerId);
                  ApplyPlayerColor(remotePlayer, playerColor);
                  
                  // Desactivar componentes que solo debe tener el jugador local
                  DisableLocalPlayerComponents(remotePlayer);
                  
                  // Añadir etiqueta identificativa
                  AddPlayerNameTag(remotePlayer, playerId);
                  
                  if (Config.PrintDebugOutput)
                      Logger.LogInfo($"Created visual GameObject for {playerId} with color {playerColor}");
                  
                  return remotePlayer;
              }
              catch (Exception ex)
              {
                  Logger.LogError($"Error creating remote player GameObject: {ex.Message}");
                  return null;
              }
          }
          
          /// <summary>
          /// Obtiene un color único para un jugador
          /// </summary>
          private Color GetPlayerColor(string playerId)
          {
              if (_playerColors.ContainsKey(playerId))
                  return _playerColors[playerId];
              
              Color color = _availableColors[_nextColorIndex % _availableColors.Count];
              _nextColorIndex++;
              _playerColors[playerId] = color;
              
              return color;
          }
          
          /// <summary>
          /// Aplica un color al sprite del jugador
          /// </summary>
          private void ApplyPlayerColor(GameObject player, Color color)
          {
              try
              {
                  var spriteRenderer = player.GetComponent<tk2dSprite>();
                  if (spriteRenderer != null)
                  {
                      spriteRenderer.color = color;
                  }
                  
                  // También aplicar a sprites hijos si existen
                  var childSprites = player.GetComponentsInChildren<tk2dSprite>();
                  foreach (var sprite in childSprites)
                  {
                      sprite.color = color;
                  }
              }
              catch (Exception ex)
              {
                  if (Config.PrintDebugOutput)
                      Logger.LogError($"Error applying color to player: {ex.Message}");
              }
          }
          
          /// <summary>
          /// Desactiva componentes que solo debe tener el jugador local
          /// </summary>
          private void DisableLocalPlayerComponents(GameObject remotePlayer)
          {
              try
              {
                  // Desactivar controles de input
                  var inputHandler = remotePlayer.GetComponent("HeroController");
                  if (inputHandler != null)
                  {
                      ((MonoBehaviour)inputHandler).enabled = false;
                  }
                  
                  // Desactivar cámara si existe
                  var cameraController = remotePlayer.GetComponent("CameraController");
                  if (cameraController != null)
                  {
                      ((MonoBehaviour)cameraController).enabled = false;
                  }
                  
                  // Mantener solo componentes visuales y de física
                   var rigidbody = remotePlayer.GetComponent<Rigidbody2D>();
                   if (rigidbody != null)
                   {
                       rigidbody.bodyType = RigidbodyType2D.Kinematic; // Controlado por red, no por física
                   }
              }
              catch (Exception ex)
              {
                  if (Config.PrintDebugOutput)
                      Logger.LogError($"Error disabling local components: {ex.Message}");
              }
          }
          
          /// <summary>
          /// Añade una etiqueta con el nombre del jugador
          /// </summary>
          private void AddPlayerNameTag(GameObject player, string playerId)
          {
              try
              {
                  // Crear un GameObject hijo para la etiqueta
                  GameObject nameTag = new GameObject($"NameTag_{playerId}");
                  nameTag.transform.SetParent(player.transform);
                  nameTag.transform.localPosition = new Vector3(0, 2f, 0); // Encima del jugador
                  
                  // TODO: Añadir TextMesh o UI Text para mostrar el nombre
                  // Por ahora solo creamos el GameObject contenedor
                  
                  if (Config.PrintDebugOutput)
                      Logger.LogInfo($"Added name tag for {playerId}");
              }
              catch (Exception ex)
              {
                  if (Config.PrintDebugOutput)
                      Logger.LogError($"Error adding name tag: {ex.Message}");
              }
          }
          
          #endregion
      }
    
    /// <summary>
    /// Datos de un enemigo
    /// </summary>
    public class EnemyData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public bool IsActive { get; set; }
        public int Health { get; set; }
        public bool IsDead { get; set; }
        public float DeathTime { get; set; }
    }
    
    /// <summary>
    /// Datos específicos de un jefe
    /// </summary>
    public class BossData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public bool IsActive { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int CurrentPhase { get; set; }
        public bool IsInCombat { get; set; }
        public float LastAttackTime { get; set; }
        public string CurrentAttack { get; set; }
        public Dictionary<string, object> BossSpecificData { get; set; }
        
        public BossData()
        {
            BossSpecificData = new Dictionary<string, object>();
        }
    }
    
    /// <summary>
    /// Datos de objetos del mundo (destructibles, interactivos)
    /// </summary>
    public class WorldObjectData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public bool IsActive { get; set; }
        public bool IsDestroyed { get; set; }
        public bool IsInteracted { get; set; }
        public string ObjectType { get; set; } // "destructible", "collectible", "switch", "door", etc.
        public Dictionary<string, object> ObjectSpecificData { get; set; }
        
        public WorldObjectData()
        {
            ObjectSpecificData = new Dictionary<string, object>();
        }
    }
    
    /// <summary>
    /// Evento de daño
    /// </summary>
    public class DamageEvent
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public int Damage { get; set; }
        public float Timestamp { get; set; }
    }
    
    /// <summary>
    /// Datos de un personaje jugador
    /// </summary>
    public class PlayerCharacter
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public CharacterType Type { get; set; }
        public GameObject Controller { get; set; }
        public object PlayerData { get; set; }
        public int LastHealth { get; set; }
        public int LastMaxHealth { get; set; }
        public Vector3 LastPosition { get; set; }
        public bool IsHost { get; set; }
        public bool IsActive { get; set; }
        
        public PlayerCharacter()
        {
            LastHealth = -1;
            LastMaxHealth = -1;
            IsActive = false;
        }
    }
    
    /// <summary>
    /// Tipos de personajes disponibles
    /// </summary>
    public enum CharacterType
    {
        Hornet,     // Personaje principal (host)
        Knight      // El Caballero (guest)
    }
}