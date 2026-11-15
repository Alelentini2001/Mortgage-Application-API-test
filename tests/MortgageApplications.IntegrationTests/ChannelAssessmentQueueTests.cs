using Microsoft.Extensions.Options;
using MortgageApplications.Api.Assessments;

namespace MortgageApplications.IntegrationTests;

/// <summary>W-5 (P6-D2): the bounded queue waits when full instead of dropping, and a cancelled wait adds nothing.</summary>
public sealed class ChannelAssessmentQueueTests
{
    private readonly ChannelAssessmentQueue _queue = new(Options.Create(new AssessmentQueueOptions { QueueCapacity = 1 }));

    [Fact]
    public async Task EnqueueAsync_at_capacity_waits_until_an_item_is_read()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await _queue.EnqueueAsync(first, Http.Ct);

        var waiting = _queue.EnqueueAsync(second, Http.Ct).AsTask();

        Assert.False(waiting.IsCompleted);
        Assert.True(_queue.Reader.TryRead(out var read));
        Assert.Equal(first, read);
        await waiting.WaitAsync(TimeSpan.FromSeconds(10), Http.Ct);
        Assert.True(_queue.Reader.TryRead(out var next));
        Assert.Equal(second, next);
    }

    [Fact]
    public async Task EnqueueAsync_cancelled_while_waiting_throws_and_adds_nothing()
    {
        var first = Guid.NewGuid();
        await _queue.EnqueueAsync(first, Http.Ct);
        using var giveUp = new CancellationTokenSource();
        var waiting = _queue.EnqueueAsync(Guid.NewGuid(), giveUp.Token).AsTask();

        await giveUp.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.Equal(1, _queue.Reader.Count);
        Assert.True(_queue.Reader.TryRead(out var only));
        Assert.Equal(first, only);
    }

    // Review R2: a request token that is already cancelled must not strand a saved Pending row while there is room.
    [Fact]
    public async Task EnqueueAsync_with_cancelled_token_and_free_capacity_queues_the_id()
    {
        var id = Guid.NewGuid();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await _queue.EnqueueAsync(id, cancelled.Token);

        Assert.True(_queue.Reader.TryRead(out var read));
        Assert.Equal(id, read);
    }
}
