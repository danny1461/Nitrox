using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Remembers which container an item is removed from so that its next <see cref="Nitrox.Model.Subnautica.Packets.EntityReparented"/>
/// can tell the server where it is expected to come from
/// </summary>
public sealed partial class ItemsContainer_NotifyRemoveItem_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ItemsContainer t) => t.NotifyRemoveItem(default));

    public static void Postfix(ItemsContainer __instance, InventoryItem item)
    {
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted || item == null)
        {
            return;
        }

        Resolve<ItemContainers>().RecordItemRemoved(item.item, __instance.tr);
    }
}
