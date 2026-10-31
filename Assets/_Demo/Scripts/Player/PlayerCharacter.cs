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
        [SerializeField] private float groundCheckRadius = 0.18f;
        [SerializeField] private float groundCheckOffset = 0.1f;

        // 摇杆死区阈值(移动输入)
        private const float MoveDeadzone = 0.15f;

        private static readonly Collider[] GroundCheckBuffer = new Collider[8];

        public CharacterController CharacterController { get; private set; }
        public StateMachine StateMachine { get; } = new();
        public InputBuffer<PlayerActionKey> InputBuffer { get; } = new(0.25f);
        public Camera GameCamera { get; private set; }

        /// <summary>水平速度(状态间共享,含惯性)。</summary>
        public Vector3 HorizontalVelocity { get; set; }

        /// <summary>垂直速度(跳跃/重力)。</summary>
        public float VerticalVelocity { get; set; }

        public bool IsGrounded { get; private set; }

        // 速度/手感参数只读暴露给状态
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float AirDeceleration => airDeceleration;
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
            StateMachine.ChangeState<PlayerGroundMoveState>();
        }

        private void Update()
        {
            UpdateGrounded();
            BufferInputs();
            InputBuffer.Tick();
            StateMachine.Tick(Time.deltaTime);
        }

        /// <summary>
        /// 着地检测:用脚底重叠检测(OverlapSphere),而不是向下投射。
        /// 向下 SphereCast 会跳过"起点已与地面重叠"的碰撞体——贴地站立时检测球
        /// 必然与地面初始重叠,导致永远误判为空中(空中移动逻辑 = 滑行手感 + 播下落动画)。
        /// 重叠检测直接询问"脚底附近是否有地面",并过滤角色自身的碰撞体。
        /// </summary>
        private void UpdateGrounded()
        {
            var feetPoint = transform.position + Vector3.up * groundCheckOffset;
            var count = Physics.OverlapSphereNonAlloc(feetPoint, groundCheckRadius, GroundCheckBuffer, ~0, QueryTriggerInteraction.Ignore);

            var grounded = false;
            for (var i = 0; i < count; i++)
            {
                // 忽略角色自身(IsChildOf 包含自身,CharacterController 也在过滤范围内)
                if (GroundCheckBuffer[i].transform.IsChildOf(transform))
                    continue;

                grounded = true;
                break;
            }

            if (grounded != IsGrounded)
                DemoLog.Log($"着地状态:{IsGrounded} -> {grounded}");

            IsGrounded = grounded;
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
