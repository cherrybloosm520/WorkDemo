using Cinemachine;
using UnityEngine;

namespace Demo.Player
{
    /// <summary>
    /// 第三人称自由视角:基于 CinemachineFreeLook 的轨道相机。
    /// 视角输入(Look)由本类读取并直接驱动 FreeLook 的 X/Y 轴,
    /// 带平滑和独立于帧率的灵敏度;障碍物遮挡由 CinemachineCollider 处理。
    /// 后续锁敌镜头会在本类上扩展(锁定时约束 X 轴朝向目标)。
    /// </summary>
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("视角手感")]
        [SerializeField] private float xSensitivity = 0.12f;    // 水平:度/像素
        [SerializeField] private float ySensitivity = 0.0018f;  // 垂直:FreeLook Y 轴(0~1)/像素
        [SerializeField] private float smoothTime = 0.06f;      // 输入平滑

        [Header("垂直视角范围(角度制,单位:度)")]
        [Tooltip("顶环俯视角度:正数 = 俯视,数值越大越接近垂直向下。不要接近 ±90°")]
        [SerializeField] private float topPitch = 38f;
        [Tooltip("中环默认视角俯角")]
        [SerializeField] private float middlePitch = 10f;
        [Tooltip("底环角度:负数 = 仰视。太负镜头会低于地面(约 -15° 是地面极限)")]
        [SerializeField] private float bottomPitch = -13f;

        [Header("三环轨道半径(单位:米)")]
        [SerializeField] private float topRadius = 2.8f;
        [SerializeField] private float middleRadius = 3.6f;
        [SerializeField] private float bottomRadius = 3.8f;

        private CinemachineFreeLook _freeLook;
        private Vector2 _smoothedDelta;
        private Vector2 _deltaVelocity;

        private void Awake()
        {
            _freeLook = gameObject.AddComponent<CinemachineFreeLook>();

            _freeLook.m_Lens.FieldOfView = 50f;

            // 视角由代码驱动:禁用 FreeLook 内部输入,避免双重响应
            _freeLook.m_XAxis.m_InputAxisName = string.Empty;
            _freeLook.m_XAxis.m_MaxSpeed = 0f;
            _freeLook.m_YAxis.m_InputAxisName = string.Empty;
            _freeLook.m_YAxis.m_MaxSpeed = 0f;

            // 不自动回中(动作游戏相机应保持玩家摆放的位置)
            _freeLook.m_RecenterToTargetHeading.m_enabled = false;

            // 相机穿墙时自动拉近
            gameObject.AddComponent<CinemachineCollider>();
        }

        /// <summary>
        /// 把"俯仰角 + 半径"换算成 FreeLook 的轨道参数。
        /// 角度定义:正数 = 相机在目标上方俯视,负数 = 仰视。
        /// 换算关系:height = tan(角度) × radius(高度相对 LookAt 目标)。
        /// </summary>
        private static CinemachineFreeLook.Orbit OrbitFromPitch(float pitchDegrees, float radius)
        {
            var height = Mathf.Tan(pitchDegrees * Mathf.Deg2Rad) * radius;
            return new CinemachineFreeLook.Orbit(height, radius);
        }

        /// <summary>绑定跟随目标与注视点(玩家与玩家头顶的 LookTarget)。</summary>
        public void Bind(Transform follow, Transform lookAt)
        {
            _freeLook.Follow = follow;
            _freeLook.LookAt = lookAt;
        }

        private void LateUpdate()
        {
            if (_freeLook.Follow == null)
                return;

            // 每帧由"俯仰角 + 半径"换算轨道高度并应用:
            // Play 模式下改 Inspector 的角度/半径可实时看效果(3 次换算,开销可忽略)
            _freeLook.m_Orbits[0] = OrbitFromPitch(topPitch, topRadius);
            _freeLook.m_Orbits[1] = OrbitFromPitch(middlePitch, middleRadius);
            _freeLook.m_Orbits[2] = OrbitFromPitch(bottomPitch, bottomRadius);

            var look = PlayerInput.Look.ReadValue<Vector2>();

            // 平滑原始输入,让镜头有轻微重量感
            var target = new Vector2(look.x * xSensitivity, -look.y * ySensitivity);
            _smoothedDelta = Vector2.SmoothDamp(_smoothedDelta, target, ref _deltaVelocity, smoothTime);

            // X 轴单位为度(0~360,环绕),Y 轴 0(俯视)~1(仰视)
            _freeLook.m_XAxis.Value += _smoothedDelta.x;
            _freeLook.m_YAxis.Value += _smoothedDelta.y;
        }
    }
}
