using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class EntityReparented : Packet
{
    public NitroxId Id { get; }

    public NitroxId NewParentId { get; }

    /// <summary>
    ///     The parent the sender believes the entity is being moved out of. When set, the server rejects the move if the entity's
    ///     current parent differs (e.g. another player already took the item) and replies with the authoritative parent.
    ///     Null when the previous parent is unknown, in which case no check is made.
    /// </summary>
    public NitroxId? OldParentId { get; }

    public EntityReparented(NitroxId id, NitroxId newParentId, NitroxId? oldParentId)
    {
        Id = id;
        NewParentId = newParentId;
        OldParentId = oldParentId;
    }
}
