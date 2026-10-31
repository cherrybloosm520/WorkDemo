using System;
using System.Collections.Generic;

namespace Demo.Core
{
    /// <summary>
    /// 状态接口。Enter / Exit 各调用一次,Tick 每帧调用。
    /// 状态通过构造函数持有宿主引用,不要求宿主继承特定基类。
    /// </summary>
    public interface IState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    /// <summary>
    /// 通用代码层状态机。
    /// 设计原则:Animator 只负责播放动画,逻辑流转完全由本状态机控制,
    /// 避免 Animator 的 Any State 造成状态转移爆炸和调试困难。
    /// </summary>
    public class StateMachine
    {
        private readonly Dictionary<Type, IState> _states = new();
        private IState _current;

        /// <summary>当前状态类型,供外部查询(如"是否处于受击状态")。</summary>
        public Type CurrentType => _current?.GetType();

        public void Register<TState>(TState state) where TState : IState
        {
            _states[typeof(TState)] = state;
        }

        /// <summary>切换到目标状态;若已在目标状态则忽略。reason 说明转移原因,写入日志。</summary>
        public void ChangeState<TState>(string reason = "") where TState : IState
        {
            var target = typeof(TState);
            var previous = _current?.GetType();
            if (_current != null && _current.GetType() == target)
                return;

            if (!_states.TryGetValue(target, out var next))
                throw new InvalidOperationException($"状态 {target.Name} 未注册,检查是否忘记 Register");

            _current?.Exit();
            _current = next;
            DemoLog.Log($"状态机:{previous?.Name} -> {target.Name}" +
                        (string.IsNullOrEmpty(reason) ? "" : $",原因:{reason}"));
            _current.Enter();
        }

        /// <summary>强制重进当前状态(如受击打断自身攻击时刷新受击状态)。</summary>
        public void RestartCurrent()
        {
            if (_current == null)
                return;

            DemoLog.Log($"状态机:重进 {_current.GetType().Name}");
            _current.Exit();
            _current.Enter();
        }

        public bool IsState<TState>() where TState : IState
        {
            return _current is TState;
        }

        public void Tick(float deltaTime)
        {
            _current?.Tick(deltaTime);
        }
    }
}
