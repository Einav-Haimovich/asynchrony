using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;

namespace CreatingTaskFromScratch
{
    public class MyTask
    {
        private readonly Lock _lock = new();
        private bool _completed;
        private Exception? _exception;
        private Action? _action;
        private ExecutionContext? _executionContext;

        public MyTaskAwaiter GetAwaiter() => new(this);

        public bool IsCompleted
        {
            get
            {
                lock (_lock)
                {
                    return _completed;
                }
            }
        }

        public static MyTask Run(Action action)
        {
            MyTask task = new();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    action();
                    task.SetResult();
                }
                catch (Exception e)
                {
                    task.SetException(e);
                }
            });

            return task;
        }

        public MyTask ContinueWith(Action action)
        {
            MyTask task = new();

            lock (_lock)
            {
                if (_completed)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            action();
                            task.SetResult();
                        }
                        catch (Exception e)
                        {
                            task.SetException(e);
                        }
                    });
                }
                else
                {
                    _action = action;
                    _executionContext = ExecutionContext.Capture();
                }

            }

            return task;
        }

        public void SetResult() => CompleteTask(null);
        public void SetException(Exception exception) => CompleteTask(exception);

        private void CompleteTask(Exception? exception)
        {
            lock (_lock)
            {
                if (_completed)
                {
                    throw new InvalidOperationException("Task already completed.");
                }
                _exception = exception;
                _completed = true;

                if (_action is not null)
                {
                    if (_executionContext is not null)
                    {
                        _action.Invoke();
                    }
                    else
                    {
                        ExecutionContext.Run(_executionContext, state => ((Action?)state)?.Invoke(), _action);
                    }
                }
            }
        }

        public void Wait()
        {
            ManualResetEventSlim? resetEventSlim = null;


            lock (_lock)
            {
                if (!_completed)
                {
                    resetEventSlim = new ManualResetEventSlim();
                    ContinueWith(() => resetEventSlim.Set());

                }
            }

            resetEventSlim?.Wait();
            if (_exception is not null)
            {
                ExceptionDispatchInfo.Throw(_exception);
            }
        }

        public static MyTask Delay(TimeSpan delay)
        {
            MyTask task = new();
            new Timer(_ => task.SetResult()).Change(delay, Timeout.InfiniteTimeSpan);
            return task;
        }
    }

    public readonly struct MyTaskAwaiter : INotifyCompletion
    {
        private readonly MyTask _task;
        internal MyTaskAwaiter(MyTask task) => _task = task;
        public bool IsCompleted => _task.IsCompleted;
        public MyTaskAwaiter GetAwaiter() => this;
        public void GetResult() => _task.Wait();
        public void OnCompleted(Action continuation) => _task.ContinueWith(continuation);
    }
}
