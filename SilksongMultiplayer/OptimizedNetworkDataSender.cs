// Optimized NetworkDataSender with change detection and data compression
// Versión optimizada del NetworkDataSender con detección de cambios y compresión de datos

using Steamworks;
using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

namespace SilksongMultiplayer
{
    internal class OptimizedNetworkDataSender
    {
        // Initialize message pooling
        static OptimizedNetworkDataSender()
        {
            NetworkMessagePool.WarmUpPools();
        }
        // Change detection for position data
        private Vector3 lastSentPosition = Vector3.zero;
        private float lastSentScale = 0f;
        private const float POSITION_THRESHOLD = 0.05f; // Only send if moved more than 5cm
        private const float SCALE_THRESHOLD = 0.01f;
        
        // Change detection for map position
        private Vector2 lastSentCompassPos = Vector2.zero;
        private Vector2 lastSentWideCompassPos = Vector2.zero;
        private const float COMPASS_THRESHOLD = 0.1f;
        
        // Animation state tracking
        private string lastSentAnimation = "";
        private int lastSentExtraValue = -999;
        
        // Message pooling for better performance
        private readonly Queue<byte[]> messagePool = new Queue<byte[]>();
        private const int POOL_SIZE = 20;
        
        public OptimizedNetworkDataSender()
        {
            // Pre-allocate message buffers
            for (int i = 0; i < POOL_SIZE; i++)
            {
                messagePool.Enqueue(new byte[64]); // Most messages are smaller than 64 bytes
            }
        }
        
        private byte[] GetPooledBuffer(int minSize)
        {
            if (messagePool.Count > 0)
            {
                byte[] buffer = messagePool.Dequeue();
                if (buffer.Length >= minSize)
                    return buffer;
            }
            return new byte[Math.Max(minSize, 64)];
        }
        
        private void ReturnToPool(byte[] buffer)
        {
            if (messagePool.Count < POOL_SIZE)
            {
                messagePool.Enqueue(buffer);
            }
        }
        
        public bool SendPositionData(Vector3 position, float scale)
        {
            // Only send if position or scale changed significantly
            if (Vector3.Distance(position, lastSentPosition) < POSITION_THRESHOLD &&
                Mathf.Abs(scale - lastSentScale) < SCALE_THRESHOLD)
            {
                return false; // No significant change, don't send
            }
            
            lastSentPosition = position;
            lastSentScale = scale;
            
            // Use pooled message object
            var positionMessage = NetworkMessagePool.GetPositionMessage();
            try
            {
                byte[] buffer = GetPooledBuffer(21);
                
                // Message type
                buffer[0] = 1;
                
                // Position data (compressed to reduce precision slightly)
                BitConverter.GetBytes(RoundToDecimal(position.x, 2)).CopyTo(buffer, 1);
                BitConverter.GetBytes(RoundToDecimal(position.y, 2)).CopyTo(buffer, 5);
                BitConverter.GetBytes(RoundToDecimal(position.z, 2)).CopyTo(buffer, 9);
                BitConverter.GetBytes(RoundToDecimal(scale, 2)).CopyTo(buffer, 13);
                
                bool sent = SendToAllPlayers(buffer, 17, EP2PSend.k_EP2PSendUnreliable);
                ReturnToPool(buffer);
                
                return sent;
            }
            finally
            {
                NetworkMessagePool.ReturnPositionMessage(positionMessage);
            }
        }
        
        public bool SendMapPositionData(Vector2 compassPos, Vector2 wideCompassPos)
        {
            // Only send if compass positions changed significantly
            if (Vector2.Distance(compassPos, lastSentCompassPos) < COMPASS_THRESHOLD &&
                Vector2.Distance(wideCompassPos, lastSentWideCompassPos) < COMPASS_THRESHOLD)
            {
                return false;
            }
            
            lastSentCompassPos = compassPos;
            lastSentWideCompassPos = wideCompassPos;
            
            // Use pooled message object
            var mapMessage = NetworkMessagePool.GetMapPositionMessage();
            try
            {
                byte[] buffer = GetPooledBuffer(17);
                
                buffer[0] = 5; // Message type
                
                BitConverter.GetBytes(compassPos.x).CopyTo(buffer, 1);
                BitConverter.GetBytes(compassPos.y).CopyTo(buffer, 5);
                BitConverter.GetBytes(wideCompassPos.x).CopyTo(buffer, 9);
                BitConverter.GetBytes(wideCompassPos.y).CopyTo(buffer, 13);
                
                bool sent = SendToAllPlayers(buffer, 17, EP2PSend.k_EP2PSendUnreliable);
                ReturnToPool(buffer);
                
                return sent;
            }
            finally
            {
                NetworkMessagePool.ReturnMapPositionMessage(mapMessage);
            }
        }
        
