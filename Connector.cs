using BepInEx.Logging;
using UnityEngine;

namespace SilklessCoop
{
    public enum GameRole
    {
        HOST,    // Controla el mundo
        CLIENT   // Solo recibe actualizaciones
    }
    
    internal abstract class Connector : MonoBehaviour
    {
        public ManualLogSource Logger;
        public ModConfig Config;

        public bool Initialized;
        public bool Active;
        
        // Arquitectura Host-Cliente
        public GameRole Role { get; protected set; } = GameRole.HOST; // Por defecto HOST
        public bool IsHost => Role == GameRole.HOST;
        public bool IsClient => Role == GameRole.CLIENT;

        protected GameSync _sync;

        protected float _tickTimeout;

        public abstract string GetName();

        protected void Start()
        {
            _sync = gameObject.GetComponent<GameSync>();
            _tickTimeout = 0;
        }

        public virtual bool Init()
        {
            Initialized = true;
            return true;
        }

        protected virtual void Update()
        {
            if (!Active) return;

            if (_tickTimeout >= 0)
            {
                _tickTimeout -= Time.unscaledDeltaTime;

                if (_tickTimeout <= 0)
                {
                    Tick();
                    _tickTimeout = 1.0f / Config.TickRate;
                }
            }
        }

        public virtual void Enable()
        {
            Active = true;
        }
        public virtual void Disable()
        {
            Active = false;
            _sync.Reset();
        }

        protected virtual void Tick()
        {

        }
    }
}
