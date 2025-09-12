// Network message pooling system for optimized serialization
// Sistema de pooling de mensajes de red para serialización optimizada

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SilksongMultiplayer
{
    /// <summary>
    /// Object pool for network messages to reduce garbage collection
    /// </summary>
    public static class NetworkMessagePool
    {
        // Message pools for different types
        private static readonly Queue<PositionMessage> positionPool = new Queue<PositionMessage>();
        private static readonly Queue<AnimationMessage> animationPool = new Queue<AnimationMessage>();
        private static readonly Queue<MapChangeMessage> mapChangePool = new Queue<MapChangeMessage>();
        private static readonly Queue<DamageMessage> damagePool = new Queue<DamageMessage>();
        private static readonly Queue<AttackMessage> attackPool = new Queue<AttackMessage>();
        
        // StringBuilder pool for string operations
        private static readonly Queue<StringBuilder> stringBuilderPool = new Queue<StringBuilder>();
        
        // Byte array pools for different sizes
        private static readonly Queue<byte[]> smallByteArrayPool = new Queue<byte[]>(); // 64 bytes
        private static readonly Queue<byte[]> mediumByteArrayPool = new Queue<byte[]>(); // 256 bytes
        private static readonly Queue<byte[]> largeByteArrayPool = new Queue<byte[]>(); // 1024 bytes
        
        private const int MAX_POOL_SIZE = 50;
        private const int SMALL_ARRAY_SIZE = 64;
        private const int MEDIUM_ARRAY_SIZE = 256;
        private const int LARGE_ARRAY_SIZE = 1024;
        
        // Pool statistics
        private static int totalAllocations = 0;
        private static int poolHits = 0;
        
        #region Message Classes
        
        public class PositionMessage
        {
            public Vector3 position;
            public float scale;
            public ulong steamId;
            public float timestamp;
            
            public void Reset()
            {
                position = Vector3.zero;
                scale = 1f;
                steamId = 0;
                timestamp = 0f;
            }
        }
        
        public class AnimationMessage
        {
            public string animationName;
            public int extraValue;
            public ulong steamId;
            public float timestamp;
            
            public void Reset()
            {
                animationName = string.Empty;
                extraValue = 0;
                steamId = 0;
                timestamp = 0f;
            }
        }
        
        public class MapChangeMessage
        {
            public string mapName;
            public ulong steamId;
            public float timestamp;
            
            public void Reset()
            {
                mapName = string.Empty;
                steamId = 0;
                timestamp = 0f;
            }
        }
        
        public class DamageMessage
        {
            public ulong targetSteamId;
            public int damage;
            public int direction;
            public int hazardType;
            public int attackTypes;
            public float timestamp;
            
            public void Reset()
            {
                targetSteamId = 0;
                damage = 0;
                direction = 0;
                hazardType = 0;
                attackTypes = 0;
                timestamp = 0f;
            }
        }
        
        public class AttackMessage
        {
            public string parentName;
            public string name;
            public string animationName;
            public ulong steamId;
            public float timestamp;
            
            public void Reset()
            {
                parentName = string.Empty;
                name = string.Empty;
                animationName = string.Empty;
                steamId = 0;
                timestamp = 0f;
            }
        }
        
        #endregion
        
        #region Message Pool Methods
        
        public static PositionMessage GetPositionMessage()
        {
            totalAllocations++;
            if (positionPool.Count > 0)
            {
                poolHits++;
                var message = positionPool.Dequeue();
                message.Reset();
                return message;
            }
            return new PositionMessage();
        }
        
        public static void ReturnPositionMessage(PositionMessage message)
        {
            if (message != null && positionPool.Count < MAX_POOL_SIZE)
            {
                positionPool.Enqueue(message);
            }
        }
        
        public static AnimationMessage GetAnimationMessage()
        {
            totalAllocations++;
            if (animationPool.Count > 0)
            {
                poolHits++;
                var message = animationPool.Dequeue();
                message.Reset();
                return message;
            }
            return new AnimationMessage();
        }
        
        public static void ReturnAnimationMessage(AnimationMessage message)
        {
            if (message != null && animationPool.Count < MAX_POOL_SIZE)
            {
                animationPool.Enqueue(message);
            }
        }
        
        public static MapChangeMessage GetMapChangeMessage()
        {
            totalAllocations++;
            if (mapChangePool.Count > 0)
            {
                poolHits++;
                var message = mapChangePool.Dequeue();
                message.Reset();
                return message;
            }
            return new MapChangeMessage();
        }
        
        public static void ReturnMapChangeMessage(MapChangeMessage message)
        {
            if (message != null && mapChangePool.Count < MAX_POOL_SIZE)
            {
                mapChangePool.Enqueue(message);
            }
        }
        
        public static DamageMessage GetDamageMessage()
        {
            totalAllocations++;
            if (damagePool.Count > 0)
            {
                poolHits++;
                var message = damagePool.Dequeue();
                message.Reset();
                return message;
            }
            return new DamageMessage();
        }
        
        public static void ReturnDamageMessage(DamageMessage message)
        {
            if (message != null && damagePool.Count < MAX_POOL_SIZE)
            {
                damagePool.Enqueue(message);
            }
        }
        
        public static AttackMessage GetAttackMessage()
        {
            totalAllocations++;
            if (attackPool.Count > 0)
            {
                poolHits++;
                var message = attackPool.Dequeue();
                message.Reset();
                return message;
            }
            return new AttackMessage();
        }
        
        public static void ReturnAttackMessage(AttackMessage message)
        {
            if (message != null && attackPool.Count < MAX_POOL_SIZE)
            {
                attackPool.Enqueue(message);
            }
        }
        
        #endregion
        
        #region StringBuilder Pool
        
        public static StringBuilder GetStringBuilder()
        {
            totalAllocations++;
            if (stringBuilderPool.Count > 0)
            {
                poolHits++;
                var sb = stringBuilderPool.Dequeue();
                sb.Clear();
                return sb;
            }
            return new StringBuilder(256);
        }
        
        public static void ReturnStringBuilder(StringBuilder sb)
        {
            if (sb != null && stringBuilderPool.Count < MAX_POOL_SIZE)
            {
                if (sb.Capacity <= 1024) // Don't pool very large StringBuilders
                {
                    stringBuilderPool.Enqueue(sb);
                }
            }
        }
        
        #endregion
        
        #region Byte Array Pool
        
        public static byte[] GetByteArray(int minSize)
        {
            totalAllocations++;
            
            if (minSize <= SMALL_ARRAY_SIZE)
            {
                if (smallByteArrayPool.Count > 0)
                {
                    poolHits++;
                    return smallByteArrayPool.Dequeue();
                }
                return new byte[SMALL_ARRAY_SIZE];
            }
            else if (minSize <= MEDIUM_ARRAY_SIZE)
            {
                if (mediumByteArrayPool.Count > 0)
                {
                    poolHits++;
                    return mediumByteArrayPool.Dequeue();
                }
                return new byte[MEDIUM_ARRAY_SIZE];
            }
            else if (minSize <= LARGE_ARRAY_SIZE)
            {
                if (largeByteArrayPool.Count > 0)
                {
                    poolHits++;
                    return largeByteArrayPool.Dequeue();
                }
                return new byte[LARGE_ARRAY_SIZE];
            }
            
            // For very large arrays, don't pool them
            return new byte[minSize];
        }
        
        public static void ReturnByteArray(byte[] array)
        {
            if (array == null)
                return;
                
            if (array.Length == SMALL_ARRAY_SIZE && smallByteArrayPool.Count < MAX_POOL_SIZE)
            {
                Array.Clear(array, 0, array.Length);
                smallByteArrayPool.Enqueue(array);
            }
            else if (array.Length == MEDIUM_ARRAY_SIZE && mediumByteArrayPool.Count < MAX_POOL_SIZE)
            {
                Array.Clear(array, 0, array.Length);
                mediumByteArrayPool.Enqueue(array);
            }
            else if (array.Length == LARGE_ARRAY_SIZE && largeByteArrayPool.Count < MAX_POOL_SIZE)
            {
                Array.Clear(array, 0, array.Length);
                largeByteArrayPool.Enqueue(array);
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Get pool efficiency as a percentage
        /// </summary>
        public static float GetPoolEfficiency()
        {
            if (totalAllocations == 0)
                return 0f;
            return (float)poolHits / totalAllocations * 100f;
        }
        
        /// <summary>
        /// Get pool statistics
        /// </summary>
        public static string GetPoolStats()
        {
            return $"Pool Stats - Total: {totalAllocations}, Hits: {poolHits}, Efficiency: {GetPoolEfficiency():F1}%\n" +
                   $"Position: {positionPool.Count}, Animation: {animationPool.Count}, " +
                   $"MapChange: {mapChangePool.Count}, Damage: {damagePool.Count}, Attack: {attackPool.Count}\n" +
                   $"StringBuilder: {stringBuilderPool.Count}, " +
                   $"ByteArrays: S{smallByteArrayPool.Count}/M{mediumByteArrayPool.Count}/L{largeByteArrayPool.Count}";
        }
        
        /// <summary>
        /// Clear all pools (useful for cleanup)
        /// </summary>
        public static void ClearAllPools()
        {
            positionPool.Clear();
            animationPool.Clear();
            mapChangePool.Clear();
            damagePool.Clear();
            attackPool.Clear();
            stringBuilderPool.Clear();
            smallByteArrayPool.Clear();
            mediumByteArrayPool.Clear();
            largeByteArrayPool.Clear();
            
            totalAllocations = 0;
            poolHits = 0;
        }
        
        /// <summary>
        /// Warm up pools with initial objects
        /// </summary>
        public static void WarmUpPools()
        {
            // Pre-allocate some objects to avoid initial allocation spikes
            for (int i = 0; i < 10; i++)
            {
                ReturnPositionMessage(new PositionMessage());
                ReturnAnimationMessage(new AnimationMessage());
                ReturnMapChangeMessage(new MapChangeMessage());
                ReturnDamageMessage(new DamageMessage());
                ReturnAttackMessage(new AttackMessage());
                ReturnStringBuilder(new StringBuilder(256));
                ReturnByteArray(new byte[SMALL_ARRAY_SIZE]);
                ReturnByteArray(new byte[MEDIUM_ARRAY_SIZE]);
                ReturnByteArray(new byte[LARGE_ARRAY_SIZE]);
            }
        }
        
        #endregion
    }
}