namespace CleanArchWorkerService.Functional.Tests;

/// <summary>Bounded poll for the timer-driven test (D6) — never a fixed sleep.</summary>
public static class WaitFor
{
    public static async Task UntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return;
            }
            await Task.Delay(25);
        }
        Assert.Fail($"Condition not met within {timeout}.");
    }
}
