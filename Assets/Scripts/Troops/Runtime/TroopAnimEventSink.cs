using UnityEngine;

namespace KoG.MiniMvp.Troops
{
    /// <summary>
    /// Animation Event sink. Wire clip events to Anim_AttackHit / Anim_DeathEnd.
    /// Works without an Animator (TroopActor falls back to timed combat).
    /// </summary>
    public sealed class TroopAnimEventSink : MonoBehaviour
    {
        TroopActor _owner;

        public void Bind(TroopActor owner) => _owner = owner;

        /// <summary>Animation Event name: Anim_AttackHit</summary>
        public void Anim_AttackHit() => _owner?.NotifyAnimAttackHit();

        /// <summary>Animation Event name: Anim_DeathEnd</summary>
        public void Anim_DeathEnd() => _owner?.NotifyAnimDeathEnd();

        /// <summary>Animation Event name: Anim_Footstep (optional SFX hook)</summary>
        public void Anim_Footstep() { }
    }
}
