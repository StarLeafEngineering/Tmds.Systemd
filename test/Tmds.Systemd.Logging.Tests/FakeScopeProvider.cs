using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Tmds.Systemd.Logging.Tests
{
    // Copied from Microsoft.Extensions.Logging.LoggerFactoryScopeProvider,
    // the default implementation, but with the activity tracking options
    // support removed and the nullability checks fixed.
    public class FakeScopeProvider : IExternalScopeProvider
    {
        private readonly AsyncLocal<Scope?> _currentScope = new();

        public void ForEachScope<TState>(Action<object, TState> callback, TState state)
        {
            void Report(Scope? current)
            {
                if (current == null)
                {
                    return;
                }

                Report(current.Parent);
                callback(current.State, state);
            }

            Report(_currentScope.Value);
        }

        public IDisposable Push(object state)
        {
            var parent = _currentScope.Value;
            var newScope = new Scope(this, state, parent);
            _currentScope.Value = newScope;

            return newScope;
        }

        private class Scope : IDisposable
        {
            private readonly FakeScopeProvider _provider;
            private bool _isDisposed;

            internal Scope(FakeScopeProvider provider, object state, Scope? parent)
            {
                _provider = provider;
                State = state;
                Parent = parent;
            }

            public Scope? Parent { get; }

            public object State { get; }

            public override string? ToString()
            {
                return State.ToString();
            }

            public void Dispose()
            {
                if (!_isDisposed)
                {
                    _provider._currentScope.Value = Parent;
                    _isDisposed = true;
                }
            }
        }
    }
}
