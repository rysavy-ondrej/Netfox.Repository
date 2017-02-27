using System;
using System.Collections.Generic;
using System.Threading;

namespace Netfox.Repository.Utils
{
    internal sealed class Memoizer<TArg, TResult>
    {
        private readonly Func<TArg, TResult> _function;
        private readonly ReaderWriterLockSlim _lock;
        private readonly Dictionary<TArg, Result> _resultCache;

        internal Memoizer(Func<TArg, TResult> function, IEqualityComparer<TArg> argComparer)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));

            _function = function;
            _resultCache = new Dictionary<TArg, Result>(argComparer);
            _lock = new ReaderWriterLockSlim();
        }

        internal TResult Evaluate(TArg arg)
        {
            Result result;
            if (!TryGetResult(arg, out result))
            {
                _lock.EnterWriteLock();
                try
                {
                    if (!_resultCache.TryGetValue(arg, out result))
                    {
                        result = new Result(() => _function(arg));
                        _resultCache.Add(arg, result);
                    }
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            return result.GetValue();
        }

        internal bool TryGetValue(TArg arg, out TResult value)
        {
            Result result;
            if (TryGetResult(arg, out result))
            {
                value = result.GetValue();
                return true;
            }
            value = default(TResult);
            return false;
        }

        private bool TryGetResult(TArg arg, out Result result)
        {
            _lock.EnterReadLock();
            bool result2;
            try
            {
                result2 = _resultCache.TryGetValue(arg, out result);
            }
            finally
            {
                _lock.ExitReadLock();
            }
            return result2;
        }

        private class Result
        {
            private Func<TResult> _delegate;
            private TResult _value;

            internal Result(Func<TResult> createValueDelegate)
            {
                _delegate = createValueDelegate;
            }

            internal TResult GetValue()
            {
                if (_delegate == null)
                {
                    return _value;
                }
                TResult value;
                lock (this)
                {
                    if (_delegate == null)
                    {
                        value = _value;
                    }
                    else
                    {
                        _value = _delegate();
                        _delegate = null;
                        value = _value;
                    }
                }
                return value;
            }
        }
    }
}