// Optimized PlayerAvatar with smooth interpolation and performance improvements
// Versión optimizada de PlayerAvatar con interpolación suave y mejoras de rendimiento

using UnityEngine;
using Steamworks;
using System.Collections;

namespace SilksongMultiplayer
{
    internal class OptimizedPlayerAvatar : MonoBehaviour
    {
        private SmoothInterpolator interpolator = new SmoothInterpolator();
        private MovementPredictor predictor;
        private tk2dSpriteAnimator spriteAnimator;
        private tk2dSprite sprite;
        private CSteamID playerID;
        private string currentMap = "";
        private bool isVisible = true;
        
        // Performance optimization: cache components
        private Transform cachedTransform;
        private GameObject cachedGameObject;
        
        // Position change detection
        private Vector3 lastNetworkPosition;
        private float lastNetworkScale;
        private const float POSITION_THRESHOLD = 0.01f;
        private const float SCALE_THRESHOLD = 0.01f;
        
        // Prediction settings
        private bool usePrediction = true;
        private float predictionStrength = 1.0f;
        
        // Update frequency control
        private float lastUpdateTime;
        private const float UPDATE_INTERVAL = 0.016f; // ~60 FPS
        
        private void Awake()
        {
            // Cache components for better performance
            cachedTransform = transform;
            cachedGameObject = gameObject;
            
            // Initialize predictor
            predictor = new MovementPredictor();
            
            // Get sprite components
            spriteAnimator = GetComponent<tk2dSpriteAnimator>();
            sprite = GetComponent<tk2dSprite>();
        }
        
        public void Initialize(CSteamID steamID, Vector3 initialPosition)
        {
            playerID = steamID;
            cachedTransform.position = initialPosition;
            lastNetworkPosition = initialPosition;
            interpolator.AddPosition(initialPosition, 1.0f);
        }
        
        public void UpdatePosition(Vector3 networkPosition, float networkScale)
        {
            // Only update if position or scale changed significantly
            if (Vector3.Distance(networkPosition, lastNetworkPosition) > POSITION_THRESHOLD ||
                Mathf.Abs(networkScale - lastNetworkScale) > SCALE_THRESHOLD)
            {
                interpolator.AddPosition(networkPosition, networkScale);
                predictor.AddSample(networkPosition, networkScale, Time.time);
                lastNetworkPosition = networkPosition;
                lastNetworkScale = networkScale;
            }
        }
        
        private void Update()
        {
            // Limit update frequency for better performance
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL)
                return;
                
            lastUpdateTime = Time.time;
            
            // Apply interpolated position
            Vector3 interpolatedPos = interpolator.GetInterpolatedPosition();
            float interpolatedScale = interpolator.GetInterpolatedScale();
            
            // Use prediction if enabled and player is moving
            if (usePrediction && predictor.IsMoving())
            {
                Vector3 predictedPosition = predictor.GetCurrentPredictedPosition();
                float predictedScale = predictor.PredictScale(Time.time);
                
                // Blend between interpolated and predicted position
                float confidence = predictor.GetMovementConfidence();
                interpolatedPos = Vector3.Lerp(interpolatedPos, predictedPosition, 
                                             confidence * predictionStrength);
                interpolatedScale = Mathf.Lerp(interpolatedScale, predictedScale, 
                                             confidence * predictionStrength);
            }
            
            cachedTransform.position = interpolatedPos;
            cachedTransform.localScale = new Vector3(interpolatedScale, 1f, 1f);
        }
        
        public void UpdateMap(string mapName)
        {
            if (currentMap != mapName)
            {
                currentMap = mapName;
                
                // Check if player should be visible in current scene
                bool shouldBeVisible = ShouldBeVisibleInCurrentScene(mapName);
                SetVisibility(shouldBeVisible);
                
                // Reset interpolation and prediction when changing maps
                if (!shouldBeVisible)
                {
                    interpolator.Reset();
                    predictor.Reset();
                }
            }
        }
        
        private bool ShouldBeVisibleInCurrentScene(string mapName)
        {
            // Get current scene name
            string currentScene = "";
            if (GameObject.Find("SceneBorder(Clone)") != null)
            {
                currentScene = GameObject.Find("SceneBorder(Clone)").scene.name;
            }
            
            return currentScene == mapName;
        }
        
        private void SetVisibility(bool visible)
        {
            if (isVisible != visible)
            {
                isVisible = visible;
                cachedGameObject.SetActive(visible);
            }
        }
        
        public void PlayAnimation(string animationName)
        {
            if (spriteAnimator != null && !string.IsNullOrEmpty(animationName))
            {
                // Only play if different from current animation
                if (spriteAnimator.CurrentClip == null || 
                    spriteAnimator.CurrentClip.name != animationName)
                {
                    spriteAnimator.Play(animationName);
                }
            }
        }
        
        public void PlayToolCrestAnimation(ToolCrest toolCrest, string animationName)
        {
            if (spriteAnimator != null && toolCrest != null && !string.IsNullOrEmpty(animationName))
            {
                var clip = toolCrest.HeroConfig.GetAnimationClip(animationName);
                if (clip != null)
                {
                    spriteAnimator.Play(clip);
                }
            }
        }
        
        public CSteamID GetPlayerID()
        {
            return playerID;
        }
        
        // Prediction control methods
        public void SetPredictionEnabled(bool enabled)
        {
            usePrediction = enabled;
            if (!enabled)
            {
                predictor.Reset();
            }
        }
        
        public void SetPredictionStrength(float strength)
        {
            predictionStrength = Mathf.Clamp01(strength);
        }
        
        public Vector3 GetPredictedVelocity()
        {
            return predictor.GetCurrentVelocity();
        }
        
        public string GetDebugInfo()
        {
            return $"Interpolator: {interpolator.GetDebugInfo()}\nPredictor: {predictor.GetDebugInfo()}";
        }
        
        private void OnDestroy()
        {
            // Clean up interpolator and predictor
            interpolator?.Reset();
            predictor?.Reset();
        }
    }
}