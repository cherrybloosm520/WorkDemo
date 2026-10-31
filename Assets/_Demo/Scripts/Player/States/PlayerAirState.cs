using Demo.Core;
using UnityEngine;

namespace Demo.Player
{
    /// <summary>
    /// 空中状态:跳跃与坠落。
    /// 空中保留部分水平控制(airControlFactor),垂直速度按重力累积并限制最大下落速度;
    /// 落地且垂直速度不再向上时回到地面状态。
    /// </summary>
    public class PlayerAirState : IState
    {
        private readonly PlayerCharacter _owner;

        public PlayerAirState(PlayerCharacter owner)
        {
            _owner = owner;
        }

        public void Enter()
        {
        }

        public void Tick(float deltaTime)
        {
            var moveInput = PlayerInput.Move.ReadValue<Vector2>();
            var hasInput = moveInput.sqrMagnitude > 0.001f;

            // 空中控制:同样的方向逻辑,但加速度乘以空控系数
            if (hasInput)
            {
                var moveDirection = _owner.InputDirection(moveInput);
                var sprinting = PlayerInput.Sprint.IsPressed();
                var targetVelocity = moveDirection * (sprinting ? _owner.SprintSpeed : _owner.WalkSpeed) * _owner.AirControlFactor;
                _owner.HorizontalVelocity = Vector3.MoveTowards(
                    _owner.HorizontalVelocity, targetVelocity, _owner.Acceleration * _owner.AirControlFactor * deltaTime);
            }

            _owner.RotateTowards(hasInput ? _owner.InputDirection(moveInput) : _owner.CameraForward(), deltaTime);

            // 重力累积,限制最大下落速度(防止从高处坠落时穿透)
            _owner.VerticalVelocity = Mathf.Max(_owner.VerticalVelocity + _owner.Gravity * deltaTime, _owner.MaxFallSpeed);

            _owner.CharacterController.Move((_owner.HorizontalVelocity + Vector3.up * _owner.VerticalVelocity) * deltaTime);

            // 落地(仅在垂直速度不再向上时判定,避免起跳瞬间被误判回地面)
            if (_owner.IsGrounded && _owner.VerticalVelocity <= 0f)
            {
                _owner.VerticalVelocity = 0f;
                _owner.StateMachine.ChangeState<PlayerGroundMoveState>();
            }
        }

        public void Exit()
        {
        }
    }
}
