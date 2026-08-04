using System.Collections.Generic;

namespace Halo.Notifications;

internal static class BannerWriter
{
    internal static int Commit(IReadOnlyList<BannerEdit> edits)
    {
        if (edits.Count == 0) return 0;

        if (Halo.Interop.AppModel.IsPackaged)
            Halo.Interop.OutProc.Run("--banner-apply", BannerBatch.Serialize(edits));
        else
            BannerApply.Apply(edits);

        int ok = 0;
        foreach (var e in edits) if (Verified(e)) ok++;
        return ok;
    }

    internal static bool Verified(BannerEdit edit)
    {
        var now = BannerApply.Read(edit.Subkey, edit.Name);
        return edit.Value is int want ? now == want : now is null;
    }
}
