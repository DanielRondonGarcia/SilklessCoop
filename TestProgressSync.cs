using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SilklessCoop
{
    /// <summary>
    /// Clase de prueba para validar el sistema de sincronización de progreso
    /// Solo se usa cuando PrintDebugOutput está habilitado
    /// </summary>
    internal class TestProgressSync : MonoBehaviour
    {
        public ManualLogSource Logger;
        public ModConfig Config;
        
        private GameProgressSync _progressSync;
        private float _testTimer = 0f;
        private const float TEST_INTERVAL = 10.0f; // Probar cada 10 segundos
        private int _testCounter = 0;
        
        private void Start()
        {
            _progressSync = GetComponent<GameProgressSync>();
            
            if (Config.PrintDebugOutput)
                Logger.LogInfo("TestProgressSync initialized - will run tests every 10 seconds");
        }

        /// <summary>
        /// Serialización simple para testing
        /// </summary>
        private string SerializeSimple(Dictionary<string, object> data)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var kvp in data)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kvp.Key}\":\"{kvp.Value}\"");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }
        
        private void Update()
        {
            if (!Config.PrintDebugOutput || !Config.SyncGameProgress) return;
            
            _testTimer += Time.deltaTime;
            
            if (_testTimer >= TEST_INTERVAL)
            {
                RunProgressSyncTest();
                _testTimer = 0f;
                _testCounter++;
            }
        }
        
        /// <summary>
        /// Ejecuta una prueba del sistema de sincronización de progreso
        /// </summary>
        private void RunProgressSyncTest()
        {
            try
            {
                Logger.LogInfo($"=== Progress Sync Test #{_testCounter + 1} ===");
                
                // Test 1: Simular datos de progreso
                var testProgress = CreateTestProgressData();
                string serializedProgress = SerializeSimple(testProgress);
                
                Logger.LogInfo($"Test progress data: {serializedProgress}");
                
                // Test 2: Probar aplicación de progreso recibido
                if (_progressSync != null)
                {
                    _progressSync.ApplyReceivedProgress(serializedProgress);
                    Logger.LogInfo("Successfully applied test progress data");
                }
                else
                {
                    Logger.LogWarning("GameProgressSync component not found");
                }
                
                // Test 3: Verificar que los campos de progreso son accesibles
                TestPlayerDataAccess();
                
                Logger.LogInfo("=== Progress Sync Test Complete ===");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Progress sync test failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Crea datos de progreso de prueba
        /// </summary>
        private Dictionary<string, object> CreateTestProgressData()
        {
            return new Dictionary<string, object>
            {
                // Recursos
                { "geo", 1000 },
                { "silk", 50 },
                
                // Habilidades básicas
                { "hasWalljump", true },
                { "hasDoubleJump", false },
                { "hasDash", true },
                { "hasSuperdash", false },
                
                // Objetos
                { "hasLantern", true },
                { "hasQuill", false },
                { "hasMap", true },
                { "hasCompass", true },
                
                // Progreso simulado
                { "openedDoor_test1", true },
                { "visitedArea_testArea", true },
                { "collectedItem_testItem", true },
                { "questCompleted_testQuest", false },
                { "npcTalkedTo_testNPC", true }
            };
        }
        
        /// <summary>
        /// Prueba el acceso a PlayerData usando reflexión
        /// </summary>
        private void TestPlayerDataAccess()
        {
            try
            {
                var playerDataType = Type.GetType("PlayerData, Assembly-CSharp");
                
                if (playerDataType != null)
                {
                    Logger.LogInfo("PlayerData type found successfully");
                    
                    var instanceProperty = playerDataType.GetProperty("instance", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    
                    if (instanceProperty != null)
                    {
                        Logger.LogInfo("PlayerData instance property found");
                        
                        var playerDataInstance = instanceProperty.GetValue(null);
                        
                        if (playerDataInstance != null)
                        {
                            Logger.LogInfo("PlayerData instance is available");
                            
                            // Probar acceso a algunos campos comunes
                            var fields = playerDataType.GetFields();
                            int fieldCount = 0;
                            
                            foreach (var field in fields)
                            {
                                if (field.Name.Contains("geo") || 
                                    field.Name.Contains("has") || 
                                    field.Name.Contains("opened") ||
                                    field.Name.Contains("visited") ||
                                    field.Name.Contains("collected"))
                                {
                                    try
                                    {
                                        var value = field.GetValue(playerDataInstance);
                                        Logger.LogInfo($"Field {field.Name}: {value} (Type: {field.FieldType.Name})");
                                        fieldCount++;
                                        
                                        if (fieldCount >= 5) break; // Limitar output
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogWarning($"Could not access field {field.Name}: {ex.Message}");
                                    }
                                }
                            }
                            
                            Logger.LogInfo($"Found {fieldCount} relevant progress fields");
                        }
                        else
                        {
                            Logger.LogWarning("PlayerData instance is null");
                        }
                    }
                    else
                    {
                        Logger.LogWarning("PlayerData instance property not found");
                    }
                }
                else
                {
                    Logger.LogWarning("PlayerData type not found - this is expected if not in game");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"PlayerData access test failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Simula la recepción de datos de progreso de otro jugador
        /// </summary>
        public void SimulateReceivedProgress()
        {
            if (!Config.PrintDebugOutput) return;
            
            var simulatedProgress = new Dictionary<string, object>
            {
                { "geo", 2000 },
                { "hasDoubleJump", true },
                { "openedDoor_simulation", true }
            };
            
            string progressData = SerializeSimple(simulatedProgress);
            
            Logger.LogInfo($"Simulating received progress: {progressData}");
            
            if (_progressSync != null)
            {
                _progressSync.ApplyReceivedProgress(progressData);
            }
        }
        
        /// <summary>
        /// Genera un reporte del estado actual del sistema de sincronización
        /// </summary>
        public void GenerateStatusReport()
        {
            if (!Config.PrintDebugOutput) return;
            
            Logger.LogInfo("=== Progress Sync Status Report ===");
            Logger.LogInfo($"SyncGameProgress enabled: {Config.SyncGameProgress}");
            Logger.LogInfo($"GameProgressSync component: {(_progressSync != null ? "Found" : "Not found")}");
            Logger.LogInfo($"Tests run: {_testCounter}");
            Logger.LogInfo("======================================");
        }
    }
}