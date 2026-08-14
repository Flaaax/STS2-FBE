using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(SharedRelicPool))]
class StarterDeck : FBERelicModel
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	public override async Task AfterObtained()
	{
		var bundles = GenerateRandomBundles(Owner)
			.Select(pack => pack
				.Select(c => Owner.RunState.CreateCard(c, Owner))
				.ToList())
			.ToList();

		if (bundles.Count == 0)
		{
			Log.Warn("No card options!");
			return;
		}

		var selectedBundles = await CardSelectHelper.FromChooseBundlesScreen(Owner, bundles, 3);
		var selected = selectedBundles.SelectMany(bundle => bundle).ToList();

		foreach (var card in selected)
		{
			await CardPileCmd.Add(card, PileType.Deck);
		}
	}

	private static List<List<CardModel>> GenerateRandomBundles(Player player)
	{
		var rewards = player.PlayerRng.Rewards;
		var cardPool = player.Character.CardPool;

		var options1 = CardCreationOptions
			.ForNonCombatWithUniformOdds([cardPool], c => c.Rarity == CardRarity.Uncommon)
			.WithFlags(CardCreationFlags.NoRarityModification);

		var options2 = CardCreationOptions.ForNonCombatWithUniformOdds([cardPool], c => c.Rarity == CardRarity.Rare)
			.WithFlags(CardCreationFlags.NoRarityModification);

		var options3 = CardCreationOptions.ForNonCombatWithUniformOdds([ModelDb.CardPool<ColorlessCardPool>()]);

		CardCreationOptions[] options = [options1, options2, options3];

		for (var i = 0; i < options.Length; i++)
		{
			options[i] = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options[i]);
		}

		var sources = options.Select(opt => opt.GetPossibleCards(player).ToList());

		List<List<CardModel>> ret = [];
		var usedCardIds = new HashSet<ModelId>();

		foreach (var source in sources)
		{
			for (var i = 0; i < 2; i++)
			{
				var selections = source.Where(c => !usedCardIds.Contains(c.Id)).ToList();
				List<CardModel> cardPack = [];
				for (var j = 0; j < 3; j++)
				{
					var card = rewards.NextItem(selections);
					if (card == null)
					{
						return [];
					}

					cardPack.Add(card);
					usedCardIds.Add(card.Id);
					selections.Remove(card);
				}

				ret.Add(cardPack);
			}
		}

		return ret;
	}
}
