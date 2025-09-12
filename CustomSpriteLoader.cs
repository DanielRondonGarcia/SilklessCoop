using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;

namespace SilklessCoop
{
    /// <summary>
    /// Sistema para cargar sprites personalizados desde archivos externos
    /// Permite integrar sprites del Knight descargados de internet
    /// </summary>
    public class CustomSpriteLoader : MonoBehaviour
    {
        private static CustomSpriteLoader _instance;
        public static CustomSpriteLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("CustomSpriteLoader");
                    _instance = go.AddComponent<CustomSpriteLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private ManualLogSource _logger;
        private Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>();
        private Dictionary<string, Sprite> _loadedSprites = new Dictionary<string, Sprite>();
        private bool _knightSpritesLoaded = false;

        // Rutas de sprites del Knight
        private readonly string KNIGHT_SPRITES_PATH = "Assets/The_Knight";
        private readonly string KNIGHT_MAIN_ATLAS = "knight_main_sprites_atlas0.png";

        void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _logger = BepInEx.Logging.Logger.CreateLogSource("CustomSpriteLoader");
        }

        void Start()
        {
            LoadKnightSprites();
        }

        /// <summary>
        /// Carga los sprites del Knight desde los archivos descargados
        /// </summary>
        public void LoadKnightSprites()
        {
            StartCoroutine(LoadKnightSpritesCoroutine());
        }

        /// <summary>
        /// Corrutina interna para cargar los sprites del Knight
        /// </summary>
        private IEnumerator LoadKnightSpritesCoroutine()
        {
            // Debug: Mostrar información de rutas
            if (_logger != null)
            {
                _logger.LogInfo($"Application.dataPath: {Application.dataPath}");
                _logger.LogInfo($"KNIGHT_SPRITES_PATH: {KNIGHT_SPRITES_PATH}");
                _logger.LogInfo($"KNIGHT_MAIN_ATLAS: {KNIGHT_MAIN_ATLAS}");
            }

            string spritesPath = Path.Combine(Application.dataPath, "..", KNIGHT_SPRITES_PATH);
            string mainAtlasPath = Path.Combine(spritesPath, KNIGHT_MAIN_ATLAS);
            
            // Normalizar la ruta para evitar problemas con separadores
            mainAtlasPath = Path.GetFullPath(mainAtlasPath);

            if (_logger != null)
            {
                _logger.LogInfo($"Ruta construida spritesPath: {spritesPath}");
                _logger.LogInfo($"Ruta completa mainAtlasPath: {mainAtlasPath}");
                _logger.LogInfo($"¿Existe el archivo? {File.Exists(mainAtlasPath)}");
            }

            if (!File.Exists(mainAtlasPath))
            {
                if (_logger != null)
                {
                    _logger.LogWarning($"No se encontró el atlas principal del Knight en: {mainAtlasPath}");
                    
                    // Intentar rutas alternativas
                    string alternativePath1 = Path.Combine(Application.dataPath, KNIGHT_SPRITES_PATH, KNIGHT_MAIN_ATLAS);
                    string alternativePath2 = Path.Combine(Directory.GetCurrentDirectory(), KNIGHT_SPRITES_PATH, KNIGHT_MAIN_ATLAS);
                    // Ruta directa al directorio del proyecto (donde están realmente los archivos)
                    string alternativePath3 = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), KNIGHT_SPRITES_PATH, KNIGHT_MAIN_ATLAS);
                    
                    _logger.LogInfo($"Probando ruta alternativa 1: {alternativePath1} - Existe: {File.Exists(alternativePath1)}");
                    _logger.LogInfo($"Probando ruta alternativa 2: {alternativePath2} - Existe: {File.Exists(alternativePath2)}");
                    _logger.LogInfo($"Probando ruta alternativa 3: {alternativePath3} - Existe: {File.Exists(alternativePath3)}");
                    
                    if (File.Exists(alternativePath1))
                    {
                        mainAtlasPath = alternativePath1;
                        _logger.LogInfo($"Usando ruta alternativa 1: {mainAtlasPath}");
                    }
                    else if (File.Exists(alternativePath2))
                    {
                        mainAtlasPath = alternativePath2;
                        _logger.LogInfo($"Usando ruta alternativa 2: {mainAtlasPath}");
                    }
                    else if (File.Exists(alternativePath3))
                    {
                        mainAtlasPath = alternativePath3;
                        _logger.LogInfo($"Usando ruta alternativa 3: {mainAtlasPath}");
                    }
                    else
                    {
                        _logger.LogError("No se pudo encontrar el archivo knight_main_sprites_atlas0.png en ninguna de las rutas probadas.");
                        _logger.LogError("Asegúrate de que el archivo esté en la carpeta 'Assets/The_Knight' dentro del directorio del juego.");
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }
            }

