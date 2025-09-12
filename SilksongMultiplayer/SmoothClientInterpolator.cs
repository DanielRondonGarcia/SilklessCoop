// Sistema de interpolación suave para clientes en arquitectura Host-Cliente
// Proporciona movimientos fluidos y predicción de posición para mejor experiencia cooperativa

using System;
using System.Collections.Generic;
using UnityEngine;

namespace SilksongMultiplayer
{
    /// <summary>
    /// Sistema de interpolación suave para clientes que reciben estado del HOST
    /// Proporciona movimientos fluidos y predicción de posición
    /// </summary>
    internal class SmoothClientInterpolator : MonoBehaviour
    {
        // Configuración de interpolación
        private const float INTERPOLATION_SPEED = 8f;
        private const float PREDICTION_TIME = 0.1f;
        private const float SNAP_DISTANCE = 2f;
        private const float MAX_PREDICTION_DISTANCE = 5f;
        
        // Estado de interpolación
        private Vector3 targetPosition;
        private Vector3 targetVelocity;
        private Vector3 lastReceivedPosition;
        private Vector3 lastReceivedVelocity;
        private float lastUpdateTime;
        private bool hasValidTarget = false;
        
        // Predicción de movimiento
        private Vector3 predictedPosition;
        private float predictionConfidence = 1f;
        
        // Referencias
        private Transform cachedTransform;
        private Rigidbody2D cachedRigidbody;
        
        // Estadísticas de rendimiento
        private float averageLatency = 0f;
        private int updateCount = 0;
        private float totalLatency = 0f;
        
        private void Start()
        {
            cachedTransform = transform;
            cachedRigidbody = GetComponent<Rigidbody2D>();
            
            targetPosition = cachedTransform.position;
            lastReceivedPosition = targetPosition;
        }
        
        private void Update()
        {
            if (!hasValidTarget) return;
            
            // Calcular posición interpolada con predicción
            Vector3 interpolatedPosition = CalculateInterpolatedPosition();
            
            // Aplicar interpolación suave
            ApplySmoothInterpolation(interpolatedPosition);
            
            // Actualizar predicción
            UpdatePrediction();
        }
        
        /// <summary>
        /// Actualiza la posición objetivo desde el estado del HOST
        /// </summary>
        public void UpdateTarget(Vector3 newPosition, Vector3 newVelocity, float timestamp)
        {
            // Calcular latencia
            float currentTime = Time.time;
            float latency = currentTime - timestamp;
            
            // Actualizar estadísticas de latencia
            totalLatency += latency;
            updateCount++;
            averageLatency = totalLatency / updateCount;
            
            // Guardar estado anterior
            lastReceivedPosition = targetPosition;
            lastReceivedVelocity = targetVelocity;
            
            // Actualizar objetivo
            targetPosition = newPosition;
            targetVelocity = newVelocity;
            lastUpdateTime = currentTime;
            hasValidTarget = true;
            
            // Compensar latencia con predicción
            if (latency > 0 && newVelocity.magnitude > 0.1f)
            {
                Vector3 latencyCompensation = newVelocity * latency;
                targetPosition += latencyCompensation;
            }
            
            // Verificar si necesitamos snap instantáneo
            float distance = Vector3.Distance(cachedTransform.position, targetPosition);
            if (distance > SNAP_DISTANCE)
            {
                // Snap instantáneo para distancias grandes
                cachedTransform.position = targetPosition;
                if (cachedRigidbody != null)
                {
                    cachedRigidbody.linearVelocity = newVelocity;
                }
            }
        }
        
        /// <summary>
        /// Calcula la posición interpolada con predicción
        /// </summary>
        private Vector3 CalculateInterpolatedPosition()
        {
            float timeSinceUpdate = Time.time - lastUpdateTime;
            
            // Predicción basada en velocidad
            Vector3 predictedTarget = targetPosition;
            if (targetVelocity.magnitude > 0.1f && timeSinceUpdate < PREDICTION_TIME)
            {
                Vector3 prediction = targetVelocity * timeSinceUpdate;
                
                // Limitar predicción para evitar errores grandes
                if (prediction.magnitude <= MAX_PREDICTION_DISTANCE)
                {
                    predictedTarget += prediction;
                    predictionConfidence = Mathf.Lerp(1f, 0.5f, timeSinceUpdate / PREDICTION_TIME);
                }
            }
            else
            {
                predictionConfidence = Mathf.Max(0.1f, predictionConfidence - Time.deltaTime * 2f);
            }
            
            return predictedTarget;
        }
        
        /// <summary>
        /// Aplica interpolación suave hacia la posición objetivo
        /// </summary>
        private void ApplySmoothInterpolation(Vector3 targetPos)
        {
            Vector3 currentPosition = cachedTransform.position;
            float distance = Vector3.Distance(currentPosition, targetPos);
            
            // Ajustar velocidad de interpolación basada en distancia y confianza de predicción
            float adaptiveSpeed = INTERPOLATION_SPEED * predictionConfidence;
            
            // Interpolación más rápida para distancias pequeñas
            if (distance < 0.5f)
            {
                adaptiveSpeed *= 1.5f;
            }
            
            // Aplicar interpolación
            Vector3 newPosition = Vector3.Lerp(currentPosition, targetPos, adaptiveSpeed * Time.deltaTime);
            cachedTransform.position = newPosition;
            
            // Actualizar velocidad del Rigidbody si está disponible
            if (cachedRigidbody != null && targetVelocity.magnitude > 0.1f)
            {
                Vector3 interpolatedVelocity = Vector3.Lerp(cachedRigidbody.linearVelocity, targetVelocity, adaptiveSpeed * Time.deltaTime * 0.5f);
                cachedRigidbody.linearVelocity = interpolatedVelocity;
            }
        }
        
