// Optimized PlayerNetworkSync with adaptive sync rates and performance improvements
// Versión optimizada de PlayerNetworkSync con tasas de sincronización adaptativas y mejoras de rendimiento

using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SilksongMultiplayer
{
    internal class OptimizedPlayerNetworkSync : MonoBehaviour
    {
        public CSteamID currentRoomID;
        private OptimizedNetworkDataSender optimizedSender;
        
        // Adaptive sync timing
        private float lastSendTime;
        private float currentSendInterval = 0.03f; // Start at 30ms
        private const float MIN_SEND_INTERVAL = 0.016f; // 60 FPS max
        private const float MAX_SEND_INTERVAL = 0.1f;   // 10 FPS min
        
        // Movement detection for adaptive sync
        private Vector3 lastPosition;
        private float lastScale;
        private float movementSpeed;
        private const float HIGH_SPEED_THRESHOLD = 5.0f;
        private const float LOW_SPEED_THRESHOLD = 0.5f;
        
        // Map and compass data
        private string mapName;
        private float createColliderCounter = 0.3f;
        private Canvas canva;
        private GameObject nameText;
        private float compassLastSendTime;
        private const float compassSendInterval = 2f; // Reduced from 1s to 2s
        
        // Performance optimization
        private Transform cachedTransform;
        private bool isMoving = false;
        private float lastMovementCheck;
        private const float MOVEMENT_CHECK_INTERVAL = 0.1f;
        
        // Network statistics for debugging
        private int positionPacketsSent = 0;
        private int positionPacketsSkipped = 0;
        
        private void Start()
        {
            SilksongMultiplayerAPI.playerNetworkSync = this;
            optimizedSender = new OptimizedNetworkDataSender();
            cachedTransform = transform;
            lastPosition = cachedTransform.position;
            lastScale = cachedTransform.localScale.x;
            
            SetupUI();
        }
        
        private void SetupUI()
        {
            GameObject gameObject = new GameObject("nameCanva");
            gameObject.transform.SetPositionAndRotation(cachedTransform.position, Quaternion.identity);
            gameObject.transform.SetParent(cachedTransform);
            
            if (!SilksongMultiplayerAPI.enterRoom)
                return;
                
            canva = gameObject.AddComponent<Canvas>();
            canva.renderMode = RenderMode.WorldSpace;
            canva.sortingLayerName = "HUD";
            canva.sortingLayerID = 629535577;
            canva.sortingOrder = 50;
            gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(2560f, 1440f);
            
            nameText = new GameObject("nameText");
            nameText.transform.SetParent(gameObject.transform);
            nameText.AddComponent<CanvasRenderer>();
            nameText.transform.localScale = Vector3.one * 0.01f;
            
            Text text = nameText.AddComponent<Text>();
            text.text = SteamFriends.GetPersonaName();
            text.font = SilksongMultiplayerAPI.savedFont;
            text.fontSize = 50;
            text.alignment = TextAnchor.MiddleCenter;
            
            // Special colors for specific users
            ulong specialUser1 = 76561198929282998;
            ulong specialUser2 = 76561199835946204;
            if (SteamUser.GetSteamID().m_SteamID == specialUser1 || 
                SteamUser.GetSteamID().m_SteamID == specialUser2)
            {
                text.color = Color.yellow;
            }
        }
        
        private void Update()
        {
            UpdateCompassIcons();
            UpdateMovementDetection();
            UpdateAdaptiveSyncRate();
            SendPositionIfNeeded();
            SendCompassDataIfNeeded();
            HandleMapChanges();
            UpdateUI();
            UpdateColliderTimer();
        }
        
        private void UpdateCompassIcons()
        {
            // Cache compass icons when available
            if (SilksongMultiplayerAPI.compassIcon == null)
            {
                var compassObjects = DDOLFinder.FindInDDOLByName("Game_Map_Hornet(Clone)");
                if (compassObjects.Count > 0)
                {
                    SilksongMultiplayerAPI.compassIcon = DDOLFinder.FindChildByName(compassObjects[0], "Compass Icon");
                }
            }
            
            if (SilksongMultiplayerAPI.wideCompassIcon == null)
            {
                var wideMapObjects = DDOLFinder.FindInDDOLByName("Wide Map(Clone)");
                if (wideMapObjects.Count > 0)
                {
                    SilksongMultiplayerAPI.wideCompassIcon = DDOLFinder.FindChildByName(wideMapObjects[0], "Compass Icon");
                }
            }
        }
        
        private void UpdateMovementDetection()
        {
            if (Time.time - lastMovementCheck < MOVEMENT_CHECK_INTERVAL)
                return;
                
            lastMovementCheck = Time.time;
            
            Vector3 currentPosition = cachedTransform.position;
            float currentScale = cachedTransform.localScale.x;
            
            // Calculate movement speed
            float distance = Vector3.Distance(currentPosition, lastPosition);
            movementSpeed = distance / MOVEMENT_CHECK_INTERVAL;
            
            isMoving = movementSpeed > 0.01f; // Consider moving if speed > 1cm/s
            
            lastPosition = currentPosition;
            lastScale = currentScale;
        }
        
        private void UpdateAdaptiveSyncRate()
        {
            // Adapt sync rate based on movement speed
            if (movementSpeed > HIGH_SPEED_THRESHOLD)
            {
                // High speed: sync more frequently
                currentSendInterval = MIN_SEND_INTERVAL;
            }
            else if (movementSpeed < LOW_SPEED_THRESHOLD)
            {
                // Low speed: sync less frequently
                currentSendInterval = Mathf.Lerp(currentSendInterval, MAX_SEND_INTERVAL, Time.deltaTime);
            }
            else
            {
                // Medium speed: interpolate sync rate
                float speedRatio = (movementSpeed - LOW_SPEED_THRESHOLD) / (HIGH_SPEED_THRESHOLD - LOW_SPEED_THRESHOLD);
                float targetInterval = Mathf.Lerp(MAX_SEND_INTERVAL, MIN_SEND_INTERVAL, speedRatio);
                currentSendInterval = Mathf.Lerp(currentSendInterval, targetInterval, Time.deltaTime * 2f);
            }
            
            currentSendInterval = Mathf.Clamp(currentSendInterval, MIN_SEND_INTERVAL, MAX_SEND_INTERVAL);
        }
        
        private void SendPositionIfNeeded()
        {
            if (Time.time - lastSendTime < currentSendInterval)
                return;
                
            Vector3 position = cachedTransform.position;
            float scale = cachedTransform.localScale.x;
            
            if (optimizedSender.SendPositionData(position, scale))
            {
                positionPacketsSent++;
                lastSendTime = Time.time;
            }
            else
            {
                positionPacketsSkipped++;
                // Still update lastSendTime to maintain rhythm
                lastSendTime = Time.time;
            }
        }
        
        private void SendCompassDataIfNeeded()
        {
            if (Time.time - compassLastSendTime < compassSendInterval)
                return;
                
            if (SilksongMultiplayerAPI.compassIcon != null && SilksongMultiplayerAPI.wideCompassIcon != null)
            {
                Vector2 compassPos = SilksongMultiplayerAPI.compassIcon.transform.localPosition;
                Vector2 wideCompassPos = SilksongMultiplayerAPI.wideCompassIcon.transform.localPosition;
                
                if (optimizedSender.SendMapPositionData(compassPos, wideCompassPos))
                {
                    compassLastSendTime = Time.time;
                }
            }
        }
        
        private void HandleMapChanges()
        {
            GameObject sceneBorder = GameObject.Find("SceneBorder(Clone)");
            if (sceneBorder != null)
            {
                string currentMapName = sceneBorder.scene.name;
                if (currentMapName != mapName)
                {
                    mapName = currentMapName;
                    optimizedSender.SendMapChangeNotification(mapName);
                    
                    // Reset change detection when changing maps
                    optimizedSender.ResetChangeDetection();
                }
            }
        }
        
        private void UpdateUI()
        {
            if (!SilksongMultiplayerAPI.enterRoom || canva == null)
                return;
                
            // Update name canvas position and scale
            canva.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            nameText.transform.localPosition = Vector3.zero;
            nameText.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 50f);
            
            // Flip canvas based on player scale
            if (cachedTransform.localScale.x < 0f)
            {
                canva.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                canva.transform.localScale = new Vector3(1f, 1f, 1f);
            }
        }
        
        private void UpdateColliderTimer()
        {
            if (createColliderCounter < 0f && createColliderCounter > -100f)
            {
                if (SilksongMultiplayerAPI.enterRoom && canva != null)
                {
                    canva.renderMode = RenderMode.WorldSpace;
                    canva.transform.localPosition = Vector3.zero;
                    nameText.transform.localPosition = Vector3.zero;
                }
                createColliderCounter = -100f;
            }
            else
            {
                createColliderCounter -= Time.deltaTime;
            }
        }
        
        // Public methods for sending specific data
        public void SendAnimationData(string animationName, int extraValue)
        {
            optimizedSender.SendAnimationData(animationName, extraValue);
        }
        
        public void SendHeroAttackAnimationData(string parentName, string name, string animationName)
        {
            optimizedSender.SendHeroAttackAnimationData(parentName, name, animationName);
        }
        
        public void SendTargetHeroTakeDamageData(ulong targetSteamId, int damage, int direction, int hazardType, int attackTypes)
        {
            optimizedSender.SendTargetHeroTakeDamageData(targetSteamId, damage, direction, hazardType, attackTypes);
        }
        
        // Debug information
        public void LogNetworkStats()
        {
            float efficiency = positionPacketsSent + positionPacketsSkipped > 0 ? 
                (float)positionPacketsSkipped / (positionPacketsSent + positionPacketsSkipped) * 100f : 0f;
                
            Debug.Log($"Network Stats - Sent: {positionPacketsSent}, Skipped: {positionPacketsSkipped}, Efficiency: {efficiency:F1}%");
            Debug.Log($"Current sync interval: {currentSendInterval * 1000f:F1}ms, Movement speed: {movementSpeed:F2}");
        }
        
        // Utility methods from original class
        public static GameObject FindInactiveChildByName(GameObject parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child.gameObject;
            }
            return null;
        }
        
        public GameObject[] FindInActiveObjectsByName(string name)
        {
            List<GameObject> gameObjectList = new List<GameObject>();
            foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject != null && gameObject.scene.IsValid() && gameObject.name == name)
                {
                    gameObjectList.Add(gameObject);
                }
            }
            return gameObjectList.ToArray();
        }
    }
}