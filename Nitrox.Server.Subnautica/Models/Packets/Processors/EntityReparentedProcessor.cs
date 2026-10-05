using System.Collections.Concurrent;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EntityReparentedProcessor(EntityRegistry entityRegistry, ILogger<EntityReparentedProcessor> logger) : IAuthPacketProcessor<EntityReparented>
{
    /// <summary>
    ///     Last move of each entity made through this processor: the player (by <see cref="Player.GameObjectId"/>) and the parent it set.
    /// </summary>
    /// <remarks>
    ///     Static because packet processors are scoped. Not persisted: after a restart no move is considered conflicting until it happens again.
    /// </remarks>
    private static readonly ConcurrentDictionary<NitroxId, (NitroxId PlayerId, NitroxId ParentId)> lastMoveByEntityId = new();

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<EntityReparentedProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, EntityReparented packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.Id, out Entity entity))
        {
            logger.ZLogError($"Couldn't find entity for {packet.Id}");
            return;
        }
        if (!entityRegistry.TryGetEntityById(packet.NewParentId, out Entity parentEntity))
        {
            logger.ZLogError($"Couldn't find parent entity for {packet.NewParentId}");
            return;
        }

        if (IsConflictingMove(context.Sender, entity, packet))
        {
            // e.g. two players took the same item out of a locker: the first move won, so the sender must undo its local change
            logger.ZLogWarning($"Rejecting reparenting of {packet.Id} from [{context.Sender.Name}]: expected parent {packet.OldParentId} but another player already moved it to {entity.ParentId}");
            await context.ReplyAsync(new EntityReparented(entity.Id, entity.ParentId, packet.NewParentId));
            return;
        }

        entityRegistry.ReparentEntity(packet.Id, packet.NewParentId);
        lastMoveByEntityId[packet.Id] = (context.Sender.GameObjectId, packet.NewParentId);
        await context.SendToOthersAsync(packet);
    }

    /// <summary>
    ///     A move conflicts when the sender expected the entity under another parent than its current one, and that current parent
    ///     was set by a different player's move. Other mismatches (e.g. parents assigned at spawn or by another packet, which may use
    ///     other ids) are let through to avoid making items impossible to move.
    /// </summary>
    private static bool IsConflictingMove(Player sender, Entity entity, EntityReparented packet)
    {
        if (packet.OldParentId == null || entity.ParentId == null || packet.OldParentId.Equals(entity.ParentId))
        {
            return false;
        }
        return lastMoveByEntityId.TryGetValue(entity.Id, out (NitroxId PlayerId, NitroxId ParentId) lastMove) &&
               !lastMove.PlayerId.Equals(sender.GameObjectId) &&
               lastMove.ParentId.Equals(entity.ParentId);
    }
}
