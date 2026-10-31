using Demo.Core;
using UnityEngine;

namespace Demo.Player
{
    /// <summary>
    /// 动画驱动层:把 PlayerCharacter 的状态(速度/着地/垂直速度)同步到 Animator 参数。
    /// 逻辑与表现分离:状态机不感知动画,动画层只读角色状态。
    /// 动画层转移日志:状态变化时按"上一帧参数快照"重建转移原因。
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private float speedDampTime = 0.08f; // 速度参数阻尼,避免走/跑状态来回抖动

        // 状态哈希常量:与 PlayerRigBuilder 生成的状态名一致(Animator.StringToHash 结果确定)
        private const int HashIdle = 0x7C161A2B;
        private const int HashWalk = 0x2DA3D56B;
        private const int HashRun = 0x683BE220;
        private const int HashJumpStart = 0x30DCAFB1;
        private const int HashFall = 0x5B627032;
        private const int HashLand = 0x08327AE6;

        private Animator _animator;
        private PlayerCharacter _character;
        private bool _jumpConsumed;
        private string _lastClipName = string.Empty;
        private int _lastStateHash;

        // 上一帧参数快照:Animator 的转移在本帧判定(参数由本帧 Set 后评估),
        // 状态变化在下一帧被检测到,此时用快照重建"为什么转移"
        private float _prevSpeed;
        private bool _prevGrounded;
        private float _prevVV;
        private bool _prevJumpTriggered;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int VerticalVelocityId = Animator.StringToHash("VerticalVelocity");
        private static readonly int JumpId = Animator.StringToHash("Jump");

        private void Awake()
        {
            // 预制体结构为"根 + 模型子物体",Animator 通常在模型子物体上;
            // 优先取带控制器的 Animator,避免取到根上的空 Animator
            _animator = GetComponent<Animator>();
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                foreach (var anim in GetComponentsInChildren<Animator>())
                {
                    if (anim.runtimeAnimatorController != null)
                    {
                        _animator = anim;
                        break;
                    }
                }
            }
            if (_animator == null)
                _animator = gameObject.AddComponent<Animator>();

            _character = GetComponent<PlayerCharacter>();

            var controllerName = _animator.runtimeAnimatorController != null
                ? _animator.runtimeAnimatorController.name
                : "(无控制器)";
            DemoLog.Log($"PlayerAnimator 绑定到:{_animator.gameObject.name},控制器:{controllerName}");
        }

        private void Update()
        {
            if (_character == null)
                return;

            var planarSpeed = new Vector3(_character.HorizontalVelocity.x, 0f, _character.HorizontalVelocity.z).magnitude;
            _animator.SetFloat(SpeedId, planarSpeed, speedDampTime, Time.deltaTime);
            _animator.SetBool(GroundedId, _character.IsGrounded);
            _animator.SetFloat(VerticalVelocityId, _character.VerticalVelocity);

            // 起跳上升段触发一次跳跃动画;落地后复位锁存
            var jumpTriggeredThisFrame = false;
            if (!_character.IsGrounded && _character.VerticalVelocity > 0.1f && !_jumpConsumed)
            {
                DemoLog.Log("设置 Jump 触发器(原因:离地且垂直速度>0.1,即起跳上升段)");
                _animator.SetTrigger(JumpId);
                _jumpConsumed = true;
                jumpTriggeredThisFrame = true;
            }
            else if (_character.IsGrounded)
            {
                _jumpConsumed = false;
            }

            // 动画状态变化日志:用上一帧参数快照重建转移原因
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash != _lastStateHash)
            {
                var reason = _lastStateHash == 0
                    ? "(初始化)"
                    : DeriveTransitionReason(_lastStateHash, stateInfo.shortNameHash,
                        _prevSpeed, _prevGrounded, _prevVV, _prevJumpTriggered);

                DemoLog.Log(
                    $"动画状态:{StateName(_lastStateHash)} -> {StateName(stateInfo.shortNameHash)},原因:{reason} " +
                    $"[判定用上一帧参数:Speed={_prevSpeed:F2}, Grounded={_prevGrounded}, VV={_prevVV:F2}]");
                _lastStateHash = stateInfo.shortNameHash;
            }

            // 当前播放剪辑变化日志
            var clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
            var clipName = clipInfo.Length > 0 ? clipInfo[0].clip.name : "(无剪辑)";
            if (clipName != _lastClipName)
            {
                DemoLog.Log($"动画剪辑:{_lastClipName} -> {clipName}");
                _lastClipName = clipName;
            }

            // 保存本帧参数快照,供下一帧转移原因判定
            _prevSpeed = planarSpeed;
            _prevGrounded = _character.IsGrounded;
            _prevVV = _character.VerticalVelocity;
            _prevJumpTriggered = jumpTriggeredThisFrame;
        }

        /// <summary>
        /// 按上一帧参数重建 Animator 转移原因。
        /// 转移规则与 PlayerRigBuilder.BuildLocomotionController 保持一致:
        ///   AnyState→JumpStart:Jump 触发器
        ///   AnyState→Fall:!Grounded 且 VV&lt;0(失足坠落)
        ///   JumpStart→Fall:VV&lt;0(越过最高点)
        ///   Fall→Land:Grounded
        ///   Land→Idle:exitTime=0.9(播完自动退出)
        ///   Idle↔Walk:Speed 阈值 0.1 | Walk↔Run:Speed 阈值 4.5
        /// </summary>
        private static string DeriveTransitionReason(int fromHash, int toHash,
            float prevSpeed, bool prevGrounded, float prevVV, bool prevJumpTriggered)
        {
            switch (toHash)
            {
                case HashJumpStart:
                    return prevJumpTriggered ? "Jump 触发器命中(起跳)" : "Jump 触发器(可能重复命中)";
                case HashFall:
                    return fromHash == HashJumpStart
                        ? $"垂直速度转负(VV={prevVV:F2}<0,越过最高点)"
                        : $"离地且 VV={prevVV:F2}<0(失去支撑/失足坠落)";
                case HashLand:
                    return $"着地判定成功(Grounded={prevGrounded},VV={prevVV:F2})";
                case HashIdle:
                    return fromHash == HashLand
                        ? "Land 播放完成自动退出(exitTime=0.9)"
                        : $"Speed={prevSpeed:F2} 降到 0.1 以下";
                case HashWalk:
                    return fromHash == HashIdle
                        ? $"Speed={prevSpeed:F2} 超过 0.1"
                        : $"Speed={prevSpeed:F2} 降到 4.5 以下";
                case HashRun:
                    return $"Speed={prevSpeed:F2} 超过 4.5";
                default:
                    return "(未知转移)";
            }
        }

        private static string StateName(int hash)
        {
            switch (hash)
            {
                case HashIdle: return "Idle";
                case HashWalk: return "Walk";
                case HashRun: return "Run";
                case HashJumpStart: return "JumpStart";
                case HashFall: return "Fall";
                case HashLand: return "Land";
                default: return $"0x{hash:X8}";
            }
        }
    }
}
