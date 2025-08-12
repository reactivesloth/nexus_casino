using System;
using UnityEngine;

namespace Code.Network.Lobby.EOSCoroutines
{
    /// <summary>
    /// Yield-инструкция: ждёт пока условие станет true, либо пока не истечёт таймаут.
    /// В onTimeout можно безопасно выставить "TimedOut".
    /// </summary>
    public sealed class WaitUntilOrTimeout : CustomYieldInstruction
    {
        private readonly Func<bool> _condition;
        private readonly float _expireAt;
        private readonly Action _onTimeout;

        public WaitUntilOrTimeout(Func<bool> condition, float timeout, Action onTimeout)
        {
            _condition = condition ?? (() => true);
            _expireAt = Time.time + Mathf.Max(0.01f, timeout);
            _onTimeout = onTimeout ?? (() => { });
        }

        public override bool keepWaiting
        {
            get
            {
                if (_condition()) return false;
                if (Time.time < _expireAt) return true;
                _onTimeout();
                return false;
            }
        }

        public override string ToString() => $"WaitUntilOrTimeout: now={Time.time:F2} expireAt={_expireAt:F2}";
    }
}