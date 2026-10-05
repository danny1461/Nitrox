using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public class EntityReparentedProcessorTest
{
    private EntityRegistry entityRegistry;
    private EntityReparentedProcessor processor;
    private RecordingPacketSender packetSender;
    private Player playerA;
    private Player playerB;
    private NitroxId lockerId;
    private NitroxId itemId;

    [TestInitialize]
    public void Setup()
    {
        entityRegistry = new EntityRegistry(NullLogger<EntityRegistry>.Instance);
        processor = new EntityReparentedProcessor(entityRegistry, NullLogger<EntityReparentedProcessor>.Instance);
        packetSender = new RecordingPacketSender();

        playerA = CreatePlayer(1, "A");
        playerB = CreatePlayer(2, "B");
        lockerId = new NitroxId();
        itemId = new NitroxId();

        entityRegistry.AddEntity(CreateEntity(playerA.GameObjectId, null));
        entityRegistry.AddEntity(CreateEntity(playerB.GameObjectId, null));
        entityRegistry.AddEntity(CreateEntity(lockerId, null));
        entityRegistry.AddEntity(CreateEntity(itemId, lockerId));
    }

    [TestMethod]
    public async Task Move_WithMatchingOldParent_IsAppliedAndBroadcast()
    {
        await processor.Process(Context(playerA), new EntityReparented(itemId, playerA.GameObjectId, lockerId));

        ParentOf(itemId).Should().Be(playerA.GameObjectId);
        packetSender.SentToOthers.Should().ContainSingle();
        packetSender.SentToSession.Should().BeEmpty();
    }

    [TestMethod]
    public async Task SecondPlayerTakingSameItem_IsRejectedAndCorrected()
    {
        await processor.Process(Context(playerA), new EntityReparented(itemId, playerA.GameObjectId, lockerId));
        // B's client still saw the item in the locker
        await processor.Process(Context(playerB), new EntityReparented(itemId, playerB.GameObjectId, lockerId));

        ParentOf(itemId).Should().Be(playerA.GameObjectId);
        packetSender.SentToOthers.Should().ContainSingle("only A's move should be broadcast");
        (SessionId sessionId, EntityReparented correction) = packetSender.SentToSession.Should().ContainSingle().Subject;
        sessionId.Should().Be(playerB.SessionId);
        correction.Id.Should().Be(itemId);
        correction.NewParentId.Should().Be(playerA.GameObjectId);
        correction.OldParentId.Should().Be(playerB.GameObjectId, "B must remove the item from where it wrongly put it");
    }

    [TestMethod]
    public async Task MismatchWithoutConflictingMove_IsApplied()
    {
        // The parent was set at spawn, so a different expected parent may just be another id for the same container
        await processor.Process(Context(playerB), new EntityReparented(itemId, playerB.GameObjectId, new NitroxId()));

        ParentOf(itemId).Should().Be(playerB.GameObjectId);
        packetSender.SentToSession.Should().BeEmpty();
    }

    [TestMethod]
    public async Task MismatchAfterOwnMove_IsApplied()
    {
        await processor.Process(Context(playerA), new EntityReparented(itemId, playerA.GameObjectId, lockerId));
        await processor.Process(Context(playerA), new EntityReparented(itemId, lockerId, new NitroxId()));

        ParentOf(itemId).Should().Be(lockerId);
        packetSender.SentToSession.Should().BeEmpty();
    }

    [TestMethod]
    public async Task MoveWithoutOldParent_IsNeverChecked()
    {
        await processor.Process(Context(playerA), new EntityReparented(itemId, playerA.GameObjectId, lockerId));
        await processor.Process(Context(playerB), new EntityReparented(itemId, playerB.GameObjectId, null));

        ParentOf(itemId).Should().Be(playerB.GameObjectId);
        packetSender.SentToSession.Should().BeEmpty();
    }

    private NitroxId ParentOf(NitroxId id) => entityRegistry.GetEntityById(id).Value.ParentId;

    private AuthProcessorContext Context(Player sender) => new(sender, packetSender);

    private static InventoryItemEntity CreateEntity(NitroxId id, NitroxId parentId) => new(id, "classId", NitroxTechType.None, null, parentId, []);

    private static Player CreatePlayer(ushort sessionId, string name) =>
        new(new PeerId(sessionId), sessionId, name, false, null, NitroxVector3.Zero, NitroxQuaternion.Identity, new NitroxId(), Optional.Empty, Perms.PLAYER,
            new PlayerStatsData(0, 0, 0, 0, 0, 0), SubnauticaGameMode.SURVIVAL, [], [], new Dictionary<string, NitroxId>(), new Dictionary<string, float>(),
            new Dictionary<string, PingInstancePreference>(), [], false, true);

    private sealed class RecordingPacketSender : IPacketSender
    {
        public List<(SessionId, EntityReparented)> SentToSession { get; } = [];
        public List<EntityReparented> SentToOthers { get; } = [];

        public ValueTask SendPacketAsync<T>(T packet, SessionId sessionId) where T : Packet
        {
            SentToSession.Add((sessionId, packet as EntityReparented));
            return ValueTask.CompletedTask;
        }

        public ValueTask SendPacketToAllAsync<T>(T packet) where T : Packet => ValueTask.CompletedTask;

        public ValueTask SendPacketToOthersAsync<T>(T packet, SessionId excludedSessionId) where T : Packet
        {
            SentToOthers.Add(packet as EntityReparented);
            return ValueTask.CompletedTask;
        }
    }
}
