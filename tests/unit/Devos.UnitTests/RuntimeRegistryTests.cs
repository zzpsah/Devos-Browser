using Devos.Runtime;
using Xunit;

namespace Devos.UnitTests;

public sealed class RuntimeRegistryTests
{
    [Fact]
    public void CreateRejectsEmptyGoal()
    {
        var registry = new InMemoryTaskRegistry();

        Assert.Throws<ArgumentException>(() => registry.Create("   "));
    }

    [Fact]
    public void CreatePersistsTask()
    {
        var registry = new InMemoryTaskRegistry();

        var task = registry.Create("open synthetic portal");
        var loaded = registry.Get(task.TaskId);

        Assert.NotNull(loaded);
        Assert.Equal(RuntimeTaskState.Created, loaded!.State);
        Assert.Equal("open synthetic portal", loaded.Goal);
    }

    [Fact]
    public void UpdateStateChangesTaskStatus()
    {
        var registry = new InMemoryTaskRegistry();
        var task = registry.Create("pause test");

        var updated = registry.UpdateState(task.TaskId, RuntimeTaskState.Paused, "test-pause");

        Assert.Equal(RuntimeTaskState.Paused, updated.State);
        Assert.Equal("test-pause", updated.Reason);
    }
}
