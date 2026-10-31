using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 全局时间控制:Hit Stop(命中停顿)与慢动作的唯一入口。
    /// 禁止其他脚本直接修改 Time.timeScale —— 多个效果叠加时会互相冲突,
    /// 这里统一取"所有未到期请求中最小的 timeScale",全部到期后恢复 1。
    /// 注意:Hit Stop 期间动画与物理会完全冻结,音效不受影响。
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        private struct TimedRequest
        {
            public float TimeScale; // 请求的时间倍率(0 = 完全停顿)
            public float EndTime;   // unscaledTime 到期时间
        }

        private readonly List<TimedRequest> _requests = new(4);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[TimeController]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<TimeController>();
        }

        /// <summary>命中停顿:完全暂停 durationSeconds 秒(真实时间)。打击感的核心手段,建议 0.06~0.1s。</summary>
        public void HitStop(float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return;

            _requests.Add(new TimedRequest
            {
                TimeScale = 0f,
                EndTime = Time.unscaledTime + durationSeconds,
            });
        }

        /// <summary>慢动作:如弹反成功后的 0.3 倍速。timeScale 会被钳制到 (0, 1] 区间。</summary>
        public void SlowMotion(float timeScale, float durationSeconds)
        {
            if (durationSeconds <= 0f)
                return;

            _requests.Add(new TimedRequest
            {
                TimeScale = Mathf.Clamp(timeScale, 0.01f, 1f),
                EndTime = Time.unscaledTime + durationSeconds,
            });
        }

        /// <summary>立即恢复正常时间(玩家死亡、读档、进入菜单等场景调用)。</summary>
        public void Reset()
        {
            _requests.Clear();
            Time.timeScale = 1f;
        }

        private void Update()
        {
            _requests.RemoveAll(r => r.EndTime <= Time.unscaledTime);

            var scale = 1f;
            foreach (var request in _requests)
            {
                if (request.TimeScale < scale)
                    scale = request.TimeScale;
            }
            Time.timeScale = scale;
        }
    }
}
