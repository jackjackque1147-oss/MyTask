namespace MyTask.Services;

public sealed class PlaybackPauseGate
{
    private readonly object _sync = new();
    private TaskCompletionSource _resumeSignal = CreateResumeSignal();
    private bool _isPaused;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _isPaused;
            }
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            _resumeSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            _resumeSignal.TrySetResult();
        }
    }

    public async Task WaitUntilResumedAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_sync)
            {
                if (!_isPaused)
                {
                    return;
                }

                waitTask = _resumeSignal.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static TaskCompletionSource CreateResumeSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }
}
