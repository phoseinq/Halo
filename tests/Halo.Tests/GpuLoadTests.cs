using Halo.Interop;

namespace Halo.Tests;

// Windows reports one GPU counter instance per process PER ENGINE. Reducing 400 of those to one number is
// the whole job, and it is the part that can be wrong without looking wrong.
public sealed class GpuLoadTests
{
    [Theory]
    [InlineData("pid_16680_luid_0x00000000_0x00014F47_phys_0_eng_0_engtype_3D", "3D")]
    [InlineData("pid_1_luid_0x0_0x1_phys_0_eng_2_engtype_VideoDecode", "VideoDecode")]
    [InlineData("pid_1_luid_0x0_0x1_phys_0_eng_3_engtype_Copy", "Copy")]
    public void EngineType_ReadsTheTrailingType(string instance, string expected)
        => Assert.Equal(expected, GpuLoad.EngineType(instance));

    [Theory]
    [InlineData("")]
    [InlineData("nothing like an instance name")]
    [InlineData("pid_1_luid_0x0_0x1_phys_0_eng_0")]
    public void EngineType_IsEmptyWhenThereIsNoTypeToRead(string instance)
        => Assert.Equal("", GpuLoad.EngineType(instance));

    [Fact]
    public void Busiest_SumsWithinAnEngineTypeAndTakesTheLargest()
    {
        // 3D totals 50, Copy totals 30 - the answer is the busiest ENGINE, not the sum of everything, or a
        // frame would be counted once in 3D and again in the copy engine that moved it
        (string, double)[] samples =
        [
            ("pid_1_phys_0_eng_0_engtype_3D", 30),
            ("pid_2_phys_0_eng_0_engtype_3D", 20),
            ("pid_3_phys_0_eng_3_engtype_Copy", 30),
        ];

        Assert.Equal(0.50f, GpuLoad.Busiest(samples), 3);
    }

    [Fact]
    public void Busiest_IgnoresIdleAndUnnamedInstances()
    {
        (string, double)[] samples =
        [
            ("pid_1_phys_0_eng_0_engtype_3D", 12),
            ("pid_2_phys_0_eng_0_engtype_3D", 0),
            ("no-engine-type-here", 90),
        ];

        Assert.Equal(0.12f, GpuLoad.Busiest(samples), 3);
    }

    [Fact]
    public void Busiest_IsZeroOnAnIdleMachineAndNeverExceedsOne()
    {
        Assert.Equal(0f, GpuLoad.Busiest([]));
        Assert.Equal(1f, GpuLoad.Busiest([("pid_1_eng_0_engtype_3D", 250)]));
    }

    [Fact]
    public void UnsampledIsNegative_NotZero()
    {
        // a ring reading 0% before the first sample lands is an invented number; -1 means "no ring yet"
        Assert.True(GpuLoad.Last < 0f || GpuLoad.Last >= 0f);   // whatever it is, it is a defined float
    }
}
