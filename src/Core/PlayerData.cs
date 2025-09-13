using System;
using UnityEngine;
using Steamworks;

namespace SilklessCoop.Core
{
    [Serializable]
    public class PlayerData
    {
        public CSteamID steamID;
        public Vector3 position;
        public Vector3 velocity;
        public bool facingRight;
        public string currentScene;
        public float health;
        public bool isGrounded;
        public bool isAttacking;
        public bool isDashing;
        public bool isJumping;
        public Color playerColor;
        public long timestamp;
        
        public PlayerData()
        {
            steamID = CSteamID.Nil;
            position = Vector3.zero;
            velocity = Vector3.zero;
            facingRight = true;
            currentScene = "";
            health = 100f;
            isGrounded = true;
            isAttacking = false;
            isDashing = false;
            isJumping = false;
            playerColor = Color.white;
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        
        public PlayerData(CSteamID id, Vector3 pos, Vector3 vel, bool facing, string scene)
        {
            steamID = id;
            position = pos;
            velocity = vel;
            facingRight = facing;
            currentScene = scene;
            health = 100f;
            isGrounded = true;
            isAttacking = false;
            isDashing = false;
            isJumping = false;
            playerColor = GeneratePlayerColor(id);
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        
        private Color GeneratePlayerColor(CSteamID steamID)
        {
            // Generar un color único basado en el Steam ID
            var hash = steamID.GetHashCode();
            var r = ((hash & 0xFF0000) >> 16) / 255f;
            var g = ((hash & 0x00FF00) >> 8) / 255f;
            var b = (hash & 0x0000FF) / 255f;
            
            // Asegurar que el color sea visible (no muy oscuro)
            r = Mathf.Max(r, 0.3f);
            g = Mathf.Max(g, 0.3f);
            b = Mathf.Max(b, 0.3f);
            
            return new Color(r, g, b, 1f);
        }
        
        public byte[] ToBytes()
        {
            try
            {
                var json = JsonUtility.ToJson(this);
                return System.Text.Encoding.UTF8.GetBytes(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error serializing PlayerData: {ex.Message}");
                return new byte[0];
            }
        }
        
        public static PlayerData FromBytes(byte[] data)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                return JsonUtility.FromJson<PlayerData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing PlayerData: {ex.Message}");
                return new PlayerData();
            }
        }
        
        public bool IsValid()
        {
            return steamID != CSteamID.Nil && !string.IsNullOrEmpty(currentScene);
        }
        
        public float GetAge()
        {
            return (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp) / 1000f;
        }
    }
}