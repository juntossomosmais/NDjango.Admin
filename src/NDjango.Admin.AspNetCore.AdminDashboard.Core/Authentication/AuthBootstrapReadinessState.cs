using System;
using System.Threading;
using System.Threading.Tasks;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Authentication
{
    internal class AuthBootstrapReadinessState
    {
        private readonly TaskCompletionSource _readyTcs =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsReady => _readyTcs.Task.Status == TaskStatus.RanToCompletion;

        public bool IsFailed => _readyTcs.Task.IsFaulted;

        public Task WaitForReadyAsync(CancellationToken ct = default)
            => _readyTcs.Task.WaitAsync(ct);

        public void SetReady() => _readyTcs.TrySetResult();

        public void SetFailed(Exception error)
        {
            ArgumentNullException.ThrowIfNull(error);
            _readyTcs.TrySetException(error);
        }
    }
}
