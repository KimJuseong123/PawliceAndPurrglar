using NUnit.Framework;
using PawliceAndPurrglar.Companions;
using PawliceAndPurrglar.Integration.Voice;

namespace PawliceAndPurrglar.Tests.EditMode
{
    /// <summary>
    /// What the host decided has to survive the trip to the other machine.
    ///
    /// A guest never runs the obedience roll: the host does, and broadcasts the
    /// chosen <see cref="CompanionCommandId"/>. The guest's feed then turns that
    /// back into a name to show, and it does so through
    /// <see cref="VoiceCommandMapper"/> — the server's intent vocabulary, not
    /// the enum's. Two of the shared commands carry an underscore there, so an
    /// enum name handed straight over maps to nothing and the guest is told
    /// "NO COMMAND" for an order the animal obeyed.
    ///
    /// That failure is invisible from the host, which never takes this path.
    /// </summary>
    public sealed class VoiceResultRoundTripTests
    {
        private static readonly (CompanionCommandId Command, CompanionKind Kind)[]
            Cases =
            {
                (CompanionCommandId.Track, CompanionKind.Dog),
                (CompanionCommandId.Search, CompanionKind.Dog),
                (CompanionCommandId.Guard, CompanionKind.Dog),
                (CompanionCommandId.Bark, CompanionKind.Dog),
                (CompanionCommandId.Scout, CompanionKind.Cat),
                (CompanionCommandId.Distract, CompanionKind.Cat),
                (CompanionCommandId.Steal, CompanionKind.Cat),
                (CompanionCommandId.Hide, CompanionKind.Cat),
                (CompanionCommandId.Stop, CompanionKind.Dog),
                (CompanionCommandId.Stay, CompanionKind.Dog),
                (CompanionCommandId.FollowOwner, CompanionKind.Dog),
                (CompanionCommandId.ReturnOwner, CompanionKind.Dog),
                (CompanionCommandId.Cancel, CompanionKind.Cat)
            };

        [Test]
        public void EveryBroadcastCommandNameMapsBackToItself()
        {
            foreach ((CompanionCommandId command, CompanionKind kind) in Cases)
            {
                string name =
                    CompanionVoiceCommandBridge.IntentNameOf(command);
                Assert.That(
                    VoiceCommandMapper.TryMap(
                        name,
                        kind,
                        out CompanionCommandId mapped),
                    Is.True,
                    $"The guest is told '{name}' for {command}, and the feed's "
                    + "mapper does not know that word, so the screen says NO "
                    + "COMMAND for an order that was carried out.");
                Assert.That(mapped, Is.EqualTo(command));
            }
        }

        [Test]
        public void NoCommandCarriesNoName()
        {
            Assert.That(
                CompanionVoiceCommandBridge.IntentNameOf(
                    CompanionCommandId.None),
                Is.Empty,
                "A refusal must read as a refusal, not as a command called "
                + "NONE.");
        }
    }
}
