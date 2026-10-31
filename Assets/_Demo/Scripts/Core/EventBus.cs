using System;
using System.Collections.Generic;

namespace Demo.Core
{
    /// <summary>
    /// 轻量事件总线:核心逻辑(战斗)只发布事件,表现层(镜头震动、音效、特效、UI)订阅响应。
    /// 用泛型事件类型作为 key,不使用字符串。
    /// 注意:订阅方在 OnDestroy / OnDisable 中必须 Unsubscribe,否则会引用泄漏。
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);
            _handlers[type] = _handlers.TryGetValue(type, out var existing)
                ? Delegate.Combine(existing, handler)
                : handler;
        }

        public static void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var existing))
                return;

            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null)
                _handlers.Remove(typeof(TEvent));
            else
                _handlers[typeof(TEvent)] = remaining;
        }

        public static void Publish<TEvent>(TEvent evt)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var existing) && existing is Action<TEvent> action)
                action.Invoke(evt);
        }

        /// <summary>清空所有订阅(仅在全局重置场景使用,如退出战斗模式)。</summary>
        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