        public bool SendAnimationData(string animationName, int extraValue)
        {
            // Only send if animation changed
            if (animationName == lastSentAnimation && extraValue == lastSentExtraValue)
            {
                return false;
            }
            
            lastSentAnimation = animationName;
            lastSentExtraValue = extraValue;
            
            // Use pooled message object
            var animationMessage = NetworkMessagePool.GetAnimationMessage();
            try
            {
                byte[] animBytes = Encoding.UTF8.GetBytes(animationName);
                if (animBytes.Length > 255) return false; // Animation name too long
                
                int totalSize = 1 + 1 + animBytes.Length + 4;
                byte[] buffer = GetPooledBuffer(totalSize);
                
                buffer[0] = 2; // Message type
                buffer[1] = (byte)animBytes.Length;
                
                Array.Copy(animBytes, 0, buffer, 2, animBytes.Length);
                BitConverter.GetBytes(extraValue).CopyTo(buffer, 2 + animBytes.Length);
                
                bool sent = SendToAllPlayers(buffer, totalSize, EP2PSend.k_EP2PSendReliable);
                ReturnToPool(buffer);
                
                return sent;
            }
            finally
            {
                NetworkMessagePool.ReturnAnimationMessage(animationMessage);
            }
        }
        
        public void SendMapChangeNotification(string mapName)
        {
            byte[] mapBytes = Encoding.UTF8.GetBytes(mapName);
            if (mapBytes.Length > 255) return;
            
            int totalSize = 2 + mapBytes.Length;
            byte[] buffer = GetPooledBuffer(totalSize);
            
            buffer[0] = 3; // Message type
            buffer[1] = (byte)mapBytes.Length;
            Array.Copy(mapBytes, 0, buffer, 2, mapBytes.Length);
            
            SendToAllPlayers(buffer, totalSize, EP2PSend.k_EP2PSendReliable);
            ReturnToPool(buffer);
        }
        
        public void SendTargetHeroTakeDamageData(ulong targetSteamId, int damage, int direction, int hazardType, int attackTypes)
        {
            byte[] buffer = GetPooledBuffer(25);
            
            buffer[0] = 4; // Message type
            BitConverter.GetBytes(targetSteamId).CopyTo(buffer, 1);
            BitConverter.GetBytes(damage).CopyTo(buffer, 9);
            BitConverter.GetBytes(direction).CopyTo(buffer, 13);
            BitConverter.GetBytes(hazardType).CopyTo(buffer, 17);
            BitConverter.GetBytes(attackTypes).CopyTo(buffer, 21);
            
            SendToAllPlayers(buffer, 25, EP2PSend.k_EP2PSendReliable);
            ReturnToPool(buffer);
        }
        
        public void SendHeroAttackAnimationData(string parentName, string name, string animationName)
        {
            byte[] parentBytes = Encoding.UTF8.GetBytes(parentName);
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            byte[] animBytes = Encoding.UTF8.GetBytes(animationName);
            
            if (parentBytes.Length > 255 || nameBytes.Length > 255 || animBytes.Length > 255)
                return;
            
            int totalSize = 4 + parentBytes.Length + nameBytes.Length + animBytes.Length;
            byte[] buffer = GetPooledBuffer(totalSize);
            
            int offset = 0;
            buffer[offset++] = 6; // Message type
            buffer[offset++] = (byte)parentBytes.Length;
            buffer[offset++] = (byte)nameBytes.Length;
            buffer[offset++] = (byte)animBytes.Length;
            
            Array.Copy(parentBytes, 0, buffer, offset, parentBytes.Length);
            offset += parentBytes.Length;
            Array.Copy(nameBytes, 0, buffer, offset, nameBytes.Length);
            offset += nameBytes.Length;
            Array.Copy(animBytes, 0, buffer, offset, animBytes.Length);
            
            SendToAllPlayers(buffer, totalSize, EP2PSend.k_EP2PSendReliable);
            ReturnToPool(buffer);
        }
        
        private bool SendToAllPlayers(byte[] data, int length, EP2PSend sendType)
        {
            bool anySent = false;
            
            foreach (CSteamID roomMember in SilksongMultiplayerAPI.GetRoomMembers())
            {
                if (CSteamID.op_Inequality(roomMember, SteamUser.GetSteamID()))
                {
                    if (SteamNetworking.SendP2PPacket(roomMember, data, (uint)length, sendType, 0))
                    {
                        anySent = true;
                    }
                }
            }
            
            return anySent;
        }
        
        // Helper method to reduce float precision for network compression
        private float RoundToDecimal(float value, int decimals)
        {
            float multiplier = Mathf.Pow(10f, decimals);
            return Mathf.Round(value * multiplier) / multiplier;
        }
        
        // Reset change detection when needed
        public void ResetChangeDetection()
        {
            lastSentPosition = Vector3.zero;
            lastSentScale = 0f;
            lastSentCompassPos = Vector2.zero;
            lastSentWideCompassPos = Vector2.zero;
            lastSentAnimation = "";
            lastSentExtraValue = -999;
        }
    }
}