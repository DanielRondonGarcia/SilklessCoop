using BepInEx.Logging;
using System.IO;
using System.Linq;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SilklessCoop
{
    internal class UIAdder : MonoBehaviour
    {
        public ManualLogSource Logger;
        public ModConfig Config;

        private Connector _connector;

        private GameObject _oldButton;
        private GameObject _mainMenu;
        private GameObject _mainMenuContainer;
        private GameObject _mainMenuButton;
        private UnityEngine.UI.Text _mainMenuText;

        // Indicador visual durante el juego
        private GameObject _gameIndicator;
        private UnityEngine.UI.Image _gameIndicatorImage;
        private Canvas _gameCanvas;
        private GameObject _statusText;
        private UnityEngine.UI.Text _statusTextComponent;
        private float _statusTextTimer = 0f;
        private bool _lastConnectorState = false;

        private void Start()
        {
            _connector = GetComponent<Connector>();
        }

        private void Update()
        {
            if (_connector.Initialized)
            {
                if (Input.GetKeyDown(Config.MultiplayerToggleKey))
                {
                    if (_connector.Active) _connector.Disable();
                    else _connector.Enable();
                    
                    // Mostrar texto temporal al cambiar estado
                    ShowStatusText(_connector.Active ? "Enabled" : "Disabled");
                }
            }

            // Manejar indicador visual durante el juego
            UpdateGameIndicator();

            if (!_mainMenu) _mainMenu = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "MainMenuScreen");
            if (!_mainMenu) return;

            if (!_mainMenuContainer) _mainMenuContainer = _mainMenu.transform.Find("MainMenuButtons").gameObject;
            if (!_mainMenuContainer) return;

            if (!_oldButton) _oldButton = _mainMenuContainer.transform.Find("OptionsButton").gameObject;
            if (!_oldButton) return;

            if (!_mainMenuButton)
            {
                _mainMenuButton = Instantiate(_oldButton);
                _mainMenuButton.transform.SetParent(_mainMenuContainer.transform);
                _mainMenuButton.transform.localScale = Vector3.one;
                _mainMenuButton.name = "MultiplayerButton";
                _mainMenuText = _mainMenuButton.transform.GetChild(0).gameObject.GetComponent<UnityEngine.UI.Text>();

                EventTrigger et = _mainMenuButton.GetComponent<EventTrigger>();
                et.triggers.Clear();

                EventTrigger.Entry e = new EventTrigger.Entry();
                e.callback.AddListener((eventData) =>
                {
                    if (!_connector.Initialized) return;

                    if (_connector.Active) _connector.Disable();
                    else _connector.Enable();
                });
                et.triggers.Add(e);

                Logger.LogInfo("Added main menu button.");
            }

            _mainMenuText.text = _connector.Active ? $"Disable Multiplayer [{Config.ConnectionType}]" : $"Enable Multiplayer [{Config.ConnectionType}]";
        }

        private void UpdateGameIndicator()
        {
            // Solo mostrar el indicador si no estamos en el menú principal
            bool inGame = !_mainMenu || !_mainMenu.activeInHierarchy;
            
            if (inGame && _connector.Initialized)
            {
                // Crear el indicador si no existe
                if (!_gameIndicator)
                {
                    CreateGameIndicator();
                }
                
                // Actualizar el color según el estado
                if (_gameIndicatorImage)
                {
                    _gameIndicatorImage.color = _connector.Active ? new Color(0.2f, 0.8f, 0.2f, 0.9f) : new Color(0.8f, 0.2f, 0.2f, 0.9f);
                    _gameIndicator.SetActive(true);
                }
                
                // Detectar cambio de estado automático
                if (_lastConnectorState != _connector.Active)
                {
                    _lastConnectorState = _connector.Active;
                }
                
                // Manejar texto temporal
                UpdateStatusText();
            }
            else
            {
                // Ocultar el indicador en menús
                if (_gameIndicator)
                {
                    _gameIndicator.SetActive(false);
                }
            }
        }

        private void CreateGameIndicator()
        {
            // Buscar o crear un Canvas para la UI del juego
            _gameCanvas = FindFirstObjectByType<Canvas>();
            if (!_gameCanvas)
            {
                GameObject canvasObj = new GameObject("SilklessCoopCanvas");
                _gameCanvas = canvasObj.AddComponent<Canvas>();
                _gameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _gameCanvas.sortingOrder = 1000; // Asegurar que esté encima de otros elementos
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // Crear el GameObject del indicador
            _gameIndicator = new GameObject("MultiplayerStatusIndicator");
            _gameIndicator.transform.SetParent(_gameCanvas.transform, false);

            // Agregar componente Image
            _gameIndicatorImage = _gameIndicator.AddComponent<UnityEngine.UI.Image>();
            
            // Crear sprite desde SVG
            Texture2D svgTexture = CreateSVGTexture();
            Sprite svgSprite = Sprite.Create(svgTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            _gameIndicatorImage.sprite = svgSprite;
            
            // Configurar posición (esquina superior derecha)
            RectTransform rectTransform = _gameIndicator.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = new Vector2(-25, -25);
            rectTransform.sizeDelta = new Vector2(24, 24);
            
            // Crear texto de estado
            CreateStatusText();

            Logger.LogInfo("Created game multiplayer status indicator.");
        }

        private void CreateStatusText()
        {
            _statusText = new GameObject("MultiplayerStatusText");
            _statusText.transform.SetParent(_gameCanvas.transform, false);
            
            _statusTextComponent = _statusText.AddComponent<UnityEngine.UI.Text>();
            _statusTextComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _statusTextComponent.fontSize = 12;
            _statusTextComponent.color = new Color(1f, 1f, 1f, 0f); // Inicialmente transparente
            _statusTextComponent.alignment = TextAnchor.MiddleCenter;
            _statusTextComponent.text = "";
            
            // Configurar posición (debajo del indicador)
            RectTransform textRect = _statusText.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(1, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(1, 1);
            textRect.anchoredPosition = new Vector2(-25, -55);
            textRect.sizeDelta = new Vector2(80, 20);
            
            _statusText.SetActive(false);
        }
        
        private void ShowStatusText(string status)
        {
            if (_statusTextComponent)
            {
                _statusTextComponent.text = status;
                _statusTextComponent.color = new Color(1f, 1f, 1f, 1f);
                _statusText.SetActive(true);
                _statusTextTimer = 2f; // Mostrar por 2 segundos
            }
        }
        
        private void UpdateStatusText()
        {
            if (_statusTextTimer > 0)
            {
                _statusTextTimer -= Time.unscaledDeltaTime;
                
                // Fade out en el último medio segundo
                if (_statusTextTimer <= 0.5f)
                {
                    float alpha = _statusTextTimer / 0.5f;
                    _statusTextComponent.color = new Color(1f, 1f, 1f, alpha);
                }
                
                if (_statusTextTimer <= 0)
                {
                    _statusText.SetActive(false);
                }
            }
        }
        
        private Texture2D CreateSVGTexture()
        {
            // Ruta al archivo SVG
            string svgPath = Path.Combine(Application.dataPath, "..", "Assets", "multiplayer_indicator.svg");
            
            // Crear una textura basada en el diseño del SVG
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color pixelColor = Color.clear;
                    
                    // Círculo exterior (borde)
                    if (distance <= 14 && distance >= 12)
                    {
                        pixelColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    }
                    // Círculo interior
                    else if (distance <= 12 && distance >= 10)
                    {
                        pixelColor = new Color(0.18f, 0.18f, 0.18f, 0.9f);
                    }
                    // Área central
                    else if (distance <= 10)
                    {
                        pixelColor = new Color(0.18f, 0.18f, 0.18f, 0.7f);
                        
                        // Agregar detalles del símbolo cooperativo
                        float dx = x - center.x;
                        float dy = y - center.y;
                        
                        // Figuras conectadas (simplificado)
                        if ((Mathf.Abs(dx + 3) < 1.5f && Mathf.Abs(dy) < 3) || 
                            (Mathf.Abs(dx - 3) < 1.5f && Mathf.Abs(dy) < 3) ||
                            (Mathf.Abs(dy - 1) < 1 && Mathf.Abs(dx) < 4))
                        {
                            pixelColor = Color.white;
                        }
                    }
                    
                    pixels[y * size + x] = pixelColor;
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
