using UnityEngine;

namespace Demo.Combat
{
    /// <summary>
    /// 战斗事件契约:核心逻辑通过 EventBus 发布,表现层(镜头/音效/特效/UI)订阅。
    /// 全部用 readonly struct 作为事件载体,避免每次发布产生 GC 分配。
    /// </summary>

    /// <summary>命中事件:攻击方 hitbox 与受击方 hurtbox 相交时发布。</summary>
    public readonly struct HitEvent
    {
        public readonly GameObject Attacker;
        public readonly GameObject Victim;
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitNormal;
        public readonly float Damage;
        public readonly bool IsHeavy; // 重击:用于区分镜头震动与音效强度

        public HitEvent(GameObject attacker, GameObject victim, Vector3 hitPoint, Vector3 hitNormal, float damage, bool isHeavy)
        {
            Attacker = attacker;
            Victim = victim;
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            Damage = damage;
            IsHeavy = isHeavy;
        }
    }

    /// <summary>弹反成功:Defender 完美格挡了 Attacker 的攻击。</summary>
    public readonly struct ParriedEvent
    {
        public readonly GameObject Defender;
        public readonly GameObject Attacker;

        public ParriedEvent(GameObject defender, GameObject attacker)
        {
            Defender = defender;
            Attacker = attacker;
        }
    }

    /// <summary>韧性条破防:Victim 的架势条被打空,进入可处决的大硬直。</summary>
    public readonly struct StaggerBreakEvent
    {
        public readonly GameObject Victim;

        public StaggerBreakEvent(GameObject victim)
        {
            Victim = victim;
        }
    }

    /// <summary>处决开始:Executor 对 Victim 执行处决演出。</summary>
    public readonly struct ExecutionEvent
    {
        public readonly GameObject Executor;
        public readonly GameObject Victim;

        public ExecutionEvent(GameObject executor, GameObject victim)
        {
            Executor = executor;
            Victim = victim;
        }
    }
}
