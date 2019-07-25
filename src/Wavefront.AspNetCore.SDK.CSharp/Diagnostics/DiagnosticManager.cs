// From https://github.com/opentracing-contrib/csharp-netcore/blob/master/src/OpenTracing.Contrib.NetCore/Internal/DiagnosticManager.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Wavefront.AspNetCore.SDK.CSharp.Diagnostics
{
    public class DiagnosticManager : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly IEnumerable<DiagnosticObserver> _diagnosticSubscribers;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private IDisposable _allListenersSubscription;

        public bool IsRunning => _allListenersSubscription != null;

        public DiagnosticManager(
            IEnumerable<DiagnosticObserver> diagnosticSubscribers)
        {
            _diagnosticSubscribers = diagnosticSubscribers ??
                throw new ArgumentNullException(nameof(diagnosticSubscribers));
        }

        public void Start()
        {
            if (_allListenersSubscription == null)
            {
                _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener diagnosticListener)
        {
            foreach (var subscriber in _diagnosticSubscribers)
            {
                IDisposable subscription = subscriber.SubscribeIfMatch(diagnosticListener);
                if (subscription != null)
                {
                    _subscriptions.Add(subscription);
                }
            }
        }

        public void Stop()
        {
            if (_allListenersSubscription != null)
            {
                _allListenersSubscription.Dispose();
                _allListenersSubscription = null;

                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
