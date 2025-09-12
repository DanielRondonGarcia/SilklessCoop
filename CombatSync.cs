using BepInEx.Logging;
using System;
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
        
        // Sistema de salud del jugador
        private GameObject _heroController = null;
        private object _playerData = null;
        private int _lastPlayerHealth = -1;
        private int _lastPlayerMaxHealth = -1;
        
        // Sistema de enemigos
        private Dictionary<string, EnemyData> _trackedEnemies = new Dictionary<string, EnemyData>();
        private Dictionary<string, EnemyData> _remoteEnemies = new Dictionary<string, EnemyData>();
        private float _lastEnemySyncTime = 0f;
        private const float ENEMY_SYNC_INTERVAL = 0.2f; // OPTIMIZADO: 5 veces por segundo (era 10)
        
        // Sistema específico de jefes optimizado
        private Dictionary<string, BossData> _activeBosses = new Dictionary<string, BossData>();
        private Dictionary<string, BossData> _remoteBosses = new Dictionary<string, BossData>();
        private float _lastBossSyncTime = 0f;
        private const float BOSS_SYNC_INTERVAL = 0.1f; // OPTIMIZADO: 10 veces por segundo (era 20)
        
        // Sistema de daño y combate optimizado
        private Dictionary<string, DamageEvent> _pendingDamageEvents = new Dictionary<string, DamageEvent>();
        private float _lastCombatSyncTime = 0f;
        private const float COMBAT_SYNC_INTERVAL = 0.15f; // OPTIMIZADO: ~6.7 veces por segundo (era 20)
        
        // Datos pendientes para envío
        private string _pendingHealthData;
        private List<string> _pendingEnemyData = new List<string>();
        private List<string> _pendingCombatEvents = new List<string>();
        private List<string> _pendingBossData = new List<string>();
        
        // Sistema de tracking de jugadores para activación automática de jefes
        private Dictionary<string, Vector3> _playersInBossArea = new Dictionary<string, Vector3>();
        private float _lastPlayerTrackingTime = 0f;
        private const float PLAYER_TRACKING_INTERVAL = 0.5f; // 2 veces por segundo
        private const float BOSS_AREA_RADIUS = 15f; // Radio para detectar jugadores cerca de jefes
        private float _lastBossActivationCheck = 0f;
        private const float BOSS_ACTIVATION_CHECK_INTERVAL = 1f; // 1 vez por segundo
        
        private bool _initialized = false;
        
        private void Start()
        {
            _connector = GetComponent<Connector>();
            _gameSync = GetComponent<GameSync>();
            _audioSync = GetComponent<AudioSync>();
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
                
            // Sincronizar salud del jugador
            SyncPlayerHealth();
            
            // Sincronizar enemigos
            if (Time.time - _lastEnemySyncTime >= ENEMY_SYNC_INTERVAL)
            {
                SyncEnemies();
                _lastEnemySyncTime = Time.time;
            }
            
            // Sincronizar combate
            if (Time.time - _lastCombatSyncTime >= COMBAT_SYNC_INTERVAL)
            {
                SyncCombat();
                _lastCombatSyncTime = Time.time;
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
            
            // Sincronizar jefes con mayor frecuencia
            if (Time.time - _lastBossSyncTime >= BOSS_SYNC_INTERVAL)
            {
                SyncBosses();
                _lastBossSyncTime = Time.time;
            }
        }
        
        /// <summary>
        /// Inicializa el sistema de sincronización de combate
        /// </summary>
        private void InitializeCombatSync()
        {
            try
            {
                // Buscar HeroController
                _heroController = GameObject.Find("Hero_Hornet");
                if (!_heroController)
                    _heroController = GameObject.Find("Hero_Hornet(Clone)");
                    
                if (_heroController)
                {
                    // Intentar acceder a PlayerData usando reflexión
                    var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                    if (playerDataType != null)
                    {
                        var instanceProperty = playerDataType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                        if (instanceProperty != null)
                        {
                            _playerData = instanceProperty.GetValue(null);
                            _initialized = true;
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo("CombatSync initialized successfully");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error initializing CombatSync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sincroniza la salud del jugador
        /// </summary>
        private void SyncPlayerHealth()
        {
            try
            {
                if (_playerData == null) return;
                
                var playerDataType = _playerData.GetType();
                
                // Obtener salud actual
                var healthField = playerDataType.GetField("health");
                var maxHealthField = playerDataType.GetField("maxHealth");
                
                if (healthField != null && maxHealthField != null)
                {
                    int currentHealth = (int)healthField.GetValue(_playerData);
                    int maxHealth = (int)maxHealthField.GetValue(_playerData);
                    
                    // Verificar si la salud ha cambiado
                    if (currentHealth != _lastPlayerHealth || maxHealth != _lastPlayerMaxHealth)
                    {
                        _lastPlayerHealth = currentHealth;
                        _lastPlayerMaxHealth = maxHealth;
                        
                        // Preparar datos para envío
                        _pendingHealthData = $"HEALTH::{currentHealth}::{maxHealth}";
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Player health changed: {currentHealth}/{maxHealth}");
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
        /// Sincroniza enemigos en la escena
        /// </summary>
        private void SyncEnemies()
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
                    if (enemy == null || !enemy.activeInHierarchy) continue;
                    
                    string enemyId = $"{enemy.name}_{enemy.GetInstanceID()}";
                    var enemyData = new EnemyData
                    {
                        Id = enemyId,
                        Name = enemy.name,
                        Position = enemy.transform.position,
                        IsActive = enemy.activeInHierarchy,
                        Health = GetEnemyHealth(enemy)
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
        /// Sincroniza eventos de combate
        /// </summary>
        private void SyncCombat()
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
                
                // Detectar ataques del jugador
                if (_heroController != null)
                {
                    var audioSources = _heroController.GetComponentsInChildren<AudioSource>();
                    foreach (var source in audioSources)
                    {
                        if (source.isPlaying && source.clip != null)
                        {
                            string clipName = source.clip.name.ToLower();
                            
                            // Detectar sonidos de ataque
                            if (clipName.Contains("attack") || clipName.Contains("slash") || 
                                clipName.Contains("hit") || clipName.Contains("swing"))
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"player_attack_{clipName}",
                                    source.clip,
                                    _heroController.transform.position,
                                    source.volume,
                                    source.pitch
                                );
                            }
                            
                            // Detectar sonidos de daño recibido
                            if (clipName.Contains("damage") || clipName.Contains("hurt") || 
                                clipName.Contains("pain") || clipName.Contains("hit_player"))
                            {
                                _audioSync.RegisterAudioEvent(
                                    $"player_damage_{clipName}",
                                    source.clip,
                                    _heroController.transform.position,
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
        /// Sincroniza jefes específicamente
        /// </summary>
        private void SyncBosses()
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
                
                if (Vector3.Distance(tracked.Position, current.Position) > 0.1f ||
                    tracked.IsActive != current.IsActive ||
                    tracked.Health != current.Health ||
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
                
                if (Vector3.Distance(tracked.Position, current.Position) > 0.1f ||
                    tracked.IsActive != current.IsActive ||
                    tracked.Health != current.Health)
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
                parts.Add($"{enemy.Id}|{enemy.Name}|{enemy.Position.x:F2}|{enemy.Position.y:F2}|{enemy.IsActive}|{enemy.Health}");
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
                if (parts.Length >= 3 && parts[0] == "HEALTH")
                {
                    int health = int.Parse(parts[1]);
                    int maxHealth = int.Parse(parts[2]);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Player {playerId} health: {health}/{maxHealth}");
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
                var enemies = new Dictionary<string, EnemyData>();
                var enemyParts = enemyData.Split(';');
                
                foreach (var enemyPart in enemyParts)
                {
                    if (string.IsNullOrEmpty(enemyPart)) continue;
                    
                    var parts = enemyPart.Split('|');
                    if (parts.Length >= 6)
                    {
                        var enemy = new EnemyData
                        {
                            Id = parts[0],
                            Name = parts[1],
                            Position = new Vector3(float.Parse(parts[2]), float.Parse(parts[3]), 0),
                            IsActive = bool.Parse(parts[4]),
                            Health = int.Parse(parts[5])
                        };
                        
                        enemies[enemy.Id] = enemy;
                    }
                }
                
                _remoteEnemies = enemies;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Received enemy data from {playerId}: {enemies.Count} enemies");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error applying enemy data: {ex.Message}");
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
                
                if (dataToSend.Count > 0)
                {
                    return $"COMBAT::{string.Join("|", dataToSend)}";
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
                
                string[] dataParts = data.Split('|');
                
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
                var bosses = new Dictionary<string, BossData>();
                var bossParts = bossData.Split(';');
                
                foreach (var bossPart in bossParts)
                {
                    if (string.IsNullOrEmpty(bossPart)) continue;
                    
                    var parts = bossPart.Split('|');
                    if (parts.Length >= 11)
                    {
                        var boss = new BossData
                        {
                            Id = parts[0],
                            Name = parts[1],
                            Position = new Vector3(float.Parse(parts[2]), float.Parse(parts[3]), 0),
                            IsActive = bool.Parse(parts[4]),
                            Health = int.Parse(parts[5]),
                            MaxHealth = int.Parse(parts[6]),
                            CurrentPhase = int.Parse(parts[7]),
                            IsInCombat = bool.Parse(parts[8]),
                            LastAttackTime = float.Parse(parts[9]),
                            CurrentAttack = parts[10]
                        };
                        
                        bosses[boss.Id] = boss;
                        
                        // TODO: Reactivar sincronización de audio con jefes
                        // Sincronizar audio si el jefe está en combate - TEMPORALMENTE DESACTIVADO
                        /*
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
                        */
                    }
                }
                
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
        /// Resetea el sistema de sincronización
        /// </summary>
        public void Reset()
        {
            _trackedEnemies.Clear();
            _remoteEnemies.Clear();
            _pendingDamageEvents.Clear();
            _lastPlayerHealth = -1;
            _lastPlayerMaxHealth = -1;
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
    /// Evento de daño
    /// </summary>
    public class DamageEvent
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public int Damage { get; set; }
        public float Timestamp { get; set; }
    }
}