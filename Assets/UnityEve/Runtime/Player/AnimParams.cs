using UnityEngine;

namespace Unity.Eve
{
    /// <summary>Animator parameter hashes shared by the controllers the content builder generates.</summary>
    public static class AnimParams
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int InBattle = Animator.StringToHash("InBattle");
        public static readonly int Searching = Animator.StringToHash("Searching");
        public static readonly int Shoot = Animator.StringToHash("Shoot");
        public static readonly int Hit = Animator.StringToHash("Hit");
        public static readonly int Dodge = Animator.StringToHash("Dodge");
        public static readonly int Cast = Animator.StringToHash("Cast");
        public static readonly int Throw = Animator.StringToHash("Throw");
        public static readonly int Die = Animator.StringToHash("Die");
        public static readonly int Revive = Animator.StringToHash("Revive");
        public static readonly int Attack = Animator.StringToHash("Attack");
        public static readonly int Slash = Animator.StringToHash("Slash");
        public static readonly int Rush = Animator.StringToHash("Rush");
        public static readonly int Backpedal = Animator.StringToHash("Backpedal");
        public static readonly int Shove = Animator.StringToHash("Shove");
    }
}
