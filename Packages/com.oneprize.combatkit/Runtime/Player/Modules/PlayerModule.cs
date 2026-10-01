using UnityEngine;

namespace CombatSystem.Player
{
    /// <summary>
    /// Base for a player feature. Modules are plain C# objects, not components:
    /// PlayerCharacter creates them, owns their lifetime and calls them in a fixed order.
    /// </summary>
    public abstract class PlayerModule
    {
        protected PlayerContext Ctx { get; private set; }
        protected PlayerCharacter Player => Ctx.Character;
        // Same names as the MonoBehaviour members the module code was written against.
        protected Transform transform => Ctx.Transform;
        protected Animator animator => Ctx.Animator;
        protected CharacterController character => Ctx.Controller;

        internal void Bind(PlayerContext context)
        {
            Ctx = context;
            OnInitialize();
        }

        /// <summary>Called once from PlayerCharacter.Awake, after every module exists.</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Called when the PlayerCharacter is disabled; drop any in-progress action.</summary>
        public virtual void OnDisable() { }
    }
}
