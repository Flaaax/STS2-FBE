using FBE.Scripts.Rewards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(SharedRelicPool))]
class StarterDeck : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new IntVar("ForeignPoolChance", 10m)
	];

	public override Task AfterCombatEnd(CombatRoom room)
	{
		room.AddExtraReward(
			Owner,
			StarterDeckBundleReward.Create(Owner, room, DynamicVars["ForeignPoolChance"].IntValue));
		return Task.CompletedTask;
	}
}
