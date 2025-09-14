using Steamworks;
using UnityEngine;
using UnityEngine.UI;

namespace SilklessCoop.Core
{
    // This is a placeholder class for the real tk2dSprite, which exists in the game's compiled DLLs.
    // This allows our code to compile. At runtime, the game's engine will use the real class.
    public class tk2dSprite : MonoBehaviour
    {
        public Color color;
        public void CopyFrom(tk2dSprite other) {}
        public void Build() {}
    }

    public class PlayerAvatar : MonoBehaviour
    {
        private CSteamID steamID;
        private tk2dSprite sprite;
        private Canvas nameplateCanvas;
        private Text nameplateText;

        private Vector3 targetPosition;
        private const float INTERPOLATION_SPEED = 15f;

        private bool isInitialized = false;

        public void Initialize(CSteamID id)
        {
            this.steamID = id;
            this.targetPosition = transform.position;

            // Add the tk2dSprite component. PlayerSyncManager will copy the details into this.
            this.sprite = gameObject.AddComponent<tk2dSprite>();

            // --- Create Nameplate ---
            var canvasObj = new GameObject("NameplateCanvas");
            canvasObj.transform.SetParent(this.transform, false);
            canvasObj.transform.localPosition = new Vector3(0, 1.5f, 0);

            this.nameplateCanvas = canvasObj.AddComponent<Canvas>();
            this.nameplateCanvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(3, 1);
            // Scale down the canvas so the text is not huge
            canvasRect.localScale = new Vector3(0.02f, 0.02f, 0.02f);

            var textObj = new GameObject("NameplateText");
            textObj.transform.SetParent(canvasObj.transform, false);

            this.nameplateText = textObj.AddComponent<Text>();
            this.nameplateText.text = SteamFriends.GetFriendPersonaName(this.steamID);
            this.nameplateText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            this.nameplateText.fontSize = 14;
            this.nameplateText.alignment = TextAnchor.MiddleCenter;
            this.nameplateText.color = Color.white;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(150, 50);

            isInitialized = true;
        }

        public void UpdateState(Vector3 position, bool facingRight)
        {
            if (!isInitialized) return;

            this.targetPosition = position;
            transform.localScale = new Vector3(facingRight ? 1 : -1, 1, 1);
        }

        void Update()
        {
            if (!isInitialized) return;

            // Smoothly move towards the target position
            transform.position = Vector3.Lerp(transform.position, this.targetPosition, Time.deltaTime * INTERPOLATION_SPEED);

            // Counter-scale the nameplate so it doesn't get mirrored
            if (this.nameplateCanvas != null)
            {
                this.nameplateCanvas.transform.localScale = new Vector3(
                    transform.localScale.x > 0 ? 1 : -1,
                    1,
                    1
                );
            }
        }

        public void SetVisible(bool visible)
        {
            if (!isInitialized) return;

            if (this.sprite != null)
            {
                var newColor = this.sprite.color;
                newColor.a = visible ? 0.7f : 0f; // Use opacity from config
                this.sprite.color = newColor;
            }

            if (this.nameplateCanvas != null)
            {
                this.nameplateCanvas.gameObject.SetActive(visible);
            }
        }

        public tk2dSprite GetSprite()
        {
            return this.sprite;
        }
    }
}
