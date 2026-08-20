using FBECore.Scripts.Multiplayer;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

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

		var candidates = await GetAuthoritativeFormCards();
		var cards = CardFactory.GetDistinctForCombat(
			Owner,
			candidates.Select(GetCanonicalCandidate),
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

	private static CardModel GetCanonicalCandidate(SerializableCard candidate)
	{
		if (candidate.Id is not { } id)
		{
			throw new InvalidOperationException("Clicker received a card candidate without an id.");
		}

		return ModelDb.GetById<CardModel>(id);
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

	/// <summary>
	/// Returns snapshots of every card that this relic can generate in the current language.
	/// The underlying candidate list is shared with combat generation and cached per language.
	/// </summary>
	internal IEnumerable<SerializableCard> GetFormCardPreviews()
	{
		return CachedFormCards.Select(card => card.ToMutable().ToSerializable());
	}

	private async Task<IReadOnlyList<SerializableCard>> GetAuthoritativeFormCards()
	{
		var runManager = RunManager.Instance;
		var actionId = runManager.ActionExecutor.CurrentlyRunningAction?.Id;
		if (runManager.NetService.Type is not NetGameType.Singleplayer and not NetGameType.Replay &&
		    actionId is null)
		{
			throw new InvalidOperationException(
				"Clicker requires a synchronized game action id in multiplayer.");
		}

		var relicIndex = Owner.Relics.IndexOf(this);
		if (relicIndex < 0)
			throw new InvalidOperationException("Clicker must be in its owner's relic inventory before it generates cards.");

		var key = new CardCandidateSyncKey(
			"FBE.Clicker.v1",
			Owner.NetId,
			actionId ?? 0,
			(uint)relicIndex,
			0,
			Owner.RunState.RunLocation);
		return await AuthoritativeCardCandidateSync.GetCandidates(
			CardCandidateAuthority.Host,
			key,
			() => CachedFormCards
				.Select(card => card.ToMutable().ToSerializable())
				.ToList());
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
