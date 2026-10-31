using Demo.Core;
using UnityEngine;

namespace Demo.Player
{
    /// <summary>
    /// 地面移动状态:空闲 / 走 / 疾跑统一在此处理,通过速度目标区分。
    /// 负责:相机相对移动、加速减速、朝向旋转、跳跃与失足坠落的状态转移。
    /// </summary>
    public class PlayerGroundMoveState : IState
    {
        private readonly PlayerCharacter _owner;

        public PlayerGroundMoveState(PlayerCharacter owner)
        {
            _owner = owner;
        }

        public void Enter()
        {
        }

        public void Tick(float deltaTime)
        {
            var moveInput = _owner.ReadMoveInput();
            var hasInput = moveInput.sqrMagnitude > 0.001f;

            // 目标速度:有输入 → 朝向输入方向;无输入 → 0(靠减速度自然停下)
            var moveDirection = hasInput ? _owner.InputDirection(moveInput) : Vector3.zero;
            var sprinting = PlayerInput.Sprint.IsPressed();
            var targetVelocity = moveDirection * (sprinting ? _owner.SprintSpeed : _owner.WalkSpeed);

            var rate = hasInput ? _owner.Acceleration : _owner.Deceleration;
            _owner.HorizontalVelocity = Vector3.MoveTowards(_owner.HorizontalVelocity, targetVelocity, rate * deltaTime);

            // 无输入且速度极小时直接归零,杜绝残留的微小滑动
            if (!hasInput && _owner.HorizontalVelocity.sqrMagnitude < 0.01f)
                _owner.HorizontalVelocity = Vector3.zero;

            // 朝向:移动时朝移动方向,静止时缓缓转向相机方向(为后续锁定/战斗朝向做准备)
            _owner.RotateTowards(hasInput ? moveDirection : _owner.CameraForward(), deltaTime);

            // 跳跃(经输入缓冲消费,缓冲窗口内按下的跳跃在地面时立即起跳)
            if (_owner.InputBuffer.Consume(PlayerActionKey.Jump))
            {
                DemoLog.Log("消费跳跃缓冲,起跳");
                _owner.VerticalVelocity = _owner.JumpSpeed;
                _owner.StateMachine.ChangeState<PlayerAirState>();
                return;
            }

            // 走出平台边缘 → 进入空中
            if (!_owner.IsGrounded)
            {
                _owner.StateMachine.ChangeState<PlayerAirState>();
                return;
            }

            // 贴地:保持一个很小的向下速度,让 CharacterController 稳定接触地面
            const float groundStick = -2f;
            _owner.CharacterController.Move((_owner.HorizontalVelocity + Vector3.up * groundStick) * deltaTime);
        }

        public void Exit()
        {
        }
    }
}
