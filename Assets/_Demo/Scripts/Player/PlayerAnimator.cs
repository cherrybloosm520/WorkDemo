using Demo.Core;
using UnityEngine;

namespace Demo.Player
{
    /// <summary>
    /// 动画驱动层:把 PlayerCharacter 的状态(速度/着地/垂直速度)同步到 Animator 参数。
    /// 逻辑与表现分离:状态机不感知动画,动画层只读角色状态。
    /// 跳跃动画用"上升段"锁存触发,落地后复位,避免重复触发。
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private float speedDampTime = 0.08f; // 速度参数阻尼,避免走/跑状态来回抖动

        private Animator _animator;
        private PlayerCharacter _character;
        private bool _jumpConsumed;
        private string _lastClipName = string.Empty;
        private int _lastStateHash;

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
            if (!_character.IsGrounded && _character.VerticalVelocity > 0.1f && !_jumpConsumed)
            {
                DemoLog.Log("触发跳跃动画触发器");
                _animator.SetTrigger(JumpId);
                _jumpConsumed = true;
            }
            else if (_character.IsGrounded)
            {
                _jumpConsumed = false;
            }

            // 动画状态变化时输出日志(哈希值与搭建时的"状态哈希对照"日志互查)
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash != _lastStateHash)
            {
                DemoLog.Log($"动画状态:0x{_lastStateHash:X8} -> 0x{stateInfo.shortNameHash:X8} " +
                            $"(Speed={planarSpeed:F2}, Grounded={_character.IsGrounded}, VV={_character.VerticalVelocity:F2})");
                _lastStateHash = stateInfo.shortNameHash;
            }

            // 当前播放剪辑变化时输出日志(带关键参数,便于排查"播错动作")
            var clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
            var clipName = clipInfo.Length > 0 ? clipInfo[0].clip.name : "(无剪辑)";
            if (clipName != _lastClipName)
            {
                DemoLog.Log($"动画剪辑:{_lastClipName} -> {clipName}");
                _lastClipName = clipName;
            }
        }
    }
}
