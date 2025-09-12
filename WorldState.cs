using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilklessCoop
{
    /// <summary>
    /// Estado completo del mundo que el HOST envía a los CLIENTES
    /// Incluye toda la información necesaria para sincronización autoritativa
    /// </summary>
    [Serializable]
    public class WorldState
    {
        public List<NetworkEnemyData> Enemies;
        public List<NetworkPlayerData> Players;
        public List<NetworkCombatEvent> CombatEvents;
        public List<NetworkBossData> Bosses;
        public List<NetworkWorldObjectData> WorldObjects;
        public NetworkGameProgressData GameProgress;
        public NetworkUIData UIData;
        public float Timestamp;
        public string HostId;
        public int SequenceNumber;
        
        public WorldState()
        {
            Enemies = new List<NetworkEnemyData>();
            Players = new List<NetworkPlayerData>();
            CombatEvents = new List<NetworkCombatEvent>();
            Bosses = new List<NetworkBossData>();
            WorldObjects = new List<NetworkWorldObjectData>();
            GameProgress = new NetworkGameProgressData();
            UIData = new NetworkUIData();
            Timestamp = Time.time;
            HostId = "";
            SequenceNumber = 0;
        }
        
        /// <summary>
        /// Crea un WorldState compacto solo con datos esenciales para optimizar ancho de banda
        /// </summary>
        public WorldState CreateCompactState()
        {
            var compact = new WorldState
            {
                Timestamp = this.Timestamp,
                HostId = this.HostId,
                SequenceNumber = this.SequenceNumber
            };
            
            // Solo incluir jugadores activos
            foreach (var player in Players)
            {
                if (player.IsActive)
                    compact.Players.Add(player);
            }
            
            // Solo incluir enemigos activos o recientemente dañados
            foreach (var enemy in Enemies)
            {
                if (enemy.IsActive || enemy.WasDamaged)
                    compact.Enemies.Add(enemy);
            }
            
            // Solo incluir eventos de combate recientes
            foreach (var combatEvent in CombatEvents)
            {
                if (Time.time - combatEvent.Timestamp < 1.0f)
                    compact.CombatEvents.Add(combatEvent);
            }
            
            return compact;
        }
    }
    
    /// <summary>
    /// Datos de enemigo para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkEnemyData
    {
        public string Id;
        public string Name;
        public Vector3 Position;
        public bool IsActive;
        public int Health;
        public int MaxHealth;
        public bool WasDamaged;
        public float LastDamageTime;
        
        public NetworkEnemyData() 
        {
            Id = "";
            Name = "";
            Position = Vector3.zero;
            IsActive = false;
            Health = 0;
            MaxHealth = 0;
            WasDamaged = false;
            LastDamageTime = 0f;
        }
        
        public NetworkEnemyData(EnemyData enemy)
        {
            Id = enemy.Id;
            Name = enemy.Name;
            Position = enemy.Position;
            IsActive = enemy.IsActive;
            Health = enemy.Health;
            MaxHealth = enemy.Health; // TODO: Obtener vida máxima real
            WasDamaged = false;
            LastDamageTime = 0f;
        }
    }
    
    /// <summary>
    /// Datos de jugador para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkPlayerData
    {
        public string Id;
        public string Name;
        public Vector3 Position;
        public int Health;
        public int MaxHealth;
        public bool IsActive;
        public CharacterType Type;
        public bool IsHost;
        
        public NetworkPlayerData() 
        {
            Id = "";
            Name = "";
            Position = Vector3.zero;
            Health = 0;
            MaxHealth = 0;
            IsActive = false;
            Type = CharacterType.Hornet;
            IsHost = false;
        }
        
        public NetworkPlayerData(PlayerCharacter player)
        {
            Id = player.Id;
            Name = player.Name;
            Position = player.LastPosition;
            Health = player.LastHealth;
            MaxHealth = 100; // TODO: Obtener vida máxima real
            IsActive = player.IsActive;
            Type = player.Type;
            IsHost = player.IsHost;
        }
    }
    
    /// <summary>
    /// Evento de combate para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkCombatEvent
    {
        public string SourceId;
        public string TargetId;
        public int Damage;
        public float Timestamp;
        public Vector3 Position;
        
        public NetworkCombatEvent() 
        {
            SourceId = "";
            TargetId = "";
            Damage = 0;
            Timestamp = 0f;
            Position = Vector3.zero;
        }
        
        public NetworkCombatEvent(DamageEvent damageEvent)
        {
            SourceId = damageEvent.SourceId;
            TargetId = damageEvent.TargetId;
            Damage = damageEvent.Damage;
            Timestamp = damageEvent.Timestamp;
            Position = Vector3.zero; // TODO: Añadir posición si es necesaria
        }
    }
    
    /// <summary>
    /// Datos de jefe para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkBossData
    {
        public string Id;
        public string Name;
        public Vector3 Position;
        public bool IsActive;
        public bool IsInCombat;
        public int Health;
        public int MaxHealth;
        public float LastDamageTime;
        
        public NetworkBossData()
        {
            Id = "";
            Name = "";
            Position = Vector3.zero;
            IsActive = false;
            IsInCombat = false;
            Health = 0;
            MaxHealth = 0;
            LastDamageTime = 0f;
        }
        
        public NetworkBossData(BossData boss)
        {
            Id = boss.Id;
            Name = boss.Name;
            Position = boss.Position;
            IsActive = boss.IsActive;
            IsInCombat = boss.IsInCombat;
            Health = boss.Health;
            MaxHealth = boss.MaxHealth;
            LastDamageTime = boss.LastAttackTime;
        }
    }
    
    /// <summary>
    /// Datos de objetos del mundo para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkWorldObjectData
    {
        public string Id;
        public string Name;
        public Vector3 Position;
        public bool IsActive;
        public bool IsDestroyed;
        public bool IsInteractable;
        public float LastInteractionTime;
        
        public NetworkWorldObjectData()
        {
            Id = "";
            Name = "";
            Position = Vector3.zero;
            IsActive = false;
            IsDestroyed = false;
            IsInteractable = true;
            LastInteractionTime = 0f;
        }
    }
    
    /// <summary>
    /// Datos de progreso del juego para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkGameProgressData
    {
        public Dictionary<string, object> ProgressData;
        public string CurrentScene;
        public float GameTime;
        public int Geo;
        public List<string> UnlockedAbilities;
        public List<string> CollectedItems;
        public Dictionary<string, bool> OpenedDoors;
        
        public NetworkGameProgressData()
        {
            ProgressData = new Dictionary<string, object>();
            CurrentScene = "";
            GameTime = 0f;
            Geo = 0;
            UnlockedAbilities = new List<string>();
            CollectedItems = new List<string>();
            OpenedDoors = new Dictionary<string, bool>();
        }
    }
    
    /// <summary>
    /// Datos de UI para sincronización de red (incluyendo nombres de usuario optimizados)
    /// </summary>
    [Serializable]
    public class NetworkUIData
    {
        public Dictionary<string, NetworkPlayerNameData> PlayerNames;
        public Dictionary<string, Vector3> CompassPositions;
        public bool ShowPlayerNames;
        public bool ShowCompass;
        public float UIUpdateTimestamp;
        
        public NetworkUIData()
        {
            PlayerNames = new Dictionary<string, NetworkPlayerNameData>();
            CompassPositions = new Dictionary<string, Vector3>();
            ShowPlayerNames = true;
            ShowCompass = true;
            UIUpdateTimestamp = 0f;
        }
    }
    
    /// <summary>
    /// Datos optimizados de nombres de usuario para sincronización de red
    /// </summary>
    [Serializable]
    public class NetworkPlayerNameData
    {
        public string PlayerId;
        public string DisplayName;
        public Color NameColor;
        public Vector3 WorldPosition;
        public bool IsVisible;
        public float LastUpdateTime;
        public bool IsSpecialUser;
        
        public NetworkPlayerNameData()
        {
            PlayerId = "";
            DisplayName = "";
            NameColor = Color.white;
            WorldPosition = Vector3.zero;
            IsVisible = true;
            LastUpdateTime = 0f;
            IsSpecialUser = false;
        }
        
        public NetworkPlayerNameData(string playerId, string displayName, Color color, Vector3 position)
        {
            PlayerId = playerId;
            DisplayName = displayName;
            NameColor = color;
            WorldPosition = position;
            IsVisible = true;
            LastUpdateTime = Time.time;
            IsSpecialUser = false;
        }
    }
    
    /// <summary>
    /// Input del jugador que el CLIENTE envía al HOST
    /// </summary>
    [Serializable]
    public class PlayerInput
    {
        public string PlayerId;
        public Vector3 Position;
        public Vector3 Velocity;
        public List<string> Actions;
        public float Timestamp;
        public int SequenceNumber;
        
        public PlayerInput()
        {
            PlayerId = "";
            Position = Vector3.zero;
            Velocity = Vector3.zero;
            Actions = new List<string>();
            Timestamp = Time.time;
            SequenceNumber = 0;
        }
        
        public PlayerInput(string playerId, Vector3 position, Vector3 velocity)
        {
            PlayerId = playerId;
            Position = position;
            Velocity = velocity;
            Actions = new List<string>();
            Timestamp = Time.time;
            SequenceNumber = 0;
        }
    }
    
    /// <summary>
    /// Utilidades para serialización y compresión de WorldState
    /// </summary>
    public static class WorldStateSerializer
    {
        /// <summary>
        /// Serializa un WorldState a string JSON comprimido
        /// </summary>
        public static string SerializeWorldState(WorldState worldState)
        {
            try
            {
                return JsonUtility.ToJson(worldState);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error serializing WorldState: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Deserializa un string JSON a WorldState
        /// </summary>
        public static WorldState DeserializeWorldState(string jsonData)
        {
            try
            {
                return JsonUtility.FromJson<WorldState>(jsonData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing WorldState: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Serializa PlayerInput a string JSON
        /// </summary>
        public static string SerializePlayerInput(PlayerInput playerInput)
        {
            try
            {
                return JsonUtility.ToJson(playerInput);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error serializing PlayerInput: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Deserializa un string JSON a PlayerInput
        /// </summary>
        public static PlayerInput DeserializePlayerInput(string jsonData)
        {
            try
            {
                return JsonUtility.FromJson<PlayerInput>(jsonData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing PlayerInput: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Calcula el tamaño aproximado en bytes de un WorldState serializado
        /// </summary>
        public static int EstimateWorldStateSize(WorldState worldState)
        {
            string serialized = SerializeWorldState(worldState);
            return serialized?.Length ?? 0;
        }
    }
    
    /// <summary>
    /// Configuración para la sincronización del estado del mundo optimizada
    /// </summary>
    public static class WorldStateConfig
    {
        /// <summary>
        /// Frecuencia de envío del estado del mundo (Hz) - Optimizada para rendimiento
        /// </summary>
        public const float WORLD_STATE_FREQUENCY = 10f; // Optimizado para cooperativo
        
        /// <summary>
        /// Frecuencia de envío del input del jugador (Hz) - Optimizada para responsividad
        /// </summary>
        public const float PLAYER_INPUT_FREQUENCY = 20f; // Más responsivo para input
        
        /// <summary>
        /// Tiempo máximo para considerar un estado como válido (segundos)
        /// </summary>
        public const float MAX_STATE_AGE = 1f;
        
        /// <summary>
        /// Número máximo de eventos de combate por estado
        /// </summary>
        public const int MAX_COMBAT_EVENTS_PER_STATE = 10;
        
        /// <summary>
        /// Distancia máxima para sincronizar enemigos (unidades)
        /// </summary>
        public const float MAX_ENEMY_SYNC_DISTANCE = 50f;
        
        /// <summary>
        /// Distancia mínima para activar interpolación de movimiento
        /// </summary>
        public const float MIN_INTERPOLATION_DISTANCE = 0.05f;
        
        /// <summary>
        /// Velocidad de interpolación para movimientos suaves
        /// </summary>
        public const float INTERPOLATION_SPEED = 0.4f;
        
        /// <summary>
        /// Distancia para snap instantáneo (evitar micro-movimientos)
        /// </summary>
        public const float SNAP_DISTANCE = 0.02f;
        
        /// <summary>
        /// Tamaño máximo del WorldState en caracteres antes de compresión
        /// </summary>
        public const int MAX_WORLD_STATE_SIZE = 8192;
        
        /// <summary>
        /// Número máximo de jugadores soportados
        /// </summary>
        public const int MAX_PLAYERS = 4;
    }
}