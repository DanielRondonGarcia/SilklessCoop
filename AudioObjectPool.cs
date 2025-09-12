using System.Collections.Generic;
using UnityEngine;
using System;

namespace SilklessCoop
{
    /// <summary>
    /// Pool de objetos para eventos de audio para evitar allocaciones constantes
    /// </summary>
    internal class AudioObjectPool
    {
        private readonly Queue<AudioEvent> _audioEventPool = new Queue<AudioEvent>();
        private readonly Queue<AudioEventDetector> _detectorPool = new Queue<AudioEventDetector>();
        private readonly Dictionary<string, AudioSource> _cachedAudioSources = new Dictionary<string, AudioSource>();
        private readonly Dictionary<string, AudioClip> _cachedAudioClips = new Dictionary<string, AudioClip>();
        
        private const int INITIAL_POOL_SIZE = 50;
        private const int MAX_POOL_SIZE = 200;
        private const int MAX_CACHE_SIZE = 100;
        
        public AudioObjectPool()
        {
            // Pre-llenar el pool con objetos
            for (int i = 0; i < INITIAL_POOL_SIZE; i++)
            {
                _audioEventPool.Enqueue(new AudioEvent());
            }
        }
        
        /// <summary>
        /// Obtiene un AudioEvent del pool o crea uno nuevo si es necesario
        /// </summary>
        public AudioEvent GetAudioEvent()
        {
            if (_audioEventPool.Count > 0)
            {
                var audioEvent = _audioEventPool.Dequeue();
                audioEvent.Reset(); // Resetear el objeto para reutilización
                return audioEvent;
            }
            
            return new AudioEvent();
        }
        
        /// <summary>
        /// Devuelve un AudioEvent al pool para reutilización
        /// </summary>
        public void ReturnAudioEvent(AudioEvent audioEvent)
        {
            if (audioEvent != null && _audioEventPool.Count < MAX_POOL_SIZE)
            {
                audioEvent.Reset();
                _audioEventPool.Enqueue(audioEvent);
            }
        }
        
        /// <summary>
        /// Cachea un AudioSource para evitar búsquedas repetitivas
        /// </summary>
        public void CacheAudioSource(string key, AudioSource source)
        {
            if (_cachedAudioSources.Count < MAX_CACHE_SIZE && !_cachedAudioSources.ContainsKey(key))
            {
                _cachedAudioSources[key] = source;
            }
        }
        
        /// <summary>
        /// Obtiene un AudioSource cacheado
        /// </summary>
        public AudioSource GetCachedAudioSource(string key)
        {
            _cachedAudioSources.TryGetValue(key, out AudioSource source);
            return source;
        }
        
        /// <summary>
        /// Cachea un AudioClip para evitar búsquedas repetitivas
        /// </summary>
        public void CacheAudioClip(string key, AudioClip clip)
        {
            if (_cachedAudioClips.Count < MAX_CACHE_SIZE && !_cachedAudioClips.ContainsKey(key))
            {
                _cachedAudioClips[key] = clip;
            }
        }
        
        /// <summary>
        /// Obtiene un AudioClip cacheado
        /// </summary>
        public AudioClip GetCachedAudioClip(string key)
        {
            _cachedAudioClips.TryGetValue(key, out AudioClip clip);
            return clip;
        }
        
        /// <summary>
        /// Limpia el cache de objetos destruidos
        /// </summary>
        public void CleanupCache()
        {
            // Limpiar AudioSources destruidos
            var keysToRemove = new List<string>();
            foreach (var kvp in _cachedAudioSources)
            {
                if (kvp.Value == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _cachedAudioSources.Remove(key);
            }
            
            // Limpiar AudioClips destruidos
            keysToRemove.Clear();
            foreach (var kvp in _cachedAudioClips)
            {
                if (kvp.Value == null)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _cachedAudioClips.Remove(key);
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas del pool
        /// </summary>
        public string GetPoolStats()
        {
            return $"AudioEvent Pool: {_audioEventPool.Count}/{MAX_POOL_SIZE}, " +
                   $"Cached Sources: {_cachedAudioSources.Count}/{MAX_CACHE_SIZE}, " +
                   $"Cached Clips: {_cachedAudioClips.Count}/{MAX_CACHE_SIZE}";
        }
    }
}