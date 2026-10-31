using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo.Player
{
    /// <summary>输入缓冲的按键枚举,与 PlayerInput 中的动作一一对应。</summary>
    public enum PlayerActionKey
    {
        Jump,
        Dodge,
        AttackLight,
        AttackHeavy,
        Guard,
        StanceSwitch,
    }

    /// <summary>
    /// 代码优先的输入定义:不依赖 .inputactions 资产,所有绑定集中在这一处,便于改键与版本管理。
    /// 键位方案(默认):
    ///   移动 WASD / 左摇杆 | 视角 鼠标 / 右摇杆 | 疾跑 左Shift(按住)
    ///   跳跃 空格 / A | 闪避 左Alt / B | 轻击 鼠标左键 / X
    ///   重击 鼠标右键 / Y | 格挡弹反 左Ctrl / LB | 架势切换 Tab / RT | 锁定 鼠标中键 / 右摇杆按下
    /// 动作在初始化时创建并全局启用,任何脚本可直接读取。
    /// </summary>
    public static class PlayerInput
    {
        public static InputActionAsset Asset { get; private set; }
        public static InputAction Move { get; private set; }
        public static InputAction Look { get; private set; }
        public static InputAction Sprint { get; private set; }
        public static InputAction Jump { get; private set; }
        public static InputAction Dodge { get; private set; }
        public static InputAction AttackLight { get; private set; }
        public static InputAction AttackHeavy { get; private set; }
        public static InputAction Guard { get; private set; }
        public static InputAction StanceSwitch { get; private set; }
        public static InputAction LockOn { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Asset = Build();

            // FindAction 找不到会抛异常:尽早暴露拼写错误
            Move = Asset.FindAction("Move");
            Look = Asset.FindAction("Look");
            Sprint = Asset.FindAction("Sprint");
            Jump = Asset.FindAction("Jump");
            Dodge = Asset.FindAction("Dodge");
            AttackLight = Asset.FindAction("AttackLight");
            AttackHeavy = Asset.FindAction("AttackHeavy");
            Guard = Asset.FindAction("Guard");
            StanceSwitch = Asset.FindAction("StanceSwitch");
            LockOn = Asset.FindAction("LockOn");

            Asset.Enable();
        }

        private static InputActionAsset Build()
        {
            var asset = new InputActionAsset();
            var map = asset.AddActionMap("Player");

            var move = map.AddAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            var look = map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            var sprint = map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            var jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");

            var dodge = map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/leftAlt");
            dodge.AddBinding("<Gamepad>/buttonEast");

            var attackLight = map.AddAction("AttackLight", InputActionType.Button, "<Mouse>/leftButton");
            attackLight.AddBinding("<Gamepad>/buttonWest");

            var attackHeavy = map.AddAction("AttackHeavy", InputActionType.Button, "<Mouse>/rightButton");
            attackHeavy.AddBinding("<Gamepad>/buttonNorth");

            var guard = map.AddAction("Guard", InputActionType.Button, "<Keyboard>/leftCtrl");
            guard.AddBinding("<Gamepad>/leftShoulder");

            var stanceSwitch = map.AddAction("StanceSwitch", InputActionType.Button, "<Keyboard>/tab");
            stanceSwitch.AddBinding("<Gamepad>/rightTrigger");

            var lockOn = map.AddAction("LockOn", InputActionType.Button, "<Mouse>/middleButton");
            lockOn.AddBinding("<Gamepad>/rightStickPress");

            return asset;
        }
    }
}
