using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace Halo.Tests;

// One CLSID, one type. This is the cheapest test in the repo and it guards a defect that cost the
// spectrum analyzer its entire existence.
//
// Halo declares its COM surface by hand - it is the whole interop policy, no NuGet - and two files had each
// grown a private `[ComImport] class MMDeviceEnumerator` for CLSID BCDE0395. That compiles, and it looks
// like ordinary local duplication. It is not: the runtime keeps ONE CLSID -> type mapping per process, so
// whichever of the two types is instantiated first owns the CLSID and the other file's
// `(IFoo)new Foo()` throws InvalidCastException for as long as the process lives.
//
// It failed silently in the worst possible place. MediaWidget constructs AudioMeter in a field initializer,
// so the meter always won and AudioSpectrum threw on its first line, every launch, in every shipped build -
// inside a `catch { }` on a background thread, where nothing could report it. The equalizer fell back to its
// canned animation and nobody could tell, because the fallback looks like a working equalizer.
public class ComClsidTests
{
    [Fact]
    public void NoTwoComClassesShareACLSID()
    {
        var byClsid = new Dictionary<Guid, List<string>>();
        foreach (var t in typeof(Halo.Widgets.Face).Assembly.GetTypes())
        {
            // the coclasses only: an INTERFACE with a shared IID is fine and normal, since an IID is not
            // what the activation cache is keyed on
            if (!t.IsClass || !t.IsImport) continue;
            var g = t.GetCustomAttributes(typeof(GuidAttribute), false)
                     .OfType<GuidAttribute>().FirstOrDefault();
            if (g is null) continue;
            var id = new Guid(g.Value);
            if (!byClsid.TryGetValue(id, out var list)) byClsid[id] = list = [];
            list.Add(t.FullName ?? t.Name);
        }

        // without this the test passes by finding nothing - a green light for a scan that never ran, which
        // is the failure mode of every "assert there are no X" test
        Assert.NotEmpty(byClsid);

        var clashes = byClsid.Where(kv => kv.Value.Count > 1)
                             .Select(kv => $"{kv.Key} declared by {string.Join(" and ", kv.Value)}")
                             .ToArray();
        Assert.True(clashes.Length == 0,
            "two [ComImport] classes share a CLSID; whichever is created first wins and the other throws "
            + "InvalidCastException for the life of the process:\n  " + string.Join("\n  ", clashes));
    }
}
