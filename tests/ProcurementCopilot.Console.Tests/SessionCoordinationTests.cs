using ProcurementCopilot.ConsoleApp.Runtime;
using Shouldly;

namespace ProcurementCopilot.Console.Tests;

public class SessionLockTests
{
    private static string NewDir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pc-console-tests", Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void TryAcquire_FreeSession_TakesLockWithThisProcess()
    {
        string dir = NewDir();
        using var sut = new SessionLock(dir);
        var id = Guid.NewGuid();

        sut.TryAcquire(id, out SessionLockInfo? owner).ShouldBeTrue();

        owner.ShouldBeNull();
        sut.Held.ShouldBe(id);
        SessionLockInfo info = sut.Read(id)!;
        info.Pid.ShouldBe(Environment.ProcessId);
        info.Instance.ShouldBe(SessionLock.InstanceName);
        File.Exists(Path.Combine(dir, id.ToString("N") + ".lock")).ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_HeldByDeadProcess_IsTreatedAsStale()
    {
        string dir = NewDir();
        var id = Guid.NewGuid();
        var stale = new SessionLockInfo { SessionId = id, Instance = "ghost:1", Pid = 1, AcquiredAt = DateTimeOffset.UtcNow.AddHours(-1), HeartbeatAt = DateTimeOffset.UtcNow.AddHours(-1) };
        File.WriteAllText(Path.Combine(dir, id.ToString("N") + ".lock"), System.Text.Json.JsonSerializer.Serialize(stale, StatusJsonContext.Default.SessionLockInfo));
        using var sut = new SessionLock(dir);

        sut.LiveOwner(id).ShouldBeNull();
        sut.TryAcquire(id, out _).ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_HeldByLiveOtherProcess_Fails()
    {
        string dir = NewDir();
        var id = Guid.NewGuid();
        int otherPid = LiveForeignProcessId();
        var live = new SessionLockInfo { SessionId = id, Instance = "other", Pid = otherPid, AcquiredAt = DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow };
        File.WriteAllText(Path.Combine(dir, id.ToString("N") + ".lock"), System.Text.Json.JsonSerializer.Serialize(live, StatusJsonContext.Default.SessionLockInfo));
        using var sut = new SessionLock(dir);

        sut.TryAcquire(id, out SessionLockInfo? owner).ShouldBeFalse();

        owner!.Pid.ShouldBe(otherPid);
        sut.Held.ShouldBeNull();
    }

    private static int LiveForeignProcessId()
    {
        foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (process.Id != Environment.ProcessId && process.Id > 4 && !process.HasExited)
                {
                    return process.Id;
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Not inspectable from this account; try the next one.
            }
        }

        throw new InvalidOperationException("No inspectable foreign process found.");
    }

    [Fact]
    public void Release_RemovesTheLockFile()
    {
        string dir = NewDir();
        var sut = new SessionLock(dir);
        var id = Guid.NewGuid();
        sut.TryAcquire(id, out _);

        sut.Release();

        File.Exists(Path.Combine(dir, id.ToString("N") + ".lock")).ShouldBeFalse();
        sut.Held.ShouldBeNull();
    }
}

public class StatusPublisherTests
{
    [Fact]
    public void Update_WritesStatusFileThatAnotherPublisherCanRead()
    {
        string dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "pc-console-tests", Guid.NewGuid().ToString("N"))).FullName;
        var writer = new StatusPublisher(dir);
        var reader = new StatusPublisher(dir);
        var id = Guid.NewGuid();

        writer.Update(s => { s.SessionId = id; s.Mode = "execute"; s.Activity = "running"; s.RecentTools.Add(new ToolEvent(DateTimeOffset.UtcNow, "score_bid", "RFP-2026-017, BID-003", "94.60")); });
        writer.Flush();
        SessionStatus? read = reader.Read(id);

        read.ShouldNotBeNull();
        read.Mode.ShouldBe("execute");
        read.Activity.ShouldBe("running");
        read.RecentTools.Single().Name.ShouldBe("score_bid");
        read.Instance.ShouldBe(SessionLock.InstanceName);
    }

    [Fact]
    public void Update_CapsRecentToolsAndSpans()
    {
        var sut = new StatusPublisher(Path.Combine(Path.GetTempPath(), "pc-console-tests", Guid.NewGuid().ToString("N")));
        sut.Update(s => s.SessionId = Guid.NewGuid());

        for (int i = 0; i < 100; i++)
        {
            sut.Update(s => { s.RecentTools.Add(new ToolEvent(DateTimeOffset.UtcNow, "t" + i, "", "")); s.RecentSpans.Insert(0, new SpanEvent("span" + i, "src", 1, "Unset")); });
        }

        sut.Status.RecentTools.Count.ShouldBe(40);
        sut.Status.RecentTools[^1].Name.ShouldBe("t99");
        sut.Status.RecentSpans.Count.ShouldBe(30);
        sut.Status.RecentSpans[0].Name.ShouldBe("span99");
    }

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        new StatusPublisher(Path.Combine(Path.GetTempPath(), "pc-console-tests", Guid.NewGuid().ToString("N"))).Read(Guid.NewGuid()).ShouldBeNull();
    }
}
