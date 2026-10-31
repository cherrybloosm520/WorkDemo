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
                _animator.SetTrigger(JumpId);
                _jumpConsumed = true;
            }
            else if (_character.IsGrounded)
            {
                _jumpConsumed = false;
            }
        }
    }
}
