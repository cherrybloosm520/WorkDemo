using System.Collections.Generic;
using UnityEngine;

namespace Demo.Core
{
    /// <summary>
    /// 输入缓冲:把"当前动作无法响应"的输入缓存一小段时间(默认 0.25s),
    /// 等状态机进入可响应窗口后立即执行 —— 连招"跟手"的根本机制。
    /// 典型用法:攻击后摇期间按下轻击 → 缓冲 → 后摇结束(或可取消窗口开启)时取出执行下一段。
    ///
    /// 设计决策:同一按键只保留最新一次输入(长按/连点不会堆满队列),
    /// 保证一次按键最多触发一次后续动作,行为可预测。
    /// </summary>
    public class InputBuffer<TKey>
    {
        private struct BufferedInput
        {
            public readonly TKey Key;
            public readonly float ExpireTime;

            public BufferedInput(TKey key, float expireTime)
            {
                Key = key;
                ExpireTime = expireTime;
            }
        }

        private readonly List<BufferedInput> _queue = new();
        private readonly float _bufferSeconds;

        public InputBuffer(float bufferSeconds = 0.25f)
        {
            _bufferSeconds = bufferSeconds;
        }

        /// <summary>缓存一次输入,由输入回调在按键瞬间调用。</summary>
        public void Buffer(TKey key)
        {
            // 同类型输入只保留最新一次
            for (var i = 0; i < _queue.Count; i++)
            {
                if (EqualityComparer<TKey>.Default.Equals(_queue[i].Key, key))
                {
                    _queue.RemoveAt(i);
                    break;
                }
            }

            _queue.Add(new BufferedInput(key, Time.time + _bufferSeconds));
        }

        /// <summary>消费一次指定输入:找到第一个仍未过期的,移除并返回 true。过期条目由 Tick 清理。</summary>
        public bool Consume(TKey key)
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                var buffered = _queue[i];
                if (!EqualityComparer<TKey>.Default.Equals(buffered.Key, key) || buffered.ExpireTime <= Time.time)
                    continue;

                _queue.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>队列中是否存在未过期的指定输入(不消费,供状态查询)。</summary>
        public bool Has(TKey key)
        {
            foreach (var buffered in _queue)
            {
                if (EqualityComparer<TKey>.Default.Equals(buffered.Key, key) && buffered.ExpireTime > Time.time)
                    return true;
            }
            return false;
        }

        /// <summary>由宿主每帧调用,清理过期输入。</summary>
        public void Tick()
        {
            for (var i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].ExpireTime <= Time.time)
                    _queue.RemoveAt(i);
            }
        }
    }
}
