using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SilklessCoop
{
    internal class GameSync : MonoBehaviour
    {
        private static float stof(string s)
        {
            float f1;
            try { f1 = float.Parse(s.Replace(",", ".")); } catch (Exception) { f1 = float.MaxValue; }
            float f2;
            try { f2 = float.Parse(s.Replace(".", ",")); } catch (Exception) { f2 = float.MaxValue; }

            if (Mathf.Abs(f1) < Mathf.Abs(f2)) return f1;
            else return f2;
        }

        public ManualLogSource Logger;
        public ModConfig Config;

        // sprite sync - self
        private GameObject _hornetObject = null;
        private tk2dSprite _hornetSprite = null;
        private Rigidbody2D _hornetRigidbody = null;

        // sprite sync - others
        private Dictionary<string, GameObject> _playerObjects = new Dictionary<string, GameObject>();
        private Dictionary<string, tk2dSprite> _playerSprites = new Dictionary<string, tk2dSprite>();
        private Dictionary<string, SimpleInterpolator> _playerInterpolators = new Dictionary<string, SimpleInterpolator>();
        
        // player color pins
        private Dictionary<string, GameObject> _playerColorPins = new Dictionary<string, GameObject>();
        private Dictionary<string, Color> _playerColors = new Dictionary<string, Color>();
        private List<Color> _availableColors = new List<Color>
        {
            new Color(1.0f, 0.2f, 0.2f, 0.9f), // Rojo
            new Color(0.2f, 0.8f, 0.2f, 0.9f), // Verde
            new Color(0.2f, 0.4f, 1.0f, 0.9f), // Azul
            new Color(1.0f, 0.8f, 0.2f, 0.9f), // Amarillo
            new Color(1.0f, 0.4f, 0.8f, 0.9f), // Rosa
            new Color(0.6f, 0.2f, 1.0f, 0.9f), // Púrpura
            new Color(0.2f, 0.8f, 0.8f, 0.9f), // Cian
            new Color(1.0f, 0.6f, 0.2f, 0.9f)  // Naranja
        };
        private int _nextColorIndex = 0;
        
        // pin de color para jugador local (solo para debug)
        private GameObject _localPlayerColorPin = null;
        private Color _localPlayerColor = new Color(1.0f, 1.0f, 1.0f, 0.9f); // Blanco para el jugador local
        
        // Arquitectura Host-Cliente: datos pendientes para envío
        private string _pendingCustomUpdate = null;
        
        // Sistema optimizado de nombres de usuario
        private SilksongMultiplayer.OptimizedUsernameSync _usernameSync = null;

        // player count
        private GameObject _pauseMenu = null;
        private int _playerCount = 0;
        private List<GameObject> _countPins = new List<GameObject>();

        // map sync - self
        private GameObject _mainQuests = null;
        private GameObject _map = null;
        private GameObject _compass = null;

        // map sync - others
        private Dictionary<string, GameObject> _playerCompasses = new Dictionary<string, GameObject>();
        private Dictionary<string, tk2dSprite> _playerCompassSprites = new Dictionary<string, tk2dSprite>();

        // progress sync
        private GameProgressSync _progressSync = null;
        
        // combat sync
        private CombatSync _combatSync = null;
        
        // time sync
        private TimeSync _timeSync = null;
        
        // inventory sync
        private InventorySync _inventorySync = null;
        
        // audio sync
        private AudioSync _audioSync = null;
        
        // connector
        private Connector _connector = null;

        private bool _setup = false;

        private void Update()
        {
            if (!_hornetObject) _hornetObject = GameObject.Find("Hero_Hornet");
            if (!_hornetObject) _hornetObject = GameObject.Find("Hero_Hornet(Clone)");
            if (!_hornetObject) { _setup = false; return; }

            if (!_hornetSprite) _hornetSprite = _hornetObject.GetComponent<tk2dSprite>();
            if (!_hornetSprite) { _setup = false; return; }

            if (!_hornetRigidbody) _hornetRigidbody = _hornetObject.GetComponent<Rigidbody2D>();
            if (!_hornetRigidbody) { _setup = false; return; }

            if (!_map) _map = GameObject.Find("Game_Map_Hornet");
            if (!_map) _map = GameObject.Find("Game_Map_Hornet(Clone)");
            if (!_map) { _setup = false; return; }

            if (!_compass) _compass = _map.transform.Find("Compass Icon")?.gameObject;
            if (!_compass) { _setup = false; return; }

            if (!_pauseMenu) _pauseMenu = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "NewPauseMenuScreen");
            if (!_pauseMenu) { _setup = false; return; }

            if (!_mainQuests) _mainQuests = _map.transform.Find("Main Quest Pins")?.gameObject;
            if (!_mainQuests) { _setup = false; return; }

            if (Config.SyncCompasses)
            {
                foreach (GameObject g in _playerCompasses.Values)
                    if (g != null) g.SetActive(_mainQuests.activeSelf);
            }

            foreach (GameObject g in _countPins)
                if (g != null) g.SetActive(_mainQuests.activeSelf);

            if (!_setup)
            {
                _setup = true;
                
                // Inicializar conector
                if (_connector == null)
                {
                    _connector = GetComponent<Connector>();
                }
                
                // Inicializar sistema de sincronización de progreso si está habilitado
                if (_progressSync == null && Config.SyncGameProgress)
                {
                    _progressSync = gameObject.AddComponent<GameProgressSync>();
                    _progressSync.Logger = Logger;
                    _progressSync.Config = Config;
                    
                    // Agregar componente de prueba si el debug está habilitado
                    if (Config.PrintDebugOutput)
                    {
                        var testSync = gameObject.AddComponent<TestProgressSync>();
                        testSync.Logger = Logger;
                        testSync.Config = Config;
                    }
                }
                
                // Inicializar sistema de sincronización de combate
                if (_combatSync == null)
                {
                    _combatSync = gameObject.AddComponent<CombatSync>();
                    _combatSync.Logger = Logger;
                    _combatSync.Config = Config;
                }
                
                // Inicializar sistema de tiempo
                if (_timeSync == null)
                {
                    _timeSync = gameObject.AddComponent<TimeSync>();
                    _timeSync.Initialize(Logger, Config);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo("TimeSync component initialized");
                }
                
                // Inicializar sistema de inventario
                if (_inventorySync == null)
                {
                    _inventorySync = gameObject.AddComponent<InventorySync>();
                    _inventorySync.Initialize(Logger, Config);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo("InventorySync component initialized");
                }
                
                // Inicializar sistema optimizado de nombres de usuario
                if (_usernameSync == null)
                {
                    _usernameSync = _hornetObject.AddComponent<SilksongMultiplayer.OptimizedUsernameSync>();
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo("OptimizedUsernameSync component initialized");
                }
                
                // TODO: Reactivar sistema de audio cuando sea necesario
                // Inicializar sistema de audio - TEMPORALMENTE DESACTIVADO
                /*
                if (_audioSync == null)
                {
                    _audioSync = gameObject.AddComponent<AudioSync>();
                    _audioSync.Initialize(Logger, Config);
                    
                    if (Config.PrintDebugOutput)
                        Logger.LogInfo("AudioSync component initialized");
                }
                */

                Logger.LogInfo("GameObject setup complete with optimized username sync.");
            }
            
            // Manejar pin de color del jugador local cuando PrintDebugOutput está activado
            if (Config.PrintDebugOutput)
            {
                if (_localPlayerColorPin == null)
                {
                    CreateLocalPlayerColorPin();
                }
                else
                {
                    UpdateLocalPlayerColorPin();
                }
            }
            else
            {
                if (_localPlayerColorPin != null)
                {
                    Destroy(_localPlayerColorPin);
                    _localPlayerColorPin = null;
                }
            }
        }

        public string GetUpdateContent()
        {
            if (!_setup) return null;
            
            // Arquitectura Host-Cliente: Priorizar datos personalizados
            if (!string.IsNullOrEmpty(_pendingCustomUpdate))
            {
                string customData = _pendingCustomUpdate;
                _pendingCustomUpdate = null; // Limpiar después de usar
                return customData;
            }

            string scene = SceneManager.GetActiveScene().name;
            float posX = _hornetObject.transform.position.x;
            float posY = _hornetObject.transform.position.y;
            float posZ = _hornetObject.transform.position.z;
            int spriteId = _hornetSprite.spriteId;
            float scaleX = _hornetObject.transform.localScale.x;
            float vX = _hornetRigidbody.linearVelocity.x;
            float vY = _hornetRigidbody.linearVelocity.y;

            int compassActive = 0;
            float compassX = 0;
            float compassY = 0;

            if (Config.SyncCompasses)
            {
                compassActive = _compass.activeSelf ? 1 : 0;
                compassX = _compass.transform.localPosition.x;
                compassY = _compass.transform.localPosition.y;
            }

            string baseData = $"{scene}:{posX}:{posY}:{posZ}:{spriteId}:{scaleX}:{vX}:{vY}";
            string compassData = Config.SyncCompasses ? $":{compassActive}:{compassX}:{compassY}" : "";
            string data = $"{baseData}{compassData}";

            // Verificar si hay datos de progreso pendientes para enviar
            if (_progressSync != null && Config.SyncGameProgress)
            {
                string progressData = _progressSync.GetPendingProgressData();
                if (!string.IsNullOrEmpty(progressData))
                {
                    return progressData; // Enviar datos de progreso en lugar de datos de posición
                }
            }
            
            // Verificar si hay datos de combate pendientes para enviar
            if (_combatSync != null)
            {
                string combatData = _combatSync.GetPendingData();
                if (!string.IsNullOrEmpty(combatData))
                {
                    return combatData; // Enviar datos de combate
                }
            }
            
            // Verificar si hay datos de tiempo pendientes para enviar
            if (_timeSync != null)
            {
                string timeData = _timeSync.GetPendingData();
                if (!string.IsNullOrEmpty(timeData))
                {
                    return timeData; // Enviar datos de tiempo
                }
            }
            
            // Verificar si hay datos de inventario pendientes para enviar
            if (_inventorySync != null)
            {
                string inventoryData = _inventorySync.GetPendingData();
                if (!string.IsNullOrEmpty(inventoryData))
                {
                    return inventoryData; // Enviar datos de inventario
                }
            }
            
            // Verificar si hay datos de audio pendientes para enviar
            if (_audioSync != null)
            {
                string audioData = _audioSync.GetPendingAudioData();
                if (!string.IsNullOrEmpty(audioData))
                {
                    return $"AUDIO::{audioData}"; // Enviar datos de audio
                }
            }

            return data;
        }

        public void ApplyUpdate(string data)
        {
            try
            {
                if (!_setup) return;

                if (Config.PrintDebugOutput) Logger.LogInfo($"Applying update {data}...");

                // Verificar si es un mensaje de progreso del juego
                if (data.Contains("PROGRESS::"))
                {
                    string[] progressParts = data.Split(new string[] { "PROGRESS::" }, StringSplitOptions.None);
                    if (progressParts.Length > 1 && _progressSync != null)
                    {
                        _progressSync.ApplyReceivedProgress(progressParts[1]);
                        return;
                    }
                }
                
                // Verificar si es un mensaje de combate
                if (data.Contains("COMBAT::"))
                {
                    string[] combatParts = data.Split(new string[] { "COMBAT::" }, StringSplitOptions.None);
                    if (combatParts.Length > 1 && _combatSync != null)
                    {
                        _combatSync.ApplyReceivedData(combatParts[1]);
                        return;
                    }
                }
                
                // Verificar si es un mensaje de tiempo
                if (data.Contains("TIME::"))
                {
                    string[] timeParts = data.Split(new string[] { "TIME::" }, StringSplitOptions.None);
                    if (timeParts.Length > 1 && _timeSync != null)
                    {
                        _timeSync.ApplyReceivedData(timeParts[1]);
                        return;
                    }
                }
                
                // Verificar si es un mensaje de inventario
                if (data.Contains("INVENTORY::"))
                {
                    string[] inventoryParts = data.Split(new string[] { "INVENTORY::" }, StringSplitOptions.None);
                    if (inventoryParts.Length > 1 && _inventorySync != null)
                    {
                        _inventorySync.ApplyReceivedData(inventoryParts[1]);
                        return;
                    }
                }
                
                // Verificar si es un mensaje de audio
                if (data.Contains("AUDIO::"))
                {
                    string[] audioParts = data.Split(new string[] { "AUDIO::" }, StringSplitOptions.None);
                    if (audioParts.Length > 1 && _audioSync != null)
                    {
                        _audioSync.ProcessRemoteAudioData(audioParts[1]);
                        return;
                    }
                }
                
                // Arquitectura Host-Cliente: Manejar estado del mundo
                if (data.Contains("WORLD_STATE::"))
                {
                    string[] worldStateParts = data.Split(new string[] { "WORLD_STATE::" }, StringSplitOptions.None);
                    if (worldStateParts.Length > 1 && _combatSync != null)
                    {
                        _combatSync.ApplyWorldState(worldStateParts[1]);
                        return;
                    }
                }
                
                // Arquitectura Host-Cliente: Manejar input del jugador
                if (data.Contains("PLAYER_INPUT::"))
                {
                    string[] playerInputParts = data.Split(new string[] { "PLAYER_INPUT::" }, StringSplitOptions.None);
                    if (playerInputParts.Length > 1 && _combatSync != null)
                    {
                        _combatSync.ProcessPlayerInput(playerInputParts[1]);
                        return;
                    }
                }

                UpdateUI();

                string[] parts = data.Split("::");
                string id = parts[0];
                string[] metadataParts = parts[1].Split(":");
                string[] contentParts = parts[2].Split(":");

                _playerCount = int.Parse(metadataParts[0]);

                string scene = contentParts[0];
                float posX = stof(contentParts[1]);
                float posY = stof(contentParts[2]);
                float posZ = stof(contentParts[3]);
                int spriteId = int.Parse(contentParts[4]);
                float scaleX = stof(contentParts[5]);
                float vX = stof(contentParts[6]);
                float vY = stof(contentParts[7]);

                bool compassActive = false;
                float compassX = 0;
                float compassY = 0;

                if (Config.SyncCompasses && contentParts.Length > 8)
                {
                    compassActive = contentParts[8] == "1";
                    compassX = stof(contentParts[9]);
                    compassY = stof(contentParts[10]);
                }

                bool sameScene = scene == SceneManager.GetActiveScene().name;

                if (!_playerObjects.ContainsKey(id))
                {
                    _playerObjects.Add(id, null);
                    _playerSprites.Add(id, null);
                    _playerInterpolators.Add(id, null);
                }

                if (!_playerCompasses.ContainsKey(id))
                {
                    _playerCompasses.Add(id, null);
                    if (!_playerCompassSprites.ContainsKey(id)) _playerCompassSprites.Add(id, null);
                }
                
                if (!_playerColorPins.ContainsKey(id))
                {
                    _playerColorPins.Add(id, null);
                }
                
                // Asignar color único al jugador si no tiene uno
                if (!_playerColors.ContainsKey(id))
                {
                    Color playerColor = GetNextPlayerColor();
                    _playerColors.Add(id, playerColor);
                    if (Config.PrintDebugOutput) Logger.LogInfo($"Assigned color {playerColor} to player {id}");
                }

                if (!sameScene)
                {
                    // clear dupes if player leaves scene
                    if (_playerObjects.ContainsKey(id))
                        if (_playerObjects[id] != null)
                            Destroy(_playerObjects[id]);
                    
                    // Limpiar pin de color también
                    if (_playerColorPins.ContainsKey(id))
                        if (_playerColorPins[id] != null)
                            Destroy(_playerColorPins[id]);
                } else
                {
                    if (_playerObjects[id] != null)
                    {
                        // update player
                        _playerObjects[id].transform.position = new Vector3(posX, posY, posZ + 0.001f);
                        _playerObjects[id].transform.localScale = new Vector3(scaleX, 1, 1);
                        _playerSprites[id].spriteId = spriteId;
                        _playerInterpolators[id].velocity = new Vector3(vX, vY, 0);
                        
                        // Actualizar posición del pin de color si está habilitado
                        if (Config.ShowPlayerColorPins)
                        {
                            if (Config.PrintDebugOutput) Logger.LogInfo($"Updating color pin position for player {id}");
                            UpdatePlayerColorPin(id, _playerObjects[id]);
                        }
                    }
                    else
                    {
                        // create player
                        if (Config.PrintDebugOutput) Logger.LogInfo($"Creating new player object for player {id}...");

                        GameObject newObject = new GameObject();
                        newObject.name = "SilklessCooperator";
                        newObject.transform.position = new Vector3(posX, posY, posZ + 0.001f);
                        newObject.transform.localScale = new Vector3(scaleX, 1, 1);

                        tk2dSprite newSprite = tk2dSprite.AddComponent(newObject, _hornetSprite.Collection, 0);
                        newSprite.color = new Color(1, 1, 1, Config.PlayerOpacity);

                        SimpleInterpolator newInterpolator = newObject.AddComponent<SimpleInterpolator>();
                        newInterpolator.velocity = new Vector3(vX, vY, 0);

                        _playerObjects[id] = newObject;
                        _playerSprites[id] = newSprite;
                        _playerInterpolators[id] = newInterpolator;
                        
                        // Crear pin de color para el jugador si está habilitado
                        if (Config.ShowPlayerColorPins)
                        {
                            if (Config.PrintDebugOutput) Logger.LogInfo($"ShowPlayerColorPins is enabled, creating color pin for player {id}");
                            CreatePlayerColorPin(id, newObject);
                        }
                        else
                        {
                            if (Config.PrintDebugOutput) Logger.LogInfo($"ShowPlayerColorPins is disabled, skipping color pin creation for player {id}");
                        }

                        if (Config.PrintDebugOutput) Logger.LogInfo($"Successfully created new player object for player {id}.");
                    }
                }
                
                if (Config.SyncCompasses)
                {
                    if (compassActive)
                    {
                        if (_playerCompasses[id] != null)
                        {
                            // update compass
                            _playerCompasses[id].transform.localPosition = new Vector3(compassX, compassY, _compass.transform.localPosition.z + 0.001f);
                            _playerCompassSprites[id].color = new Color(1, 1, 1, Config.ActiveCompassOpacity);
                        }
                        else
                        {
                            // create compass
                            if (Config.PrintDebugOutput) Logger.LogInfo($"Creating new compass for player {id}...");

                            GameObject newObject = Instantiate(_compass, _map.transform);
                            newObject.name = "SilklessCompass";
                            newObject.transform.localPosition = new Vector3(compassX, compassY, _compass.transform.localPosition.z + 0.001f);
                            tk2dSprite newSprite = newObject.GetComponent<tk2dSprite>();
                            newSprite.color = new Color(1, 1, 1, Config.ActiveCompassOpacity);

                            _playerCompasses[id] = newObject;
                            _playerCompassSprites[id] = newSprite;

                            if (Config.PrintDebugOutput) Logger.LogInfo($"Successfully created new compass for player {id}.");
                        }
                    }
                    else
                    {
                        if (_playerCompasses[id] != null)
                            _playerCompassSprites[id].color = new Color(1, 1, 1, Config.InactiveCompassOpacity);
                    }
                }
            } catch (Exception e)
            {
                Logger.LogError($"Error while applying update: {e}");
            }
        }

        private void UpdateUI()
        {
            try
            {
                while (_countPins.Count < _playerCount)
                {
                    if (Config.PrintDebugOutput) Logger.LogInfo($"Creating player count pin {_countPins.Count + 1}...");

                    GameObject newPin = Instantiate(_compass, _map.transform);
                    newPin.name = "SilklessPlayerCountPin";
                    _countPins.Add(newPin);

                    if (Config.PrintDebugOutput) Logger.LogInfo($"Successfully created player count pin {_countPins.Count}.");
                }

                while (_countPins.Count > _playerCount)
                {
                    if (Config.PrintDebugOutput) Logger.LogInfo($"Removing player count pin {_countPins.Count}...");

                    Destroy(_countPins[_countPins.Count - 1]);
                    _countPins.RemoveAt(_countPins.Count - 1);

                    if (Config.PrintDebugOutput) Logger.LogInfo($"Successfully removed player count pin {_countPins.Count + 1}.");
                }

                for (int i = 0; i < _countPins.Count; i++)
                    _countPins[i].transform.position = new Vector3(-14.8f + i * 0.9f, -8.2f, -5f);
            }
            catch (Exception e)
            {
                Logger.LogError($"Error while updating ui: {e}");
            }
        }

        private Color GetNextPlayerColor()
        {
            Color color = _availableColors[_nextColorIndex % _availableColors.Count];
            _nextColorIndex++;
            return color;
        }
        
        private void CreatePlayerColorPin(string playerId, GameObject playerObject)
        {
            if (_playerColorPins[playerId] != null) return;
            
            // Crear el pin de color en el mundo, no como hijo del jugador
            GameObject colorPin = new GameObject($"PlayerColorPin_{playerId}");
            
            // Agregar componente de imagen
            SpriteRenderer pinRenderer = colorPin.AddComponent<SpriteRenderer>();
            
            // Crear sprite circular para el pin
            Texture2D pinTexture = CreateCircularPinTexture(_playerColors[playerId]);
            Sprite pinSprite = Sprite.Create(pinTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            pinRenderer.sprite = pinSprite;
            pinRenderer.sortingOrder = 1000; // Asegurar que esté encima de otros elementos
            
            // Posicionar el pin encima del jugador (posición absoluta)
            Vector3 playerPos = playerObject.transform.position;
            colorPin.transform.position = new Vector3(playerPos.x, playerPos.y + 2.0f, playerPos.z - 0.1f);
            colorPin.transform.localScale = new Vector3(1.2f, 1.2f, 1f); // Tamaño consistente con pin local
            
            if (Config.PrintDebugOutput) Logger.LogInfo($"Pin sprite created with texture size 32x32, color {_playerColors[playerId]}, position {colorPin.transform.position}");
            
            _playerColorPins[playerId] = colorPin;
            
            if (Config.PrintDebugOutput) Logger.LogInfo($"Created color pin for player {playerId} at position {colorPin.transform.position}");
        }
        
        private void UpdatePlayerColorPin(string playerId, GameObject playerObject)
        {
            if (_playerColorPins[playerId] == null) return;
            
            // Actualizar la posición del pin para que siga al jugador
            Vector3 playerPos = playerObject.transform.position;
            _playerColorPins[playerId].transform.position = new Vector3(playerPos.x, playerPos.y + 2.0f, playerPos.z - 0.1f);
            _playerColorPins[playerId].SetActive(true);
        }
        
        private Texture2D CreateCircularPinTexture(Color pinColor)
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color pixelColor = Color.clear;
                    
                    if (distance <= radius)
                    {
                        if (distance <= radius - 1)
                        {
                            // Interior del círculo con el color del jugador
                            pixelColor = pinColor;
                        }
                        else
                        {
                            // Borde del círculo en color más oscuro
                            pixelColor = new Color(pinColor.r * 0.7f, pinColor.g * 0.7f, pinColor.b * 0.7f, pinColor.a);
                        }
                    }
                    
                    pixels[y * size + x] = pixelColor;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void CreateLocalPlayerColorPin()
        {
            if (_localPlayerColorPin != null) return;
            
            // Crear el pin de color para el jugador local
            GameObject colorPin = new GameObject("LocalPlayerColorPin");
            
            // Agregar componente de imagen
            SpriteRenderer pinRenderer = colorPin.AddComponent<SpriteRenderer>();
            
            // Crear sprite circular para el pin
            Texture2D pinTexture = CreateCircularPinTexture(_localPlayerColor);
            Sprite pinSprite = Sprite.Create(pinTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            pinRenderer.sprite = pinSprite;
            pinRenderer.sortingOrder = 1000; // Asegurar que esté encima de otros elementos
            
            // Posicionar el pin encima del jugador local
            Vector3 playerPos = _hornetObject.transform.position;
            colorPin.transform.position = new Vector3(playerPos.x, playerPos.y + 2.0f, playerPos.z - 0.1f);
            colorPin.transform.localScale = new Vector3(1.2f, 1.2f, 1f); // Tamaño consistente con otros pins
            
            _localPlayerColorPin = colorPin;
            
            if (Config.PrintDebugOutput) Logger.LogInfo($"Created local player color pin at position {colorPin.transform.position}");
        }
        
        private void UpdateLocalPlayerColorPin()
        {
            if (_localPlayerColorPin == null) return;
            
            // Actualizar la posición del pin para que siga al jugador local
            Vector3 playerPos = _hornetObject.transform.position;
            _localPlayerColorPin.transform.position = new Vector3(playerPos.x, playerPos.y + 2.0f, playerPos.z - 0.1f);
            _localPlayerColorPin.SetActive(true);
        }

        public void Reset()
        {
            foreach (GameObject g in _playerObjects.Values)
                if (g != null) Destroy(g);
            _playerObjects.Clear();
            _playerSprites.Clear();
            _playerInterpolators.Clear();
            
            foreach (GameObject g in _playerColorPins.Values)
                if (g != null) Destroy(g);
            _playerColorPins.Clear();
            _playerColors.Clear();
            _nextColorIndex = 0;
            
            // Limpiar pin del jugador local también
            if (_localPlayerColorPin != null)
            {
                Destroy(_localPlayerColorPin);
                _localPlayerColorPin = null;
            }

            foreach (GameObject g in _countPins)
                if (g != null) Destroy(g);
            _countPins.Clear();

            foreach (GameObject g in _playerCompasses.Values)
                if (g != null) Destroy(g);
            _playerCompasses.Clear();
            
            // Resetear sistema de sincronización de progreso
            if (_progressSync != null)
            {
                _progressSync.Reset();
            }
            
            if (_combatSync != null)
            {
                _combatSync.Reset();
            }
            
            if (_timeSync != null)
            {
                _timeSync.Reset();
            }
            
            if (_inventorySync != null)
            {
                _inventorySync.Reset();
            }
        }
        
        /// <summary>
        /// Envía una actualización a través del conector (para arquitectura Host-Cliente)
        /// </summary>
        /// <param name="data">Datos a enviar</param>
        public void SendUpdate(string data)
        {
            if (_connector != null && _connector.Active)
            {
                // Usar el mecanismo de envío del conector
                // Temporalmente almacenar los datos para que Tick() los envíe
                _pendingCustomUpdate = data;
                
                if (Config.PrintDebugOutput)
                    Logger.LogInfo($"Queued update for sending: {data.Substring(0, Math.Min(100, data.Length))}...");
            }
        }
    }
}
