using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 集中式调试日志:排查状态机/动画问题时使用,统一 [Demo] 前缀便于在 Console 过滤。
    /// 不需要时把 Enabled 置为 false,或直接删除调用点。
    /// </summary>
    public static class DemoLog
    {
        public static bool Enabled = true;

        public static void Log(object message)
        {
            if (Enabled)
                Debug.Log($"[Demo] {message}");
        }

        public static void Warn(object message)
        {
            if (Enabled)
                Debug.LogWarning($"[Demo] {message}");
        }
    }
}
