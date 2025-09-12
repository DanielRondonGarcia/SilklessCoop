// Optimized interpolation system for smooth player movement
// Mejora la fluidez del movimiento de jugadores remotos

using UnityEngine;
using System.Collections.Generic;

namespace SilksongMultiplayer
{
    internal class SmoothInterpolator
    {
        private struct PositionData
        {
            public Vector3 position;
            public float scale;
            public float timestamp;
            
            public PositionData(Vector3 pos, float scl, float time)
            {
                position = pos;
                scale = scl;
                timestamp = time;
            }
        }
        
        private Queue<PositionData> positionBuffer = new Queue<PositionData>();
        private const int MAX_BUFFER_SIZE = 10;
        private const float INTERPOLATION_DELAY = 0.1f; // 100ms delay for smooth interpolation
        private const float MAX_DISTANCE_THRESHOLD = 5.0f; // Teleport if too far
        
        private Vector3 currentPosition;
        private float currentScale;
        private bool isInitialized = false;
        
        public void AddPosition(Vector3 position, float scale)
        {
            float currentTime = Time.time;
            
            // Initialize if this is the first position
            if (!isInitialized)
            {
                currentPosition = position;
                currentScale = scale;
                isInitialized = true;
                return;
            }
            
            // Check if we should teleport (position changed too much)
            if (Vector3.Distance(currentPosition, position) > MAX_DISTANCE_THRESHOLD)
            {
                currentPosition = position;
                currentScale = scale;
                positionBuffer.Clear();
                return;
            }
            
            // Add to buffer
            positionBuffer.Enqueue(new PositionData(position, scale, currentTime));
            
            // Limit buffer size
            while (positionBuffer.Count > MAX_BUFFER_SIZE)
            {
                positionBuffer.Dequeue();
            }
        }
        
        public Vector3 GetInterpolatedPosition()
        {
            if (!isInitialized)
                return Vector3.zero;
                
            if (positionBuffer.Count == 0)
                return currentPosition;
            
            float targetTime = Time.time - INTERPOLATION_DELAY;
            
            // Find the two positions to interpolate between
            PositionData? before = null;
            PositionData? after = null;
            
            foreach (var data in positionBuffer)
            {
                if (data.timestamp <= targetTime)
                {
                    before = data;
                }
                else
                {
                    after = data;
                    break;
                }
            }
            
            // If we have both positions, interpolate
            if (before.HasValue && after.HasValue)
            {
                float timeDiff = after.Value.timestamp - before.Value.timestamp;
                if (timeDiff > 0)
                {
                    float t = (targetTime - before.Value.timestamp) / timeDiff;
                    t = Mathf.Clamp01(t);
                    
                    currentPosition = Vector3.Lerp(before.Value.position, after.Value.position, t);
                    currentScale = Mathf.Lerp(before.Value.scale, after.Value.scale, t);
                }
            }
            // If we only have one position, use it
            else if (before.HasValue)
            {
                currentPosition = before.Value.position;
                currentScale = before.Value.scale;
            }
            
            return currentPosition;
        }
        
        public float GetInterpolatedScale()
        {
            return currentScale;
        }
        
        public void Reset()
        {
            positionBuffer.Clear();
            isInitialized = false;
        }
    }
}