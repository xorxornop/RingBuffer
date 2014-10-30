#region License

//        The MIT License (MIT)
//
//        Copyright (c) 2014 StephenCleary
//
//        Permission is hereby granted, free of charge, to any person obtaining a copy
//        of this software and associated documentation files (the "Software"), to deal
//        in the Software without restriction, including without limitation the rights
//        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//        copies of the Software, and to permit persons to whom the Software is
//        furnished to do so, subject to the following conditions:
//
//        The above copyright notice and this permission notice shall be included in all
//        copies or substantial portions of the Software.
//
//        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//        SOFTWARE.

#endregion

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RingByteBuffer.AsyncHelpers
{
    /// <summary>
    ///     An async-compatible manual-reset event.
    /// </summary>
    [DebuggerDisplay("Id = {Id}, IsSet = {GetStateForDebugger}")]
    [DebuggerTypeProxy(typeof (DebugView))]
    public sealed class AsyncManualResetEvent
    {
        /// <summary>
        ///     The object used for synchronization.
        /// </summary>
        private readonly object _sync;

        /// <summary>
        ///     The semi-unique identifier for this instance. This is 0 if the id has not yet been created.
        /// </summary>
        private int _id;

        /// <summary>
        ///     The current state of the event.
        /// </summary>
        private TaskCompletionSource _tcs;

        /// <summary>
        ///     Creates an async-compatible manual-reset event.
        /// </summary>
        /// <param name="set">Whether the manual-reset event is initially set or unset.</param>
        public AsyncManualResetEvent(bool set)
        {
            _sync = new object();
            _tcs = new TaskCompletionSource();
            if (set) {
                //Enlightenment.Trace.AsyncManualResetEvent_Set(this, _tcs.Task);
                _tcs.SetResult();
            }
        }

        /// <summary>
        ///     Creates an async-compatible manual-reset event that is initially unset.
        /// </summary>
        public AsyncManualResetEvent()
            : this(false) {}

        [DebuggerNonUserCode]
        private bool GetStateForDebugger
        {
            get { return _tcs.Task.IsCompleted; }
        }

        /// <summary>
        ///     Gets a semi-unique identifier for this asynchronous manual-reset event.
        /// </summary>
        public int Id
        {
            get { return IdManager<AsyncManualResetEvent>.GetId(ref _id); }
        }

        /// <summary>
        ///     Asynchronously waits for this event to be set.
        /// </summary>
        public Task WaitAsync()
        {
            lock (_sync) {
                Task ret = _tcs.Task;
                //Enlightenment.Trace.AsyncManualResetEvent_Wait(this, ret);
                return ret;
            }
        }

        /// <summary>
        ///     Synchronously waits for this event to be set. This method may block the calling thread.
        /// </summary>
        public void Wait()
        {
            WaitAsync().Wait();
        }

        /// <summary>
        ///     Synchronously waits for this event to be set. This method may block the calling thread.
        /// </summary>
        /// <param name="cancellationToken">
        ///     The cancellation token used to cancel the wait. If this token is already canceled, this
        ///     method will first check whether the event is set.
        /// </param>
        public void Wait(CancellationToken cancellationToken)
        {
            Task ret = WaitAsync();
            if (ret.IsCompleted) {
                return;
            }
            ret.Wait(cancellationToken);
        }

        /// <summary>
        ///     Sets the event, atomically completing every task returned by <see cref="WaitAsync" />. If the event is already set,
        ///     this method does nothing.
        /// </summary>
        public void Set()
        {
            lock (_sync) {
                //Enlightenment.Trace.AsyncManualResetEvent_Set(this, _tcs.Task);
                _tcs.TrySetResultWithBackgroundContinuations();
            }
        }

        /// <summary>
        ///     Resets the event. If the event is already reset, this method does nothing.
        /// </summary>
        public void Reset()
        {
            lock (_sync) {
                if (_tcs.Task.IsCompleted) {
                    _tcs = new TaskCompletionSource();
                }
                //Enlightenment.Trace.AsyncManualResetEvent_Reset(this, _tcs.Task);
            }
        }

        // ReSharper disable UnusedMember.Local

        #region Nested type: DebugView

        [DebuggerNonUserCode]
        private sealed class DebugView
        {
            private readonly AsyncManualResetEvent _mre;

            public DebugView(AsyncManualResetEvent mre)
            {
                _mre = mre;
            }

            public int Id
            {
                get { return _mre.Id; }
            }

            public bool IsSet
            {
                get { return _mre.GetStateForDebugger; }
            }

            public Task CurrentTask
            {
                get { return _mre._tcs.Task; }
            }
        }

        #endregion

        // ReSharper restore UnusedMember.Local
    }
}
