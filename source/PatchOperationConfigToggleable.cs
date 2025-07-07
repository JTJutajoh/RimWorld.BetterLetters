using System.Reflection;
using System.Xml;
using HarmonyLib;
using JetBrains.Annotations;

namespace BetterLetters;

public class PatchOperationConfigToggleable : PatchOperation
{
    // ReSharper disable InconsistentNaming
    private string settingName = null!;

    private PatchOperation match = null!;
    private PatchOperation nomatch = null!;
    // ReSharper restore InconsistentNaming

    [UsedImplicitly]
    protected override bool ApplyWorker(XmlDocument xml)
    {
        if (settingName == null)
        {
            Log.Error("PatchOperationConfigToggleable: settingName is null");
            return false;
        }

        var field = typeof(Settings).GetField(settingName, AccessTools.all);
        if (field is null)
        {
            Log.Error($"PatchOperationConfigToggleable: no field named {settingName} found on Settings class");
            return false;
        }

        if ((bool)field.GetValue(null!) && match != null)
        {
            Log.Trace("PatchOperationConfigToggleable: Patch enabled. Applying");
            return match.Apply(xml);
        }

        if (nomatch != null)
        {
            Log.Trace("PatchOperationConfigToggleable: Patch disabled.");
            return nomatch.Apply(xml);
        }

        return true;
    }
}