        /// <summary>
        /// Actualiza la predicción de movimiento
        /// </summary>
        private void UpdatePrediction()
        {
            if (targetVelocity.magnitude > 0.1f)
            {
                predictedPosition = targetPosition + (targetVelocity * PREDICTION_TIME);
            }
            else
            {
                predictedPosition = targetPosition;
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de rendimiento de la interpolación
        /// </summary>
        public InterpolationStats GetStats()
        {
            return new InterpolationStats
            {
                AverageLatency = averageLatency,
                PredictionConfidence = predictionConfidence,
                UpdateCount = updateCount,
                HasValidTarget = hasValidTarget,
                CurrentDistance = hasValidTarget ? Vector3.Distance(cachedTransform.position, targetPosition) : 0f
            };
        }
        
        /// <summary>
        /// Reinicia el sistema de interpolación
        /// </summary>
        public void Reset()
        {
            hasValidTarget = false;
            targetPosition = cachedTransform.position;
            targetVelocity = Vector3.zero;
            lastReceivedPosition = targetPosition;
            lastReceivedVelocity = Vector3.zero;
            predictedPosition = targetPosition;
            predictionConfidence = 1f;
            
            // Reiniciar estadísticas
            averageLatency = 0f;
            updateCount = 0;
            totalLatency = 0f;
        }
        
        /// <summary>
        /// Configura parámetros de interpolación personalizados
        /// </summary>
        public void ConfigureInterpolation(float interpolationSpeed, float predictionTime, float snapDistance)
        {
            // Nota: En esta implementación, usamos constantes para mejor rendimiento
            // En una versión más avanzada, estos podrían ser variables configurables
        }
        
        private void OnDrawGizmos()
        {
            if (!hasValidTarget) return;
            
            // Dibujar posición objetivo
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.2f);
            
            // Dibujar predicción
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(predictedPosition, 0.15f);
            
            // Dibujar línea de velocidad
            if (targetVelocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(targetPosition, targetPosition + targetVelocity);
            }
        }
    }
    
    /// <summary>
    /// Estadísticas de rendimiento del sistema de interpolación
    /// </summary>
    public struct InterpolationStats
    {
        public float AverageLatency;
        public float PredictionConfidence;
        public int UpdateCount;
        public bool HasValidTarget;
        public float CurrentDistance;
    }
    
    /// <summary>
    /// Manager para múltiples interpoladores de clientes
    /// </summary>
    internal class ClientInterpolationManager : MonoBehaviour
    {
        private Dictionary<string, SmoothClientInterpolator> interpolators = new Dictionary<string, SmoothClientInterpolator>();
        private Dictionary<string, GameObject> clientObjects = new Dictionary<string, GameObject>();
        
        /// <summary>
        /// Crea o actualiza un interpolador para un cliente específico
        /// </summary>
        public void UpdateClientInterpolator(string clientId, Vector3 position, Vector3 velocity, float timestamp)
        {
            if (!interpolators.ContainsKey(clientId))
            {
                CreateClientInterpolator(clientId, position);
            }
            
            interpolators[clientId].UpdateTarget(position, velocity, timestamp);
        }
        
        /// <summary>
        /// Crea un nuevo interpolador para un cliente
        /// </summary>
        private void CreateClientInterpolator(string clientId, Vector3 initialPosition)
        {
            GameObject clientObject = new GameObject($"Client_{clientId}");
            clientObject.transform.position = initialPosition;
            
            SmoothClientInterpolator interpolator = clientObject.AddComponent<SmoothClientInterpolator>();
            
            interpolators[clientId] = interpolator;
            clientObjects[clientId] = clientObject;
        }
        
        /// <summary>
        /// Elimina un interpolador de cliente
        /// </summary>
        public void RemoveClientInterpolator(string clientId)
        {
            if (interpolators.ContainsKey(clientId))
            {
                if (clientObjects[clientId] != null)
                {
                    Destroy(clientObjects[clientId]);
                }
                
                interpolators.Remove(clientId);
                clientObjects.Remove(clientId);
            }
        }
        
        /// <summary>
        /// Obtiene estadísticas de todos los interpoladores
        /// </summary>
        public Dictionary<string, InterpolationStats> GetAllStats()
        {
            var stats = new Dictionary<string, InterpolationStats>();
            
            foreach (var kvp in interpolators)
            {
                stats[kvp.Key] = kvp.Value.GetStats();
            }
            
            return stats;
        }
        
        /// <summary>
        /// Reinicia todos los interpoladores
        /// </summary>
        public void ResetAll()
        {
            foreach (var interpolator in interpolators.Values)
            {
                interpolator.Reset();
            }
        }
    }
}