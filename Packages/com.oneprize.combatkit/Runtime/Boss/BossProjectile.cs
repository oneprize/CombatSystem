using UnityEngine;
using CombatSystem.Player;

namespace CombatSystem.Boss
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class BossProjectile : MonoBehaviour
    {
        private BossCombat owner;
        private bool resolved;
        public bool Resolved => resolved;

        public void Initialize(BossCombat boss) { owner = boss; }

        private void OnCollisionEnter(Collision collision)
        {
            if (resolved) return;
            resolved = true;
            var player = collision.collider.GetComponentInParent<PlayerCharacter>();
            if (player != null)
                player.Health.ReceiveHit(3, owner != null ? owner.transform : transform, knockDown: true);
            Destroy(gameObject);
        }
    }
}
