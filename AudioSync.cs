using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace SilklessCoop
{
    /// <summary>
    /// Información de tracking de un boss
    /// </summary>
    internal class BossTrackingInfo
    {
        public GameObject BossObject { get; set; }
        public bool IsVisible { get; set; }
        public bool IsInCombat { get; set; }
        public float LastSeenTime { get; set; }
        public Vector3 LastKnownPosition { get; set; }
        public int PlayersInArea { get; set; }
        
        public BossTrackingInfo(GameObject boss)
        {
            BossObject = boss;
            IsVisible = false;
            IsInCombat = false;
            LastSeenTime = Time.time;
            LastKnownPosition = boss.transform.position;
            PlayersInArea = 0;
        }
    }
    
    /// <summary>
    /// Sistema de sincronización de audio para modo cooperativo
    /// Sincroniza efectos de sonido durante combates, especialmente con jefes
    /// </summary>
    internal class AudioSync : MonoBehaviour
    {
        public ManualLogSource Logger;
        public ModConfig Config;
        
        private Connector _connector;
        private GameSync _gameSync;
        private CombatSync _combatSync;
        
        // Sistema de audio
        private Dictionary<string, AudioEvent> _trackedAudioEvents = new Dictionary<string, AudioEvent>();
        private Dictionary<string, AudioEvent> _remoteAudioEvents = new Dictionary<string, AudioEvent>();
        private float _lastAudioSyncTime = 0f;
        private const float AUDIO_SYNC_INTERVAL = 0.1f; // OPTIMIZADO: 10 veces por segundo (era 20)
        
        // Datos pendientes para envío
        private List<string> _pendingAudioData = new List<string>();
        
        // Sincronización adaptativa optimizada
        private int _activeAudioEvents = 0;
        private float _dynamicSyncInterval = AUDIO_SYNC_INTERVAL;
        private const float MIN_SYNC_INTERVAL = 0.05f; // OPTIMIZADO: 20 FPS máximo (era 50)
        private const float MAX_SYNC_INTERVAL = 0.5f;  // OPTIMIZADO: 2 FPS mínimo (era 5)
        
        // Configuración de intervalos optimizados
        private const float BOSS_UPDATE_INTERVAL = 1.0f; // OPTIMIZADO: cada 1 segundo (era 0.5)
        private const float PLAYER_UPDATE_INTERVAL = 0.5f; // OPTIMIZADO: cada 0.5 segundos (era 0.2)
        private const float CLEANUP_INTERVAL = 10f; // OPTIMIZADO: cada 10 segundos (era 5)
        
        // Timers
        private float _bossUpdateTimer = 0f;
        private float _playerUpdateTimer = 0f;
        private float _cleanupTimer = 0f;
        
        // Audio sources activos en la escena
        private Dictionary<string, AudioSource> _trackedAudioSources = new Dictionary<string, AudioSource>();
        
        // Sistema de tracking inteligente de bosses
        private Dictionary<string, BossTrackingInfo> _activeBosses = new Dictionary<string, BossTrackingInfo>();
        private Dictionary<string, GameObject> _nearbyPlayers = new Dictionary<string, GameObject>();
        private const float PROXIMITY_CHECK_INTERVAL = 1f; // Verificar proximidad cada segundo
        private const float BOSS_AREA_RADIUS = 25f; // Radio del área de boss
        private const float AUDIO_SYNC_RADIUS = 40f; // Radio para sincronizar audio
        private Vector3 _lastPlayerPosition = Vector3.zero;
        private const float PLAYER_MOVE_THRESHOLD = 3f; // Distancia mínima de movimiento para actualizar
        
        // Control de activación de bosses
        private bool _bossTrackingEnabled = false;
        private int _playersInBossArea = 0;
        private int _totalPlayers = 0;
        
        // Layers para optimizar detección
        private LayerMask _bossLayerMask = -1; // Todos los layers por defecto
        private Collider2D[] _proximityResults = new Collider2D[20]; // Buffer reutilizable
        
        // Pool de objetos y caché optimizado
        private AudioObjectPool _audioPool;
        private Dictionary<string, AudioClip> _cachedAudioClips = new Dictionary<string, AudioClip>();
        private bool _audioClipsCached = false;
        private float _lastCacheUpdate = 0f;
        private const float CACHE_UPDATE_INTERVAL = 60f; // OPTIMIZADO: cada 60 segundos
        
        // Información adicional para BossTrackingInfo
        public class BossTrackingInfo
        {
            public GameObject BossObject { get; set; }
            public bool IsVisible { get; set; }
            public bool IsInCombat { get; set; }
            public float LastSeenTime { get; set; }
            public Vector3 LastKnownPosition { get; set; }
            public int PlayersInArea { get; set; }
            public int CurrentHealth { get; set; }
            public int MaxHealth { get; set; }
            public float DistanceToPlayer { get; set; }
            
            public BossTrackingInfo(GameObject boss)
            {
                BossObject = boss;
                IsVisible = false;
                IsInCombat = false;
                LastSeenTime = Time.time;
                LastKnownPosition = boss.transform.position;
                PlayersInArea = 0;
                CurrentHealth = 0;
                MaxHealth = 0;
                DistanceToPlayer = 0f;
            }
        }
        
        // Configuración de proximidad optimizada con LOD
        private const float MAX_AUDIO_DISTANCE = 50f; // Máxima distancia para sincronizar audio
        private const float AUDIO_LOD_NEAR = 20f;   // Distancia cercana - máxima calidad
        private const float AUDIO_LOD_MID = 40f;    // Distancia media - calidad reducida
        private const float AUDIO_LOD_FAR = 60f;    // Distancia lejana - solo eventos importantes
        
        // Sistema LOD para audio culling
        private float _currentAudioLOD = 1f; // Multiplicador de LOD actual
        private Dictionary<string, float> _audioSourceLOD = new Dictionary<string, float>();
        
        // Cache inteligente de AudioClips
        private Dictionary<string, AudioClip> _audioClipCache = new Dictionary<string, AudioClip>();
        private Dictionary<string, float> _clipLastAccessTime = new Dictionary<string, float>();
        private const float CLIP_CACHE_TIMEOUT = 120f; // 2 minutos antes de limpiar clips no usados
        private const int MAX_CACHED_CLIPS = 50; // Máximo número de clips en cache
        
        private Transform _playerTransform;
        
        // Eventos de audio específicos para jefes
        private readonly string[] _bossAudioEvents = {
            "boss_attack",
            "boss_damage",
            "boss_death",
            "boss_roar",
            "boss_phase_change",
            "boss_special_attack"
        };
        
        // Eventos de audio de combate general
        private readonly string[] _combatAudioEvents = {
            "player_attack",
            "player_damage",
            "enemy_damage",
            "weapon_clash",
            "spell_cast"
        };
        
        private bool _initialized = false;
        
        private void Start()
        {
            _connector = GetComponent<Connector>();
            _gameSync = GetComponent<GameSync>();
            _combatSync = GetComponent<CombatSync>();
        }
        
        private void Update()
        {
            if (!_initialized)
            {
                InitializeAudioSync();
                return;
            }
            
            if (_connector == null || !_connector.Active)
                return;
            
            // Actualizar proximidad de jugadores
            if (Time.time - _playerUpdateTimer >= PLAYER_UPDATE_INTERVAL)
            {
                UpdatePlayerProximity();
                _playerUpdateTimer = Time.time;
            }
            
            // Actualizar tracking de bosses
            if (Time.time - _bossUpdateTimer >= BOSS_UPDATE_INTERVAL)
            {
                UpdateBossTracking();
                _bossUpdateTimer = Time.time;
            }
            
            // Limpiar bosses destruidos
            if (Time.time - _cleanupTimer >= CLEANUP_INTERVAL)
            {
                CleanupDestroyedBosses();
                _cleanupTimer = Time.time;
            }
            
            // Sincronizar audio solo si el tracking está habilitado y hay jugadores cerca
            if (_bossTrackingEnabled && Time.time - _lastAudioSyncTime >= _dynamicSyncInterval)
            {
                SyncAudio();
                _lastAudioSyncTime = Time.time;
                
                // Ajustar frecuencia de sincronización basada en actividad
                AdjustSyncFrequency();
            }
        }
        
        /// <summary>
        /// Inicializa el sistema de sincronización de audio
        /// </summary>
        internal void Initialize(ManualLogSource logger, ModConfig config)
        {
            Logger = logger;
            Config = config;
            
            // Obtener número total de jugadores
            _totalPlayers = GetConnectedPlayersCount();
            
            // Inicializar pool de objetos
            _audioPool = new AudioObjectPool();
            
            if (Config.PrintDebugOutput)
                Logger.LogInfo($"AudioSync initialized with {_totalPlayers} players and object pooling");
        }
        
        /// <summary>
        /// Inicializa el sistema de sincronización de audio
        /// </summary>
        private void InitializeAudioSync()
        {
            try
            {
                // Obtener referencia al jugador
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    _playerTransform = player.transform;
                
                // Buscar y registrar AudioSources en la escena
                RegisterAudioSources();
                
                // Inicializar caché de AudioClips
                CacheAudioClips();
                
                // Inicializar sistema de proximidad
                InitializeProximitySystem();
                
                _initialized = true;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo("AudioSync initialized successfully");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error initializing AudioSync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Registra AudioSources activos en la escena (OPTIMIZADO)
        /// </summary>
        private void RegisterAudioSources()
        {
            try
            {
                // OPTIMIZACIÓN: Solo buscar AudioSources si han pasado suficiente tiempo
                if (Time.time - _lastCacheUpdate < CACHE_UPDATE_INTERVAL)
                    return;
                
                var audioSources = FindObjectsOfType<AudioSource>()
                    .Where(source => source != null && source.gameObject.activeInHierarchy)
                    .ToList();
                
                // Limpiar sources destruidos del tracking
                var keysToRemove = _trackedAudioSources.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
                foreach (var key in keysToRemove)
                {
                    _trackedAudioSources.Remove(key);
                }
                
                // Solo agregar nuevos sources
                foreach (var source in audioSources)
                {
                    string sourceId = $"{source.gameObject.name}_{source.GetInstanceID()}";
                    
                    if (!_trackedAudioSources.ContainsKey(sourceId))
                    {
                        _trackedAudioSources[sourceId] = source;
                        _audioPool.CacheAudioSource(sourceId, source);
                        
                        // Agregar detector solo si no existe
                        if (!source.gameObject.GetComponent<AudioEventDetector>())
                        {
                            var detector = source.gameObject.AddComponent<AudioEventDetector>();
                            detector.Initialize(this, sourceId);
                        }
                    }
                }
                
                _lastCacheUpdate = Time.time;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Registered {_trackedAudioSources.Count} audio sources (cached)");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error registering audio sources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Sincroniza eventos de audio
        /// </summary>
        private void SyncAudio()
        {
            try
            {
                // Detectar nuevos eventos de audio
                DetectAudioEvents();
                
                // Procesar sincronización de audio para bosses activos
                ProcessAudioSync();
                
                // Actualizar contador de eventos activos
                _activeAudioEvents = _trackedAudioEvents.Count;
                
                // Verificar si hay eventos pendientes para enviar
                if (_trackedAudioEvents.Count > 0)
                {
                    string audioData = SerializeAudioData(_trackedAudioEvents);
                    _pendingAudioData.Add(audioData);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Audio events queued for sync: {_trackedAudioEvents.Count}");
                    
                    _trackedAudioEvents.Clear();
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error syncing audio: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa la sincronización de audio para bosses activos y jugadores cercanos
        /// </summary>
        private void ProcessAudioSync()
        {
            try
            {
                if (!_bossTrackingEnabled || _activeBosses.Count == 0) return;
                
                foreach (var bossEntry in _activeBosses)
                {
                    var bossInfo = bossEntry.Value;
                    if (bossInfo?.BossObject == null) continue;
                    
                    var bossObj = bossInfo.BossObject;
                    
                    // Solo procesar audio si el boss está visible o en combate
                    if (!bossInfo.IsVisible && !bossInfo.IsInCombat) continue;
                    
                    // Verificar si hay jugadores lo suficientemente cerca para el audio
                    bool hasNearbyPlayers = false;
                    foreach (var playerEntry in _nearbyPlayers)
                    {
                        var playerObj = playerEntry.Value;
                        if (playerObj != null)
                        {
                            float distance = Vector3.Distance(playerObj.transform.position, bossObj.transform.position);
                            if (distance <= AUDIO_SYNC_RADIUS)
                            {
                                hasNearbyPlayers = true;
                                break;
                            }
                        }
                    }
                    
                    if (!hasNearbyPlayers) continue;
                    
                    var audioSources = bossObj.GetComponentsInChildren<AudioSource>();
                    foreach (var audioSource in audioSources)
                    {
                        if (audioSource != null && audioSource.isPlaying)
                        {
                            ProcessBossAudio(bossObj, audioSource);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error processing audio sync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Procesa el audio de un boss específico
        /// </summary>
        private void ProcessBossAudio(GameObject bossObj, AudioSource audioSource)
        {
            try
            {
                if (audioSource.clip == null) return;
                
                string eventId = $"boss_{bossObj.name}_{audioSource.clip.name}_{Time.time}";
                var audioEvent = new AudioEvent
                {
                    Id = eventId,
                    SourceName = bossObj.name,
                    ClipName = audioSource.clip.name,
                    Position = bossObj.transform.position,
                    Volume = audioSource.volume,
                    Pitch = audioSource.pitch,
                    Time = audioSource.time,
                    EventType = "boss_audio",
                    Timestamp = Time.time
                };
                
                _trackedAudioEvents[eventId] = audioEvent;
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error processing boss audio: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detecta eventos de audio solo para bosses activos y jugadores cercanos
        /// </summary>
        private void DetectAudioEvents()
        {
            try
            {
                // Solo procesar si el tracking está habilitado
                if (!_bossTrackingEnabled || _activeBosses.Count == 0) return;
                
                // Procesar solo jefes que están en combate o son visibles
                foreach (var bossInfo in _activeBosses.Values)
                {
                    if (bossInfo?.BossObject != null && 
                        bossInfo.BossObject.activeInHierarchy && 
                        (bossInfo.IsInCombat || bossInfo.IsVisible))
                    {
                        // Solo sincronizar audio si hay jugadores cerca y está dentro del rango
                        if (bossInfo.PlayersInArea > 0 && IsWithinAudioRange(bossInfo.LastKnownPosition))
                        {
                            DetectBossAudioEvents(bossInfo.BossObject);
                        }
                    }
                }
                
                // Detectar eventos de combate general solo si hay jugadores cerca
                if (_playersInBossArea > 0)
                {
                    DetectCombatAudioEvents();
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error detecting audio events: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detecta eventos de audio específicos de jefes (OPTIMIZADO)
        /// </summary>
        private void DetectBossAudioEvents(GameObject boss)
        {
            try
            {
                // OPTIMIZACIÓN: Verificar LOD antes de procesar
                Vector3 bossPosition = boss.transform.position;
                if (!ShouldProcessAudioEvent(bossPosition, "boss_audio")) return;
                
                float audioLOD = CalculateAudioLOD(bossPosition);
                var audioSources = boss.GetComponentsInChildren<AudioSource>();
                
                foreach (var source in audioSources)
                {
                    if (source.isPlaying && source.clip != null)
                    {
                        // OPTIMIZACIÓN: Filtrar por importancia basado en LOD
                        if (audioLOD < 1f && source.volume < 0.3f) continue; // Saltar sonidos débiles en distancia media/lejana
                        
                        string eventId = $"boss_{boss.name}_{source.clip.name}_{Time.time}";
                        
                        // OPTIMIZACIÓN: Usar cache inteligente para AudioClips
                        var cachedClip = GetCachedAudioClip(source);
                        if (cachedClip == null) continue; // Saltar si no se puede cachear
                        
                        // OPTIMIZACIÓN: Usar pool de objetos en lugar de crear nuevos
                        var audioEvent = _audioPool.GetAudioEvent();
                        audioEvent.Id = eventId;
                        audioEvent.SourceName = boss.name;
                        audioEvent.ClipName = cachedClip.name;
                        audioEvent.Position = boss.transform.position;
                        audioEvent.Volume = source.volume * audioLOD; // Ajustar volumen por LOD
                        audioEvent.Pitch = source.pitch;
                        audioEvent.Time = source.time;
                        audioEvent.EventType = "boss_audio";
                        audioEvent.Timestamp = Time.time;
                        
                        _trackedAudioEvents[eventId] = audioEvent;
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error detecting boss audio events: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detecta eventos de audio de combate general
        /// </summary>
        private void DetectCombatAudioEvents()
        {
            try
            {
                // Detectar sonidos de ataques del jugador
                var heroController = GameObject.FindGameObjectWithTag("Player");
                if (heroController != null)
                {
                    var audioSources = heroController.GetComponentsInChildren<AudioSource>();
                    
                    foreach (var source in audioSources)
                    {
                        if (source.isPlaying && source.clip != null)
                        {
                            string eventId = $"player_{source.clip.name}_{Time.time}";
                            var audioEvent = new AudioEvent
                            {
                                Id = eventId,
                                SourceName = "Player",
                                ClipName = source.clip.name,
                                Position = heroController.transform.position,
                                Volume = source.volume,
                                Pitch = source.pitch,
                                Time = source.time,
                                EventType = "player_audio",
                                Timestamp = Time.time
                            };
                            
                            _trackedAudioEvents[eventId] = audioEvent;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error detecting combat audio events: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Serializa datos de audio para envío
        /// </summary>
        private string SerializeAudioData(Dictionary<string, AudioEvent> audioEvents)
        {
            try
            {
                var eventStrings = audioEvents.Values.Select(e => 
                    $"{e.Id}|{e.SourceName}|{e.ClipName}|{e.Position.x:F2}|{e.Position.y:F2}|{e.Volume:F2}|{e.Pitch:F2}|{e.Time:F2}|{e.EventType}|{e.Timestamp:F2}");
                
                return string.Join(";", eventStrings);
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error serializing audio data: {ex.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Procesa datos de audio recibidos de otros jugadores
        /// </summary>
        public void ProcessRemoteAudioData(string audioData)
        {
            try
            {
                if (string.IsNullOrEmpty(audioData)) return;
                
                var events = audioData.Split(';');
                
                foreach (var eventStr in events)
                {
                    if (string.IsNullOrEmpty(eventStr)) continue;
                    
                    var parts = eventStr.Split('|');
                    if (parts.Length >= 10)
                    {
                        var audioEvent = new AudioEvent
                        {
                            Id = parts[0],
                            SourceName = parts[1],
                            ClipName = parts[2],
                            Position = new Vector3(float.Parse(parts[3]), float.Parse(parts[4]), 0),
                            Volume = float.Parse(parts[5]),
                            Pitch = float.Parse(parts[6]),
                            Time = float.Parse(parts[7]),
                            EventType = parts[8],
                            Timestamp = float.Parse(parts[9])
                        };
                        
                        PlayRemoteAudioEvent(audioEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error processing remote audio data: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Reproduce un evento de audio remoto
        /// </summary>
        private void PlayRemoteAudioEvent(AudioEvent audioEvent)
        {
            try
            {
                // Buscar el AudioSource correspondiente o crear uno temporal
                AudioSource targetSource = null;
                
                // Intentar encontrar el AudioSource original
                var matchingSources = _trackedAudioSources.Values
                    .Where(s => s.gameObject.name.Contains(audioEvent.SourceName))
                    .ToList();
                
                if (matchingSources.Count > 0)
                {
                    targetSource = matchingSources.First();
                }
                else
                {
                    // Crear AudioSource temporal
                    var tempObject = new GameObject($"TempAudio_{audioEvent.Id}");
                    tempObject.transform.position = audioEvent.Position;
                    targetSource = tempObject.AddComponent<AudioSource>();
                    
                    // Destruir después de reproducir
                    Destroy(tempObject, 5f);
                }
                
                if (targetSource != null)
                {
                    // Buscar el clip de audio en caché
                    AudioClip clip = null;
                    if (!_cachedAudioClips.TryGetValue(audioEvent.ClipName, out clip))
                    {
                        // Si no está en caché, buscarlo y agregarlo
                        clip = Resources.FindObjectsOfTypeAll<AudioClip>()
                            .FirstOrDefault(c => c.name == audioEvent.ClipName);
                        if (clip != null)
                            _cachedAudioClips[audioEvent.ClipName] = clip;
                    }
                    
                    if (clip != null)
                    {
                        targetSource.clip = clip;
                        targetSource.volume = audioEvent.Volume * 0.7f; // Reducir volumen para audio remoto
                        targetSource.pitch = audioEvent.Pitch;
                        targetSource.time = audioEvent.Time;
                        targetSource.Play();
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Playing remote audio: {audioEvent.ClipName} from {audioEvent.SourceName}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error playing remote audio event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Obtiene datos de audio pendientes para envío
        /// </summary>
        public string GetPendingAudioData()
        {
            if (_pendingAudioData.Count == 0) return null;
            
            string data = string.Join("#", _pendingAudioData);
            _pendingAudioData.Clear();
            return data;
        }
        
        /// <summary>
        /// Registra un evento de audio manualmente
        /// </summary>
        public void RegisterAudioEvent(string sourceId, AudioClip clip, Vector3 position, float volume, float pitch)
        {
            try
            {
                string eventId = $"{sourceId}_{clip.name}_{Time.time}";
                var audioEvent = new AudioEvent
                {
                    Id = eventId,
                    SourceName = sourceId,
                    ClipName = clip.name,
                    Position = position,
                    Volume = volume,
                    Pitch = pitch,
                    Time = 0f,
                    EventType = "manual",
                    Timestamp = Time.time
                };
                
                _trackedAudioEvents[eventId] = audioEvent;
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error registering audio event: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Inicializa el sistema de tracking inteligente
        /// </summary>
        private void InitializeProximitySystem()
        {
            try
            {
                if (_playerTransform != null)
                {
                    _lastPlayerPosition = _playerTransform.position;
                    
                    // Obtener número total de jugadores conectados
                    _totalPlayers = GetConnectedPlayersCount();
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Intelligent tracking system initialized. Total players: {_totalPlayers}");
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error initializing intelligent tracking system: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualiza la proximidad de jugadores y detecta áreas de boss
        /// </summary>
        private void UpdatePlayerProximity()
        {
            try
            {
                if (_playerTransform == null) return;
                
                Vector3 currentPlayerPos = _playerTransform.position;
                
                // Solo actualizar si el jugador se ha movido significativamente
                if (Vector3.Distance(currentPlayerPos, _lastPlayerPosition) < PLAYER_MOVE_THRESHOLD)
                {
                    return;
                }
                
                _lastPlayerPosition = currentPlayerPos;
                _nearbyPlayers.Clear();
                _playersInBossArea = 0;
                
                // Buscar jugadores cercanos
                var playerColliders = Physics2D.OverlapCircleAll(
                    currentPlayerPos, 
                    AUDIO_SYNC_RADIUS, 
                    _bossLayerMask
                );
                
                foreach (var collider in playerColliders)
                {
                    if (collider != null && collider.gameObject != null)
                    {
                        var gameObj = collider.gameObject;
                        
                        // Verificar si es un jugador
                        if (IsPlayerObject(gameObj))
                        {
                            string playerId = $"{gameObj.name}_{gameObj.GetInstanceID()}";
                            _nearbyPlayers[playerId] = gameObj;
                            _playersInBossArea++;
                        }
                    }
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Players in area: {_playersInBossArea}/{_totalPlayers}");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error updating player proximity: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Actualiza el tracking de bosses basado en visibilidad y combate
        /// </summary>
        private void UpdateBossTracking()
        {
            try
            {
                if (_playerTransform == null) return;
                
                Vector3 currentPlayerPos = _playerTransform.position;
                
                // Buscar bosses en el área solo si hay jugadores
                if (_playersInBossArea == 0)
                {
                    // Desactivar tracking si no hay jugadores
                    _bossTrackingEnabled = false;
                    _activeBosses.Clear();
                    return;
                }
                
                // Buscar bosses cercanos
                var bossColliders = Physics2D.OverlapCircleAll(
                    currentPlayerPos, 
                    BOSS_AREA_RADIUS, 
                    _bossLayerMask
                );
                
                var foundBosses = new HashSet<string>();
                
                foreach (var collider in bossColliders)
                {
                    if (collider != null && collider.gameObject != null)
                    {
                        var gameObj = collider.gameObject;
                        
                        // Verificar si es un jefe
                        if (IsBossObject(gameObj))
                        {
                            string bossId = $"{gameObj.name}_{gameObj.GetInstanceID()}";
                            foundBosses.Add(bossId);
                            
                            if (!_activeBosses.ContainsKey(bossId))
                            {
                                _activeBosses[bossId] = new BossTrackingInfo(gameObj);
                            }
                            
                            var bossInfo = _activeBosses[bossId];
                            bossInfo.LastSeenTime = Time.time;
                            bossInfo.LastKnownPosition = gameObj.transform.position;
                            bossInfo.PlayersInArea = _playersInBossArea;
                            
                            // Verificar visibilidad y combate
                            UpdateBossState(bossInfo);
                        }
                    }
                }
                
                // Remover bosses que ya no están en el área
                var bossesToRemove = _activeBosses.Keys.Where(id => !foundBosses.Contains(id)).ToList();
                foreach (var bossId in bossesToRemove)
                {
                    _activeBosses.Remove(bossId);
                }
                
                // Activar tracking solo si todos los jugadores están en el área
                _bossTrackingEnabled = (_playersInBossArea >= _totalPlayers && _activeBosses.Count > 0);
                
                // Activar jefes inactivos cuando todos los jugadores están presentes
                if (_bossTrackingEnabled)
                {
                    ActivateInactiveBosses();
                }
                
                if (Config.PrintDebugOutput && _activeBosses.Count > 0)
                    Logger.LogInfo($"Boss tracking: {(_bossTrackingEnabled ? "ENABLED" : "WAITING")} - {_activeBosses.Count} bosses, {_playersInBossArea}/{_totalPlayers} players");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error updating boss tracking: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Verifica si un GameObject es un jefe basándose en nombre, tag o componentes
        /// </summary>
        private bool IsBossObject(GameObject obj)
        {
            if (obj == null) return false;
            
            string objName = obj.name.ToLower();
            
            // Lista de nombres comunes de jefes en Hollow Knight
            string[] bossNames = {
                "boss", "gruz", "vengefly", "false_knight", "hornet", "mothwing", 
                "mantis", "soul_master", "dung_defender", "crystal_guardian",
                "broken_vessel", "watcher_knight", "traitor_lord", "white_defender",
                "grey_prince", "nightmare_king", "radiance", "absolute_radiance",
                "pale_king", "hollow_knight", "pure_vessel", "sisters_of_battle",
                "flukemarm", "massive_moss_charger", "nosk", "collector",
                "god_tamer", "gorb", "elder_hu", "galien", "markoth", "marmu",
                "no_eyes", "xero", "failed_champion", "lost_kin", "soul_tyrant",
                "white_palace", "path_of_pain", "trial", "colosseum"
            };
            
            // Verificar por nombre
            foreach (string bossName in bossNames)
            {
                if (objName.Contains(bossName))
                    return true;
            }
            
            // Verificar por tag
            if (obj.CompareTag("Boss") || obj.CompareTag("Enemy") || obj.CompareTag("MiniBoss"))
                return true;
            
            // Verificar por componentes específicos (esto puede variar según el juego)
            var healthManager = obj.GetComponent("HealthManager");
            var enemyDeathEffects = obj.GetComponent("EnemyDeathEffects");
            var damageHero = obj.GetComponent("DamageHero");
            
            // Si tiene HealthManager con mucha vida, probablemente es un jefe
            if (healthManager != null)
            {
                try
                {
                    var hpField = healthManager.GetType().GetField("hp");
                    if (hpField != null)
                    {
                        int hp = (int)hpField.GetValue(healthManager);
                        if (hp > 50) // Los jefes suelen tener más de 50 HP
                            return true;
                    }
                }
                catch { /* Ignorar errores de reflexión */ }
            }
            
            return false;
        }
        
        /// <summary>
        /// Verifica si un GameObject es un jugador
        /// </summary>
        private bool IsPlayerObject(GameObject obj)
        {
            if (obj == null) return false;
            
            string objName = obj.name.ToLower();
            
            // Verificar por nombre común de jugadores
            if (objName.Contains("player") || objName.Contains("knight") || objName.Contains("hero"))
                return true;
            
            // Verificar por tag
            if (obj.CompareTag("Player"))
                return true;
            
            // Verificar por componentes específicos del jugador
            var heroController = obj.GetComponent("HeroController");
            var playerData = obj.GetComponent("PlayerData");
            
            return heroController != null || playerData != null;
        }
        
        /// <summary>
        /// Actualiza el estado de un boss específico
        /// </summary>
        private void UpdateBossState(BossTrackingInfo bossInfo)
        {
            if (bossInfo?.BossObject == null) return;
            
            try
            {
                var bossObj = bossInfo.BossObject;
                
                // Verificar si el boss está visible
                var renderer = bossObj.GetComponent<Renderer>();
                bossInfo.IsVisible = renderer != null && renderer.isVisible;
                
                // Verificar si está en combate
                var healthManager = bossObj.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    try
                    {
                        var hpField = healthManager.GetType().GetField("hp");
                         var maxHpField = healthManager.GetType().GetField("maxHealth");
                         
                         if (hpField != null && maxHpField != null)
                         {
                             int currentHp = (int)hpField.GetValue(healthManager);
                             int maxHp = (int)maxHpField.GetValue(healthManager);
                             
                             bossInfo.CurrentHealth = currentHp;
                             bossInfo.MaxHealth = maxHp;
                             
                             // Considerar en combate si ha perdido vida o está activo
                             bossInfo.IsInCombat = (currentHp < maxHp && currentHp > 0) || bossObj.activeInHierarchy;
                         }
                         else if (hpField != null)
                         {
                             int currentHp = (int)hpField.GetValue(healthManager);
                             bossInfo.CurrentHealth = currentHp;
                             
                             // Considerar en combate si está vivo y activo
                             bossInfo.IsInCombat = currentHp > 0 && bossObj.activeInHierarchy;
                         }
                     }
                     catch { /* Ignorar errores de reflexión */ }
                 }
                 
                 // Verificar distancia al jugador
                 if (_playerTransform != null)
                 {
                     float distance = Vector3.Distance(_playerTransform.position, bossObj.transform.position);
                     bossInfo.DistanceToPlayer = distance;
                 }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error updating boss state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Limpia jefes destruidos del caché
        /// </summary>
        private void CleanupDestroyedBosses()
        {
            var keysToRemove = _activeBosses.Where(kvp => kvp.Value?.BossObject == null).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove)
            {
                _activeBosses.Remove(key);
            }
            
            // OPTIMIZACIÓN: Limpiar también el pool de objetos y cache
            if (_audioPool != null)
            {
                _audioPool.CleanupCache();
            }
            
            // Limpiar eventos de audio antiguos para liberar memoria
            var oldEvents = _trackedAudioEvents.Where(kvp => Time.time - kvp.Value.Timestamp > 5f).Select(kvp => kvp.Key).ToList();
            foreach (var eventKey in oldEvents)
            {
                if (_trackedAudioEvents.TryGetValue(eventKey, out AudioEvent audioEvent))
                {
                    _audioPool?.ReturnAudioEvent(audioEvent);
                    _trackedAudioEvents.Remove(eventKey);
                }
            }
            
            // OPTIMIZACIÓN: Limpiar cache de AudioClips periódicamente
            CleanupOldAudioClips();
            
            if (Config.PrintDebugOutput && (keysToRemove.Count > 0 || oldEvents.Count > 0))
                Logger.LogInfo($"Cleaned {keysToRemove.Count} destroyed bosses, {oldEvents.Count} old audio events, and audio clip cache");
        }
        
        /// <summary>
        /// Verifica si una posición está dentro del rango de audio (OPTIMIZADO con LOD)
        /// </summary>
        private bool IsWithinAudioRange(Vector3 position)
        {
            if (_playerTransform == null) return false;
            
            float distance = Vector3.Distance(_playerTransform.position, position);
            return distance <= AUDIO_SYNC_RADIUS;
        }
        
        /// <summary>
        /// Calcula el nivel de detalle (LOD) de audio basado en distancia
        /// </summary>
        private float CalculateAudioLOD(Vector3 position)
        {
            if (_playerTransform == null) return 0f;
            
            float distance = Vector3.Distance(_playerTransform.position, position);
            
            if (distance <= AUDIO_LOD_NEAR)
                return 1f; // Máxima calidad - procesar todos los eventos
            else if (distance <= AUDIO_LOD_MID)
                return 0.5f; // Calidad media - procesar eventos importantes
            else if (distance <= AUDIO_LOD_FAR)
                return 0.25f; // Calidad baja - solo eventos críticos
            else
                return 0f; // Fuera de rango - no procesar
        }
        
        /// <summary>
        /// Determina si un evento de audio debe procesarse basado en LOD
        /// </summary>
        private bool ShouldProcessAudioEvent(Vector3 position, string eventType)
        {
            float lod = CalculateAudioLOD(position);
            
            if (lod >= 1f) return true; // Distancia cercana - procesar todo
            if (lod >= 0.5f && (eventType == "boss_audio" || eventType == "combat")) return true; // Distancia media - solo combate
            if (lod >= 0.25f && eventType == "boss_audio") return true; // Distancia lejana - solo jefes
            
            return false; // Muy lejos o no importante
        }
        
        /// <summary>
        /// Obtiene un AudioClip del cache o lo cachea si no existe
        /// </summary>
        private AudioClip GetCachedAudioClip(AudioSource source)
        {
            if (source?.clip == null) return null;
            
            string clipName = source.clip.name;
            
            // Verificar si ya está en cache
            if (_audioClipCache.ContainsKey(clipName))
            {
                _clipLastAccessTime[clipName] = Time.time; // Actualizar tiempo de acceso
                return _audioClipCache[clipName];
            }
            
            // Agregar al cache si hay espacio
            if (_audioClipCache.Count < MAX_CACHED_CLIPS)
            {
                _audioClipCache[clipName] = source.clip;
                _clipLastAccessTime[clipName] = Time.time;
                return source.clip;
            }
            
            // Cache lleno - limpiar clips antiguos y agregar nuevo
            CleanupOldAudioClips();
            _audioClipCache[clipName] = source.clip;
            _clipLastAccessTime[clipName] = Time.time;
            
            return source.clip;
        }
        
        /// <summary>
        /// Limpia clips de audio antiguos del cache
        /// </summary>
        private void CleanupOldAudioClips()
        {
            var currentTime = Time.time;
            var clipsToRemove = new List<string>();
            
            foreach (var kvp in _clipLastAccessTime)
            {
                if (currentTime - kvp.Value > CLIP_CACHE_TIMEOUT)
                {
                    clipsToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var clipName in clipsToRemove)
            {
                _audioClipCache.Remove(clipName);
                _clipLastAccessTime.Remove(clipName);
            }
            
            if (Config.PrintDebugOutput && clipsToRemove.Count > 0)
                Logger.LogInfo($"[SilklessCoop] Cleaned {clipsToRemove.Count} old audio clips from cache");
        }
        
        /// <summary>
        /// Obtiene el número de jugadores conectados
        /// </summary>
        private int GetConnectedPlayersCount()
        {
            try
            {
                if (_connector != null && _connector.Active)
                {
                    // Intentar obtener el número de jugadores conectados desde el conector
                    // Esto puede variar según la implementación específica del mod
                    return 2; // Valor por defecto para modo cooperativo
                }
                return 1; // Solo el jugador local
            }
            catch
            {
                return 1;
            }
        }
        
        /// <summary>
        /// Inicializa el caché de AudioClips
        /// </summary>
        private void CacheAudioClips()
        {
            try
            {
                if (_audioClipsCached) return;
                
                // Cachear clips de audio más comunes para evitar búsquedas repetidas
                var commonClips = new string[] {
                    "hero_attack", "hero_damage", "hero_death",
                    "boss_roar", "boss_attack", "boss_damage",
                    "sword_clash", "spell_cast", "footstep"
                };
                
                var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
                
                foreach (var clipName in commonClips)
                {
                    var clip = allClips.FirstOrDefault(c => c.name.Contains(clipName));
                    if (clip != null)
                    {
                        _cachedAudioClips[clip.name] = clip;
                    }
                }
                
                _audioClipsCached = true;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Audio clips cached: {_cachedAudioClips.Count} clips");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error caching audio clips: {ex.Message}");
            }
        }
        

        
        /// <summary>
        /// Ajusta la frecuencia de sincronización basada en la actividad de audio
        /// </summary>
        private void AdjustSyncFrequency()
        {
            try
            {
                // Más eventos de audio = mayor frecuencia de sincronización
                if (_activeAudioEvents > 5)
                {
                    _dynamicSyncInterval = MIN_SYNC_INTERVAL; // Máxima frecuencia
                }
                else if (_activeAudioEvents > 2)
                {
                    _dynamicSyncInterval = AUDIO_SYNC_INTERVAL; // Frecuencia normal
                }
                else if (_activeAudioEvents > 0)
                {
                    _dynamicSyncInterval = AUDIO_SYNC_INTERVAL * 2; // Frecuencia reducida
                }
                else
                {
                    _dynamicSyncInterval = MAX_SYNC_INTERVAL; // Mínima frecuencia cuando no hay actividad
                }
                
                // Asegurar que esté dentro de los límites
                _dynamicSyncInterval = Mathf.Clamp(_dynamicSyncInterval, MIN_SYNC_INTERVAL, MAX_SYNC_INTERVAL);
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error adjusting sync frequency: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Resetea el sistema de sincronización de audio
        /// </summary>
        public void Reset()
        {
            try
            {
                _trackedAudioEvents.Clear();
                _remoteAudioEvents.Clear();
                _pendingAudioData.Clear();
                _trackedAudioSources.Clear();
                
                // Limpiar cachés
                _activeBosses.Clear();
                _nearbyPlayers.Clear();
                _cachedAudioClips.Clear();
                _audioClipsCached = false;
                
                // Resetear variables de proximidad
                _lastPlayerPosition = Vector3.zero;
                
                // Limpiar buffer de resultados
                for (int i = 0; i < _proximityResults.Length; i++)
                {
                    _proximityResults[i] = null;
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo("Audio sync system reset");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error resetting audio sync: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Detecta jefes dormidos/inactivos en el área
        /// </summary>
        private List<GameObject> DetectInactiveBosses()
        {
            var inactiveBosses = new List<GameObject>();
            
            try
            {
                foreach (var bossInfo in _activeBosses.Values)
                {
                    if (bossInfo?.BossObject == null) continue;
                    
                    var bossObj = bossInfo.BossObject;
                    
                    // Verificar si el jefe está inactivo/dormido
                    if (IsBossInactive(bossObj))
                    {
                        inactiveBosses.Add(bossObj);
                        
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Detected inactive boss: {bossObj.name}");
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
        /// Verifica si un jefe está inactivo/dormido
        /// </summary>
        private bool IsBossInactive(GameObject boss)
        {
            if (boss == null || !boss.activeInHierarchy) return false;
            
            try
            {
                // Verificar estado de combate
                if (IsBossInCombatState(boss)) return false;
                
                // Verificar si tiene HealthManager y está en estado inicial
                var healthManager = boss.GetComponent("HealthManager");
                if (healthManager != null)
                {
                    var hpField = healthManager.GetType().GetField("hp");
                    var maxHpField = healthManager.GetType().GetField("maxHealth");
                    
                    if (hpField != null && maxHpField != null)
                    {
                        int currentHp = (int)hpField.GetValue(healthManager);
                        int maxHp = (int)maxHpField.GetValue(healthManager);
                        
                        // Si tiene vida completa, podría estar inactivo
                        if (currentHp >= maxHp) return true;
                    }
                }
                
                // Verificar componentes específicos de estado
                var components = boss.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    // Buscar campos de estado inactivo
                    var fields = new string[] { "sleeping", "dormant", "inactive", "awakened", "activated", "started" };
                    
                    foreach (var fieldName in fields)
                    {
                        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType == typeof(bool))
                        {
                            bool value = (bool)field.GetValue(component);
                            
                            // Si está durmiendo, inactivo o no despierto
                            if ((fieldName == "sleeping" || fieldName == "dormant" || fieldName == "inactive") && value)
                                return true;
                            
                            // Si no está despierto o activado
                            if ((fieldName == "awakened" || fieldName == "activated" || fieldName == "started") && !value)
                                return true;
                        }
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Verifica si un jefe está en estado de combate activo
        /// </summary>
        private bool IsBossInCombatState(GameObject boss)
        {
            try
            {
                var components = boss.GetComponents<MonoBehaviour>();
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    // Buscar campos relacionados con estado de combate
                    var combatFields = new string[] { "inCombat", "isActive", "activated", "fighting", "engaged" };
                    
                    foreach (var fieldName in combatFields)
                    {
                        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType == typeof(bool))
                        {
                            bool value = (bool)field.GetValue(component);
                            if (value) return true;
                        }
                    }
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Activa automáticamente jefes dormidos cuando todos los jugadores están presentes
        /// </summary>
        private void ActivateInactiveBosses()
        {
            try
            {
                // Solo activar si todos los jugadores están en el área
                if (_playersInBossArea < _totalPlayers) return;
                
                var inactiveBosses = DetectInactiveBosses();
                
                foreach (var boss in inactiveBosses)
                {
                    if (AwakeBoss(boss))
                    {
                        if (Config.PrintDebugOutput)
                            Logger.LogInfo($"Successfully awakened boss: {boss.name}");
                        
                        // Notificar al CombatSync sobre la activación
                        if (_combatSync != null)
                        {
                            NotifyCombatSyncBossActivation(boss);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error activating inactive bosses: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Despierta/activa un jefe específico usando reflexión
        /// </summary>
        private bool AwakeBoss(GameObject boss)
        {
            if (boss == null) return false;
            
            try
            {
                bool activated = false;
                var components = boss.GetComponents<MonoBehaviour>();
                
                foreach (var component in components)
                {
                    var type = component.GetType();
                    
                    // Intentar activar usando campos de estado
                    var activationFields = new string[] { "awakened", "activated", "started", "inCombat", "isActive" };
                    
                    foreach (var fieldName in activationFields)
                    {
                        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType == typeof(bool))
                        {
                            field.SetValue(component, true);
                            activated = true;
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Set {fieldName} = true for {boss.name}");
                        }
                    }
                    
                    // Desactivar campos de inactividad
                    var deactivationFields = new string[] { "sleeping", "dormant", "inactive" };
                    
                    foreach (var fieldName in deactivationFields)
                    {
                        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null && field.FieldType == typeof(bool))
                        {
                            field.SetValue(component, false);
                            activated = true;
                            
                            if (Config.PrintDebugOutput)
                                Logger.LogInfo($"Set {fieldName} = false for {boss.name}");
                        }
                    }
                    
                    // Intentar llamar métodos de activación
                    var activationMethods = new string[] { "Awake", "Activate", "StartCombat", "Wake", "Begin", "Trigger" };
                    
                    foreach (var methodName in activationMethods)
                    {
                        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (method != null && method.GetParameters().Length == 0)
                        {
                            try
                            {
                                method.Invoke(component, null);
                                activated = true;
                                
                                if (Config.PrintDebugOutput)
                                    Logger.LogInfo($"Called {methodName}() for {boss.name}");
                            }
                            catch (Exception methodEx)
                            {
                                if (Config.PrintDebugOutput)
                                    Logger.LogWarning($"Failed to call {methodName}() for {boss.name}: {methodEx.Message}");
                            }
                        }
                    }
                }
                
                // Activar el GameObject si estaba desactivado
                if (!boss.activeInHierarchy)
                {
                    boss.SetActive(true);
                    activated = true;
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo($"Activated GameObject for {boss.name}");
                }
                
                return activated;
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error awakening boss {boss.name}: {ex.Message}");
                
                return false;
            }
        }
        
        /// <summary>
        /// Notifica al CombatSync sobre la activación de un jefe
        /// </summary>
        private void NotifyCombatSyncBossActivation(GameObject boss)
        {
            try
            {
                if (_combatSync == null) return;
                
                // Usar reflexión para llamar métodos del CombatSync si existen
                var combatSyncType = _combatSync.GetType();
                
                // Intentar notificar sobre activación de jefe
                var notifyMethod = combatSyncType.GetMethod("NotifyBossActivation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (notifyMethod != null)
                {
                    notifyMethod.Invoke(_combatSync, new object[] { boss });
                }
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Notified CombatSync about boss activation: {boss.name}");
            }
            catch (Exception ex)
            {
                if (Config.PrintDebugOutput)
                    Logger.LogError($"Error notifying CombatSync: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Estructura para eventos de audio
    /// </summary>
    [Serializable]
    public class AudioEvent
    {
        public string Id;
        public string SourceName;
        public string ClipName;
        public Vector3 Position;
        public float Volume;
        public float Pitch;
        public float Time;
        public string EventType;
        public float Timestamp;
        
        /// <summary>
        /// Resetea el objeto para reutilización en el pool
        /// </summary>
        public void Reset()
        {
            Id = null;
            SourceName = null;
            ClipName = null;
            Position = Vector3.zero;
            Volume = 0f;
            Pitch = 0f;
            Time = 0f;
            EventType = null;
            Timestamp = 0f;
        }
    }
    
    /// <summary>
    /// Componente para detectar eventos de audio
    /// </summary>
    internal class AudioEventDetector : MonoBehaviour
    {
        private AudioSync _audioSync;
        private string _sourceId;
        private AudioSource _audioSource;
        private bool _wasPlaying = false;
        
        public void Initialize(AudioSync audioSync, string sourceId)
        {
            _audioSync = audioSync;
            _sourceId = sourceId;
            _audioSource = GetComponent<AudioSource>();
        }
        
        private void Update()
        {
            if (_audioSource == null || _audioSync == null) return;
            
            // Detectar cuando empieza a reproducirse audio
            if (_audioSource.isPlaying && !_wasPlaying && _audioSource.clip != null)
            {
                _audioSync.RegisterAudioEvent(
                    _sourceId,
                    _audioSource.clip,
                    transform.position,
                    _audioSource.volume,
                    _audioSource.pitch
                );
            }
            
            _wasPlaying = _audioSource.isPlaying;
        }
    }
}