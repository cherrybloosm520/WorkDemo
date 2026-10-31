using Demo.Core;
using UnityEngine;

namespace Demo.Player
{
    /// <summary>
    /// 玩家角色宿主:持有 CharacterController、状态机与共享战斗/移动数据。
    /// 状态(GroundMove / Air)通过构造函数持有本类引用,由 Update 统一驱动。
    /// 后续动画接入时,状态机保持不变,只替换视觉层(胶囊 → Mixamo 模型)。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCharacter : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float sprintSpeed = 6.2f;
        [SerializeField] private float acceleration = 40f;    // 有输入时的地面加速度
        [SerializeField] private float deceleration = 60f;    // 无输入时的地面减速度
        [SerializeField] private float airDeceleration = 5f;  // 空中无输入时的空气阻力
        [SerializeField] private float airControlFactor = 0.35f;
        [SerializeField] private float turnSpeed = 900f;      // 旋转角速度(度/秒)

        [Header("跳跃与重力")]
        [SerializeField] private float jumpSpeed = 7.5f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float maxFallSpeed = -30f;
        // 着地检测参数:从脚上方 groundCheckOffset 处向下投射,扫到脚底以下。
        [SerializeField] private float groundCheckRadius = 0.2f;
        [SerializeField] private float groundCheckOffset = 0.5f;
        [SerializeField] private float groundCheckDistance = 0.75f;

        [Header("滞空容错(土狼时间)")]
        [Tooltip("离地后仍视为地面的宽限秒数:过滤单帧检测噪声,并允许走出平台边缘后短时间内仍可起跳")]
        [SerializeField] private float coyoteTime = 0.12f;

        // 摇杆死区阈值(移动输入)
        private const float MoveDeadzone = 0.15f;

        public CharacterController CharacterController { get; private set; }
        public StateMachine StateMachine { get; } = new();
        public InputBuffer<PlayerActionKey> InputBuffer { get; } = new(0.25f);
        public Camera GameCamera { get; private set; }

        /// <summary>水平速度(状态间共享,含惯性)。</summary>
        public Vector3 HorizontalVelocity { get; set; }

        /// <summary>垂直速度(跳跃/重力)。</summary>
        public float VerticalVelocity { get; set; }

        public bool IsGrounded { get; private set; }

        /// <summary>累计滞空时间(着地时清零)。</summary>
        public float AirTime { get; private set; }

        /// <summary>
        /// 确认离地:着地检测为假且滞空超过土狼时间。
        /// 地面→空中的转移以此为准,单帧检测噪声不会翻转状态机。
        /// </summary>
        public bool CanLeaveGround => !IsGrounded && AirTime > coyoteTime;

        // 速度/手感参数只读暴露给状态
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float AirDeceleration => airDeceleration;
        public float CoyoteTime => coyoteTime;
        public float AirControlFactor => airControlFactor;
        public float TurnSpeed => turnSpeed;
        public float JumpSpeed => jumpSpeed;
        public float Gravity => gravity;
        public float MaxFallSpeed => maxFallSpeed;

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            GameCamera = Camera.main;

            StateMachine.Register(new PlayerGroundMoveState(this));
            StateMachine.Register(new PlayerAirState(this));
        }

        private void Start()
        {
            StateMachine.ChangeState<PlayerGroundMoveState>("初始化,进入地面状态");
        }

        private void Update()
        {
            UpdateGrounded();

            // 滞空计时:作为地面→空中转移的缓冲(土狼时间)
            AirTime = IsGrounded ? 0f : AirTime + Time.deltaTime;

            BufferInputs();
            InputBuffer.Tick();
            StateMachine.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 着地检测(业界标准做法):
        /// 1. 玩家放在独立 Player 图层,查询掩码 ~(1&lt;&lt;layer) 直接排除自身——不依赖
        ///    "初始重叠被跳过"等不可靠的 PhysX 行为,也不做几何自过滤。
        /// 2. 从脚上方高处向下 SphereCast,起点不与地面初始重叠。
        /// 3. 只认法线朝上的表面(normal.y &gt; 0.5):竖直墙面(如地面边缘的侧壁)不算地面。
        /// 检测结果单帧可能受噪声影响,状态机层面由土狼时间兜底。
        /// </summary>
        private void UpdateGrounded()
        {
            var origin = transform.position + Vector3.up * groundCheckOffset;
            var selfMask = ~(1 << gameObject.layer);
            var hitGround = Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out var hit,
                                groundCheckDistance, selfMask, QueryTriggerInteraction.Ignore)
                            && hit.normal.y > 0.5f;

            if (hitGround != IsGrounded)
                DemoLog.Log($"着地状态:{IsGrounded} -> {hitGround}");

            IsGrounded = hitGround;
        }

        /// <summary>
        /// 读取移动输入并施加死区:过滤手柄摇杆漂移
        /// (漂移会让角色自己动、松手后仍滑动;键盘输入幅值 ≥ 1,不受死区影响)。
        /// </summary>
        public Vector2 ReadMoveInput()
        {
            return InputUtils.ApplyStickDeadzone(PlayerInput.Move.ReadValue<Vector2>(), MoveDeadzone);
        }

        /// <summary>把需要"缓冲"的输入(当前仅跳跃)在按键瞬间压入缓冲队列,由状态按时机消费。</summary>
        private void BufferInputs()
        {
            if (PlayerInput.Jump.WasPressedThisFrame())
            {
                InputBuffer.Buffer(PlayerActionKey.Jump);
                DemoLog.Log("输入缓冲:跳跃");
            }
        }

        /// <summary>朝目标方向平滑旋转(移动与空闲状态共用)。</summary>
        public void RotateTowards(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return;

            var targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * deltaTime);
        }

        /// <summary>相机朝向的水平分量(移动输入与空闲朝向的基准)。</summary>
        public Vector3 CameraForward()
        {
            var forward = GameCamera.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 0.0001f ? GameCamera.transform.forward : forward.normalized;
        }

        /// <summary>把输入向量换算成相机相对的世界方向。</summary>
        public Vector3 InputDirection(Vector2 moveInput)
        {
            var camForward = CameraForward();
            var camRight = Vector3.Cross(Vector3.up, camForward);
            return (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
    }
}
