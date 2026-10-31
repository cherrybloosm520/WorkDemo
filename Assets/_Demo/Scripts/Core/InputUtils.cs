using UnityEngine;

namespace Demo.Core
{
    public static class InputUtils
    {
        /// <summary>
        /// 摇杆死区:输入幅值 ≤ deadzone 视为 0,其余重映射到 0~1 并钳制幅值 ≤ 1。
        /// 用途:过滤手柄摇杆漂移——漂移会让角色"自己动""松手后仍滑动",也会让镜头缓慢自转。
        /// 键盘输入(幅值 1 / 1.414)与鼠标 delta(单位为像素)不受死区影响。
        /// </summary>
        public static Vector2 ApplyStickDeadzone(Vector2 input, float deadzone)
        {
            var magnitude = input.magnitude;
            if (magnitude <= deadzone)
                return Vector2.zero;

            return Vector2.ClampMagnitude(input / magnitude * ((magnitude - deadzone) / (1f - deadzone)), 1f);
        }
    }
}
