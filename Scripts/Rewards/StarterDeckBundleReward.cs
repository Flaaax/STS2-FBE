using System.Text.Json.Serialization;
using FBE.Scripts.Relics;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Combat.Rewards;

namespace FBE.Scripts.Rewards;

internal sealed class StarterDeckBundleReward : ModCustomReward
{
	private const int CardsPerBundle = 3;
	private static ModRewardDefinition? _definition;

	private readonly IReadOnlyList<IReadOnlyList<CardModel>> _bundles;

	private StarterDeckBundleReward(Player player, IReadOnlyList<IReadOnlyList<CardModel>> bundles)
		: base(player)
	{
		_bundles = bundles;
	}

	public override RewardType ModRewardType => _definition?.RewardType
		?? throw new InvalidOperationException("Starter Deck bundle reward has not been registered.");

	public override int RewardsSetIndex => 5;

	protected override string DescriptionLocTable => "relics";
	protected override string DescriptionLocKey => "FBE_REWARD_STARTER_DECK_BUNDLE.description";
	protected override string? RewardIconPath => ImageHelper.GetImagePath("ui/reward_screen/reward_icon_card.png");

	public static void Register()
	{
		_definition = ModRewardRegistry.For(Entry.ModId).RegisterOwned(
			"STARTER_DECK_BUNDLE",
			StarterDeckBundleRewardJsonContext.Default.StarterDeckBundleRewardPayload,
			static (_, player, payload) => new StarterDeckBundleReward(player, RestoreBundles(player, payload)));
	}

	public static StarterDeckBundleReward Create(Player player, CombatRoom room, int foreignPoolChance)
	{
		var bundles = GenerateBundles(player, room.RoomType, foreignPoolChance);
		return new StarterDeckBundleReward(player, bundles);
	}

	protected override async Task<bool> OnSelect()
	{
		var selectedBundle = await CardSelectHelper.FromChooseOptionalBundleScreen(Player, _bundles);
		if (selectedBundle is null)
		{
			return false;
		}

		var chosenCards = new List<CardModel>(selectedBundle.Count);
		foreach (var card in selectedBundle)
		{
			var result = await CardPileCmd.Add(card, PileType.Deck);
			if (result.success)
			{
				chosenCards.Add(result.cardAdded);
			}
		}

		RecordChosenCards(chosenCards);
		return true;
	}

	public override void OnSkipped()
	{
		RecordSkippedCards();
	}

	public override void MarkContentAsSeen()
	{
	}

	public override SerializableReward ToSerializable()
	{
		var payload = new StarterDeckBundleRewardPayload
		{
			Bundles = _bundles
				.Select(bundle => bundle.Select(card => card.ToSerializable()).ToList())
				.ToList()
		};

		return ToSerializable(payload, StarterDeckBundleRewardJsonContext.Default.StarterDeckBundleRewardPayload);
	}

	private static IReadOnlyList<IReadOnlyList<CardModel>> GenerateBundles(
		Player player,
		RoomType roomType,
		int foreignPoolChance)
	{
		foreignPoolChance = Math.Clamp(foreignPoolChance, 0, 100);
		var foreignPools = ModelDb.AllCharacterCardPools
			.Where(pool => pool != player.Character.CardPool)
			.Append(ModelDb.CardPool<ColorlessCardPool>())
			.Distinct()
			.ToList();

		var foreignPoolHasCards = foreignPools
			.SelectMany(pool => pool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint))
			.Any();

		var bundles = new List<IReadOnlyList<CardModel>>(2);
		for (var bundleIndex = 0; bundleIndex < 2; bundleIndex++)
		{
			var cards = new List<CardCreationResult>(CardsPerBundle);
			var blacklist = new List<CardModel>(CardsPerBundle);

			for (var cardIndex = 0; cardIndex < CardsPerBundle; cardIndex++)
			{
				var useForeignPool = foreignPoolHasCards && foreignPoolChance > 0 &&
					player.PlayerRng.Rewards.NextInt(100) < foreignPoolChance;
				var pools = useForeignPool ? foreignPools : [player.Character.CardPool];
				var options = CardCreationOptions.ForRoom(player, roomType)
					.WithCardPools(pools)
					.WithFlags(CardCreationFlags.IsCardReward
#if STS2_Beta
						| CardCreationFlags.IsFromCombat
#endif
					);

				var result = CreateCardForReward(player, blacklist, options);
				cards.Add(result);
				blacklist.Add(result.Card.CanonicalInstance);
			}

			var hookOptions = CardCreationOptions.ForRoom(player, roomType)
				.WithFlags(CardCreationFlags.IsCardReward
#if STS2_Beta
					| CardCreationFlags.IsFromCombat
#endif
				);
			if (!hookOptions.Flags.HasFlag(CardCreationFlags.NoModifyHooks) &&
				Hook.TryModifyCardRewardOptions(player.RunState, player, cards, hookOptions, out var modifiers))
			{
				TaskHelper.RunSafely(Hook.AfterModifyingCardRewardOptions(player.RunState, modifiers));
			}

			bundles.Add(cards.Select(result => result.Card).ToList());
		}

