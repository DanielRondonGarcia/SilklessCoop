// Movement prediction system to reduce visual lag
// Sistema de predicción de movimiento para reducir el lag visual

using System.Collections.Generic;
using UnityEngine;

namespace SilksongMultiplayer
{
    public class MovementPredictor
    {
        private struct MovementSample
        {
            public Vector3 position;
            public Vector3 velocity;
            public float timestamp;
            public float scale;
        }
        
        private Queue<MovementSample> movementHistory;
        private const int MAX_HISTORY_SIZE = 10;
        private const float PREDICTION_TIME = 0.1f; // Predict 100ms ahead
        private const float MAX_PREDICTION_DISTANCE = 5.0f; // Max prediction distance
        
        // Velocity smoothing
        private Vector3 smoothedVelocity;
        private const float VELOCITY_SMOOTHING = 0.8f;
        
        // Acceleration tracking
        private Vector3 lastVelocity;
        private Vector3 acceleration;
        
        public MovementPredictor()
        {
            movementHistory = new Queue<MovementSample>();
            smoothedVelocity = Vector3.zero;
            lastVelocity = Vector3.zero;
            acceleration = Vector3.zero;
        }
        
        /// <summary>
        /// Add a new position sample to the movement history
        /// </summary>
        public void AddSample(Vector3 position, float scale, float timestamp)
        {
            // Calculate velocity from previous sample
            Vector3 velocity = Vector3.zero;
            if (movementHistory.Count > 0)
            {
                MovementSample lastSample = GetLatestSample();
                float deltaTime = timestamp - lastSample.timestamp;
                if (deltaTime > 0)
                {
                    velocity = (position - lastSample.position) / deltaTime;
                }
            }
            
            // Smooth velocity to reduce jitter
            smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - VELOCITY_SMOOTHING);
            
            // Calculate acceleration
            acceleration = (smoothedVelocity - lastVelocity) / Time.fixedDeltaTime;
            lastVelocity = smoothedVelocity;
            
            // Add new sample
            MovementSample newSample = new MovementSample
            {
                position = position,
                velocity = smoothedVelocity,
                timestamp = timestamp,
                scale = scale
            };
            
            movementHistory.Enqueue(newSample);
            
            // Maintain history size
            while (movementHistory.Count > MAX_HISTORY_SIZE)
            {
                movementHistory.Dequeue();
            }
        }
        
        /// <summary>
        /// Predict the position at a future time
        /// </summary>
        public Vector3 PredictPosition(float futureTime)
        {
            if (movementHistory.Count == 0)
                return Vector3.zero;
                
            MovementSample latestSample = GetLatestSample();
            float deltaTime = futureTime - latestSample.timestamp;
            
            // Don't predict too far into the future
            deltaTime = Mathf.Clamp(deltaTime, 0f, PREDICTION_TIME);
            
            // Use kinematic equation: position = initial_position + velocity * time + 0.5 * acceleration * time^2
            Vector3 predictedPosition = latestSample.position + 
                                      latestSample.velocity * deltaTime + 
                                      0.5f * acceleration * deltaTime * deltaTime;
            
            // Limit prediction distance to prevent wild predictions
            Vector3 predictionOffset = predictedPosition - latestSample.position;
            if (predictionOffset.magnitude > MAX_PREDICTION_DISTANCE)
            {
                predictionOffset = predictionOffset.normalized * MAX_PREDICTION_DISTANCE;
                predictedPosition = latestSample.position + predictionOffset;
            }
            
            return predictedPosition;
        }
        
        /// <summary>
        /// Get the current predicted position based on current time
        /// </summary>
        public Vector3 GetCurrentPredictedPosition()
        {
            return PredictPosition(Time.time);
        }
        
        /// <summary>
        /// Get interpolated position between two timestamps
        /// </summary>
        public Vector3 GetInterpolatedPosition(float targetTime)
        {
            if (movementHistory.Count < 2)
            {
                return movementHistory.Count > 0 ? GetLatestSample().position : Vector3.zero;
            }
            
            // Find the two samples that bracket the target time
            MovementSample[] samples = movementHistory.ToArray();
            
            for (int i = 0; i < samples.Length - 1; i++)
            {
                if (targetTime >= samples[i].timestamp && targetTime <= samples[i + 1].timestamp)
                {
                    // Interpolate between these two samples
                    float t = (targetTime - samples[i].timestamp) / (samples[i + 1].timestamp - samples[i].timestamp);
                    return Vector3.Lerp(samples[i].position, samples[i + 1].position, t);
                }
            }
            
            // If target time is beyond our samples, use prediction
            return PredictPosition(targetTime);
        }
        
        /// <summary>
        /// Get the current velocity
        /// </summary>
        public Vector3 GetCurrentVelocity()
        {
            return smoothedVelocity;
        }
        
        /// <summary>
        /// Get the current acceleration
        /// </summary>
        public Vector3 GetCurrentAcceleration()
        {
            return acceleration;
        }
        
        /// <summary>
        /// Check if the player is moving
        /// </summary>
        public bool IsMoving(float threshold = 0.1f)
        {
            return smoothedVelocity.magnitude > threshold;
        }
        
        /// <summary>
        /// Get movement confidence based on velocity consistency
        /// </summary>
        public float GetMovementConfidence()
        {
            if (movementHistory.Count < 3)
                return 0f;
                
            // Calculate velocity variance
            Vector3 avgVelocity = Vector3.zero;
            MovementSample[] samples = movementHistory.ToArray();
            
            for (int i = 1; i < samples.Length; i++)
            {
                avgVelocity += samples[i].velocity;
            }
            avgVelocity /= (samples.Length - 1);
            
            float variance = 0f;
            for (int i = 1; i < samples.Length; i++)
            {
                variance += (samples[i].velocity - avgVelocity).sqrMagnitude;
            }
            variance /= (samples.Length - 1);
            
            // Convert variance to confidence (lower variance = higher confidence)
            return Mathf.Clamp01(1f / (1f + variance));
        }
        
        /// <summary>
        /// Reset the predictor state
        /// </summary>
        public void Reset()
        {
            movementHistory.Clear();
            smoothedVelocity = Vector3.zero;
            lastVelocity = Vector3.zero;
            acceleration = Vector3.zero;
        }
        
        /// <summary>
        /// Get the latest movement sample
        /// </summary>
        private MovementSample GetLatestSample()
        {
            MovementSample[] samples = movementHistory.ToArray();
            return samples[samples.Length - 1];
        }
        
        /// <summary>
        /// Get predicted scale at future time
        /// </summary>
        public float PredictScale(float futureTime)
        {
            if (movementHistory.Count == 0)
                return 1f;
                
            // For scale, we just return the latest scale as it doesn't change smoothly like position
            return GetLatestSample().scale;
        }
        
        /// <summary>
        /// Get debug information about the predictor state
        /// </summary>
        public string GetDebugInfo()
        {
            return $"Samples: {movementHistory.Count}, Velocity: {smoothedVelocity.magnitude:F2}, " +
                   $"Acceleration: {acceleration.magnitude:F2}, Confidence: {GetMovementConfidence():F2}";
        }
    }
}