            // Cargar la textura principal del Knight
            yield return StartCoroutine(LoadTextureFromFile(mainAtlasPath, "knight_main"));

            if (_loadedTextures.ContainsKey("knight_main"))
            {
                try
                {
                    // Crear sprites individuales del atlas
                    CreateKnightSprites(_loadedTextures["knight_main"]);
                    _knightSpritesLoaded = true;
                    
                    if (_logger != null)
                        _logger.LogInfo("Sprites del Knight cargados exitosamente");
                }
                catch (Exception ex)
                {
                    if (_logger != null)
                        _logger.LogError($"Error al crear sprites del Knight: {ex.Message}");
                }
            }
            else
            {
                if (_logger != null)
                    _logger.LogError("Error al cargar la textura principal del Knight");
            }
        }

        /// <summary>
        /// Carga una textura desde un archivo
        /// </summary>
        private IEnumerator LoadTextureFromFile(string filePath, string textureName)
        {
            if (!File.Exists(filePath))
            {
                if (_logger != null)
                    _logger.LogError($"Archivo no encontrado: {filePath}");
                yield break;
            }

            try
            {
                // Leer los bytes del archivo
                byte[] fileData = File.ReadAllBytes(filePath);
                
                if (_logger != null)
                    _logger.LogInfo($"Archivo leído: {fileData.Length} bytes");
                
                // Crear textura con formato específico
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                
                // Cargar la imagen
                if (texture.LoadImage(fileData))
                {
                    texture.name = textureName;
                    _loadedTextures[textureName] = texture;
                    
                    if (_logger != null)
                        _logger.LogInfo($"Textura cargada exitosamente: {textureName} ({texture.width}x{texture.height})");
                }
                else
                {
                    if (_logger != null)
                    {
                        _logger.LogError($"LoadImage falló para: {filePath}");
                        _logger.LogError($"Tamaño del archivo: {fileData.Length} bytes");
                        _logger.LogError($"Primeros 10 bytes: {string.Join(", ", fileData.Take(Math.Min(10, fileData.Length)))}");
                    }
                    Destroy(texture);
                }
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger.LogError($"Excepción cargando textura {filePath}: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            
            yield return null;
        }

        /// <summary>
        /// Crea sprites individuales del Knight desde el atlas principal
        /// </summary>
        private void CreateKnightSprites(Texture2D atlasTexture)
        {
            try
            {
                if (_logger != null)
                    _logger.LogInfo($"Creating Knight sprites from atlas: {atlasTexture.width}x{atlasTexture.height}");
                
                // Coordenadas más realistas para el atlas del Knight
                // Basado en el atlas típico de Hollow Knight donde los sprites están organizados en filas
                
                // Sprite idle del Knight - primera fila, primera columna
                Rect idleRect = new Rect(0, atlasTexture.height - 128, 128, 128); // 128x128 pixels
                Sprite idleSprite = Sprite.Create(atlasTexture, idleRect, new Vector2(0.5f, 0.5f), 100.0f);
                idleSprite.name = "knight_idle";
                _loadedSprites["knight_idle"] = idleSprite;
                
                if (_logger != null)
                    _logger.LogInfo($"Created knight_idle sprite: {idleRect}");

                // Sprite de caminar - segunda columna
                Rect walkRect = new Rect(128, atlasTexture.height - 128, 128, 128);
                Sprite walkSprite = Sprite.Create(atlasTexture, walkRect, new Vector2(0.5f, 0.5f), 100.0f);
                walkSprite.name = "knight_walk";
                _loadedSprites["knight_walk"] = walkSprite;
                
                if (_logger != null)
                    _logger.LogInfo($"Created knight_walk sprite: {walkRect}");
                
                // Sprite de salto - tercera columna
                Rect jumpRect = new Rect(256, atlasTexture.height - 128, 128, 128);
                Sprite jumpSprite = Sprite.Create(atlasTexture, jumpRect, new Vector2(0.5f, 0.5f), 100.0f);
                jumpSprite.name = "knight_jump";
                _loadedSprites["knight_jump"] = jumpSprite;
                
                if (_logger != null)
                    _logger.LogInfo($"Created knight_jump sprite: {jumpRect}");

                if (_logger != null)
                    _logger.LogInfo($"Creados {_loadedSprites.Count} sprites del Knight");
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger.LogError($"Error creando sprites del Knight: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtiene un sprite del Knight por nombre
        /// </summary>
        public Sprite GetKnightSprite(string spriteName)
        {
            if (_loadedSprites.ContainsKey(spriteName))
            {
                return _loadedSprites[spriteName];
            }
            
            if (_logger != null)
                _logger.LogWarning($"Sprite del Knight no encontrado: {spriteName}");
            return null;
        }

        /// <summary>
        /// Verifica si los sprites del Knight están cargados
        /// </summary>
        public bool AreKnightSpritesLoaded()
        {
            return _knightSpritesLoaded;
        }

        /// <summary>
        /// Aplica un sprite del Knight a un tk2dSprite
        /// </summary>
        public bool ApplyKnightSpriteToTk2d(tk2dSprite tk2dSprite, string spriteName)
        {
            if (!_knightSpritesLoaded)
            {
                if (_logger != null)
                    _logger.LogWarning("Los sprites del Knight aún no están cargados");
                return false;
            }

            Sprite knightSprite = GetKnightSprite(spriteName);
            if (knightSprite == null)
            {
                if (_logger != null)
                    _logger.LogError($"No se pudo obtener el sprite: {spriteName}");
                return false;
            }

            try
            {
                if (_logger != null)
                    _logger.LogInfo($"Aplicando sprite {spriteName} - Rect: {knightSprite.rect}, Texture: {knightSprite.texture.width}x{knightSprite.texture.height}");
                
                // Obtener componentes necesarios
                Renderer spriteRenderer = tk2dSprite.GetComponent<Renderer>();
                MeshFilter meshFilter = tk2dSprite.GetComponent<MeshFilter>();
                
                if (spriteRenderer == null || meshFilter == null)
                {
                    if (_logger != null)
                        _logger.LogError("No se encontraron componentes Renderer o MeshFilter");
                    return false;
                }
                
                // Crear un nuevo material con la textura del Knight
                Material knightMaterial = new Material(spriteRenderer.material.shader);
                knightMaterial.mainTexture = knightSprite.texture;
                
                // Copiar propiedades importantes del material original
                knightMaterial.color = spriteRenderer.material.color;
                
                // Aplicar el material
                spriteRenderer.material = knightMaterial;
                
                // Ajustar las coordenadas UV para mostrar el sprite correcto
                Mesh mesh = meshFilter.mesh;
                if (mesh == null)
                {
                    if (_logger != null)
                        _logger.LogError("Mesh es null");
                    return false;
                }
                
                Vector2[] uvs = new Vector2[mesh.vertexCount];
                
                Rect spriteRect = knightSprite.rect;
                float texWidth = knightSprite.texture.width;
                float texHeight = knightSprite.texture.height;
                
                // Calcular UVs normalizados
                float uMin = spriteRect.x / texWidth;
                float uMax = (spriteRect.x + spriteRect.width) / texWidth;
                float vMin = spriteRect.y / texHeight;
                float vMax = (spriteRect.y + spriteRect.height) / texHeight;
                
                if (_logger != null)
                    _logger.LogInfo($"UV coords - uMin: {uMin}, uMax: {uMax}, vMin: {vMin}, vMax: {vMax}");
                
                // Asignar UVs (asumiendo quad estándar)
                if (uvs.Length >= 4)
                {
                    uvs[0] = new Vector2(uMin, vMin); // bottom-left
                    uvs[1] = new Vector2(uMax, vMin); // bottom-right
                    uvs[2] = new Vector2(uMin, vMax); // top-left
                    uvs[3] = new Vector2(uMax, vMax); // top-right
                }
                
                mesh.uv = uvs;
                
                if (_logger != null)
                    _logger.LogInfo($"Sprite del Knight aplicado exitosamente: {spriteName}");
                
                return true;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                    _logger.LogError($"Error aplicando sprite del Knight: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return false;
            }
        }

        void OnDestroy()
        {
            // Limpiar texturas cargadas
            foreach (var texture in _loadedTextures.Values)
            {
                if (texture != null)
                    Destroy(texture);
            }
            _loadedTextures.Clear();
            
            foreach (var sprite in _loadedSprites.Values)
            {
                if (sprite != null)
                    Destroy(sprite);
            }
            _loadedSprites.Clear();
        }
    }
}