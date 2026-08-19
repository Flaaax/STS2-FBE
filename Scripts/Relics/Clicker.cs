using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace FBE.Scripts.Relics;

[STS2RitsuLib.Interop.AutoRegistration.RegisterRelic(typeof(EventRelicPool))]
public sealed class Clicker : FBERelicModel
{
	private static readonly object FormCardsLock = new();
	private static readonly Dictionary<string, IReadOnlyList<CardModel>> FormCardsByLanguage =
		new(StringComparer.Ordinal);

	public override RelicRarity Rarity => RelicRarity.Ancient;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(3)
	];

	protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
	[
		HoverTipFactory.FromKeyword(CardKeyword.Ethereal)
	];

	public override Task AfterObtained()
	{
		// Build this locale-specific lookup outside combat. It is shared by all
		// Clickers; a game loaded after restart only needs this one-time recovery.
		_ = CachedFormCards;
		return Task.CompletedTask;
	}

	public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants,
		ICombatState combatState)
	{
		if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState!.TurnNumber > 1)
		{
			return;
		}

		var cards = CardFactory.GetDistinctForCombat(
			Owner,
			CachedFormCards,
			DynamicVars.Cards.IntValue,
			Owner.RunState.Rng.CombatCardGeneration).ToList();

		if (cards.Count == 0)
		{
			return;
		}

		foreach (var card in cards)
		{
			CardCmd.ApplyKeyword(card, CardKeyword.Ethereal);
			card.EnergyCost.AddThisCombat(-1, reduceOnly: true);
		}

		Flash();
		await CardPileCmd.AddGeneratedCardsToCombat(cards, PileType.Hand, Owner);
	}

	private static IReadOnlyList<CardModel> CachedFormCards
	{
		get
		{
			var language = LocManager.Instance.Language;

			lock (FormCardsLock)
			{
				if (FormCardsByLanguage.TryGetValue(language, out var cards))
				{
					return cards;
				}

				cards = FindFormCards(language);
				FormCardsByLanguage.Add(language, cards);
				return cards;
			}
		}
	}

	private static IReadOnlyList<CardModel> FindFormCards(string language)
	{
		return language switch
		{
			"eng" => ModelDb.AllCards
				.Where(card => card.Title.Contains("Form", StringComparison.OrdinalIgnoreCase))
				.ToList(),
			"zhs" => ModelDb.AllCards
				.Where(card => card.Title.Contains("形态", StringComparison.Ordinal))
				.ToList(),
			_ =>
			[
				ModelDb.Card<DemonForm>(),
				ModelDb.Card<VoidForm>(),
				ModelDb.Card<SerpentForm>(),
				ModelDb.Card<EchoForm>(),
				ModelDb.Card<ReaperForm>(),
				ModelDb.Card<WraithForm>()
			]
		};
	}
}
