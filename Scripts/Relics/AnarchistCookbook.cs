using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
class AnarchistCookbook : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<MayhemPower>(1m),
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
	[
		HoverTipFactory.FromPower<MayhemPower>(),
		HoverTipFactory.FromCard<Mayhem>()
	];


	public override async Task AfterRoomEntered(AbstractRoom room)
	{
		if (room is CombatRoom)
		{
			await PowerCmd.Apply<MayhemPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
				DynamicVars["MayhemPower"].BaseValue, Owner.Creature, null);
		}
	}

	public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext,
		ICombatState combatState)
	{
		if (player == Owner && Owner.PlayerCombatState!.TurnNumber <= 1 && !CombatManager.Instance.IsOverOrEnding)
		{
			Flash();
			var card = combatState.CreateCard<Mayhem>(Owner);
			await CardPileCmd.AddGeneratedCardsToCombat([card], PileType.Hand, Owner);
		}
	}
}
