using BepInEx.Logging;
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
                    _gameIndicatorImage.color = _connector.Active ? Color.green : Color.red;
                    _gameIndicator.SetActive(true);
                }
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
            
            // Crear una textura simple para el círculo
            Texture2D circleTexture = CreateCircleTexture(32);
            Sprite circleSprite = Sprite.Create(circleTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            _gameIndicatorImage.sprite = circleSprite;
            
            // Configurar posición (esquina superior derecha)
            RectTransform rectTransform = _gameIndicator.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(1, 1);
            rectTransform.anchoredPosition = new Vector2(-20, -20);
            rectTransform.sizeDelta = new Vector2(20, 20);

            Logger.LogInfo("Created game multiplayer status indicator.");
        }

        private Texture2D CreateCircleTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 1;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= radius)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }
            
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