		return bundles;
	}

	// This mirrors CardFactory's normal reward generation, while allowing each card to use its own pool.
	private static CardCreationResult CreateCardForReward(
		Player player,
		IReadOnlyCollection<CardModel> blacklist,
		CardCreationOptions options)
	{
		options = Hook.ModifyCardRewardCreationOptions(player.RunState, player, options);
		var possibleCards = options.GetPossibleCards(player)
			.Except(blacklist)
			.Where(card => IsAvailableForPlayerCount(player, card))
			.ToList();
		IEnumerable<CardModel> choices;
		if (options.RarityOdds == CardRarityOddsType.Uniform)
		{
			choices = possibleCards.Where(card => card.Rarity is not CardRarity.Basic and not CardRarity.Ancient);
		}
		else
		{
			var allowedRarities = possibleCards.Select(card => card.Rarity).ToHashSet();
			var rarity = RollRewardRarity(player, options, allowedRarities);
			if (rarity == CardRarity.None)
			{
				throw new InvalidOperationException("Unable to generate a valid card rarity for Starter Deck.");
			}

			choices = possibleCards.Where(card => card.Rarity == rarity);
		}

		var rng = options.RngOverride ?? player.PlayerRng.Rewards;
		var canonicalCard = rng.NextItem(choices)
			?? throw new InvalidOperationException("Unable to generate a valid card for Starter Deck.");
		var card = player.RunState.CreateCard(canonicalCard, player);
		if (!options.Flags.HasFlag(CardCreationFlags.NoUpgradeRoll))
		{
			RollForUpgrade(player, card, rng);
		}

		return new CardCreationResult(card);
	}

	private static bool IsAvailableForPlayerCount(Player player, CardModel card)
	{
		return player.RunState.Players.Count switch
		{
			> 1 => card.MultiplayerConstraint != CardMultiplayerConstraint.SingleplayerOnly,
			_ => card.MultiplayerConstraint != CardMultiplayerConstraint.MultiplayerOnly
		};
	}

	private static CardRarity RollRewardRarity(
		Player player,
		CardCreationOptions options,
		ISet<CardRarity> allowedRarities)
	{
		var shouldModifyFutureOdds = options.Flags.HasFlag(CardCreationFlags.ForceRarityOddsChange) ||
			(options.Source == CardCreationSource.Encounter &&
			 options.RarityOdds is CardRarityOddsType.RegularEncounter or CardRarityOddsType.EliteEncounter or CardRarityOddsType.BossEncounter);
		var rolledRarity = shouldModifyFutureOdds
			? player.PlayerOdds.CardRarity.Roll(options.RarityOdds)
			: player.PlayerOdds.CardRarity.RollWithBaseOdds(options.RarityOdds);

		var seenRarities = new HashSet<CardRarity>();
		while (!allowedRarities.Contains(rolledRarity) && rolledRarity != CardRarity.None)
		{
			if (!seenRarities.Add(rolledRarity))
				return CardRarity.None;

			rolledRarity = rolledRarity.GetNextHighestRarityWithWrapping();
		}

		return rolledRarity;
	}

	private static void RollForUpgrade(Player player, CardModel card, MegaCrit.Sts2.Core.Random.Rng rng)
	{
		var roll = (decimal)rng.NextFloat();
		if (!card.IsUpgradable)
			return;

		var chance = card.Rarity == CardRarity.Rare
			? 0m
			: player.RunState.CurrentActIndex * AscensionHelper.GetValueIfAscension(AscensionLevel.Scarcity, 0.125m, 0.25m);
		chance = Hook.ModifyCardRewardUpgradeOdds(player.RunState, player, card, chance);
		if (roll <= chance)
			CardCmd.Upgrade(card);
	}

	private static IReadOnlyList<IReadOnlyList<CardModel>> RestoreBundles(
		Player player,
		StarterDeckBundleRewardPayload? payload)
	{
		if (payload?.Bundles is null || payload.Bundles.Count != 2 || payload.Bundles.Any(bundle => bundle.Count != CardsPerBundle))
		{
			throw new InvalidOperationException("Starter Deck reward save data is invalid.");
		}

		return payload.Bundles
			.Select(bundle => (IReadOnlyList<CardModel>)bundle.Select(card => player.RunState.LoadCard(card, player)).ToList())
			.ToList();
	}

	private void RecordChosenCards(IEnumerable<CardModel> chosenCards)
	{
		var chosen = chosenCards.ToHashSet();
		var history = Player.RunState.CurrentMapPointHistoryEntry?.GetEntry(Player.NetId);
		if (history is null)
			return;

		foreach (var card in _bundles.SelectMany(bundle => bundle))
		{
			history.CardChoices.Add(new CardChoiceHistoryEntry(card, chosen.Contains(card)));
		}
	}

	private void RecordSkippedCards()
	{
		var history = Player.RunState.CurrentMapPointHistoryEntry?.GetEntry(Player.NetId);
		if (history is null)
			return;

		foreach (var card in _bundles.SelectMany(bundle => bundle))
		{
			history.CardChoices.Add(new CardChoiceHistoryEntry(card, wasPicked: false));
		}
	}
}

internal sealed class StarterDeckBundleRewardPayload
{
	public List<List<SerializableCard>> Bundles { get; init; } = [];
}

[JsonSerializable(typeof(StarterDeckBundleRewardPayload))]
internal sealed partial class StarterDeckBundleRewardJsonContext : JsonSerializerContext;
