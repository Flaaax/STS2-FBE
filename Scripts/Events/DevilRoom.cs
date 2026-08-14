using System.Diagnostics;
using FBE.Scripts.Cards;
using FBE.Scripts.Relics;
using FBE.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Events;

[STS2RitsuLib.Interop.AutoRegistration.RegisterSharedEvent]
public sealed class DevilRoom : FBEEventModel
{
	private const string DevilRoomMusicPath = "res://FBE/audio/deal with the devil.ogg";

	private bool _gameBgmSuppressed;
	private bool _runExitCleanupRegistered;

	public override string CustomInitialPortraitPath => "res://FBE/images/events/DevilRoom1.png";

	protected override IEnumerable<DynamicVar> CanonicalVars => [];

	public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex == 1;

	protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
	[
		Option(Enter),
		Option(WalkAway, HoverTipFactory.FromRelic<Redemption>())
	];

	public override void OnRoomEnter()
	{
		PlayLocalSfx("res://FBE/audio/devil room appear.wav");
	}

	protected override void OnEventFinished()
	{
		StopDevilRoomMusic();
	}

	private Task Enter()
	{
		SetLocalPortrait("res://FBE/images/events/DevilRoom2.png");

		var goods = RollGoods();
		for (var i = 0; i < goods.Length; i++)
		{
			_goodsOptionCaches[i] = MakeOptionCache(goods[i], i);
		}

		SetEventState(PageDescription("ENTER"), MakeOptionsFromCache());

		StartDevilRoomMusic();

		return Task.CompletedTask;
	}

	private async Task WalkAway()
	{
		await RelicCmd.Obtain<Redemption>(Owner!);
		SetEventFinished(PageDescription("WALKAWAY_CHOSEN"));
	}

	private enum Goods
	{
		RareCard,
		Relic2,

		CardDelete,
		CardReward,
		CardUpgrade,
		CardTransform,
		Relic1,
	}

	private enum Costs
	{
		Damage,
		MaxHealth,
		Curse
	}

	private record OptionCache
	{
		public Goods Good;
		public LocString? Title;
		public LocString? Description;
		public Func<Task>? OnChosen;
		public Costs Cost;
		public int Value;
		public IEnumerable<IHoverTip>? HoverTips;
	}

	private const int InitialGoodsBackup = 2;
	private const int GoodsSlotCount = 3;
	private const int MaxPurchase = InitialGoodsBackup + GoodsSlotCount;

	private int _goodsBackup = InitialGoodsBackup;
	private OptionCache?[] _goodsOptionCaches = new OptionCache?[GoodsSlotCount];
	private int _purchaseCount;

	protected override void AfterCloned()
	{
		base.AfterCloned();
		ResetGoodsState();
	}

	private void ResetGoodsState()
	{
		_goodsBackup = InitialGoodsBackup;
		_goodsOptionCaches = new OptionCache?[GoodsSlotCount];
		_purchaseCount = 0;
	}

	private static bool IsExpensive(Goods goods)
	{
		return (int)goods <= 2;
	}

	private Goods[] RollGoods()
	{
		var allGoods = Enum.GetValues<Goods>();
		allGoods = allGoods.Except([Goods.CardDelete]).ToArray();
		var expensiveGoods = allGoods.Where(IsExpensive).ToList();
		var cheapGoods = allGoods.Except(expensiveGoods).ToList();
		var first = TakeRandom(cheapGoods);
		var third = TakeRandom(expensiveGoods);
		var remainingGoods = allGoods.Except([first, third]).ToList();
		var second = TakeRandom(remainingGoods);

		return [first, second, third];

		Goods TakeRandom(List<Goods> list)
		{
			var index = Rng.NextInt(0, list.Count);
			var result = list[index];
			list.RemoveAt(index);
			return result;
		}
	}

	private Goods RollReplacementGood(int index)
	{
		var unavailableGoods = new HashSet<Goods>();
		for (var i = 0; i < _goodsOptionCaches.Length; i++)
		{
			if (i == index)
			{
				continue;
			}

			var cache = _goodsOptionCaches[i];
			if (cache != null)
			{
				unavailableGoods.Add(cache.Good);
			}
		}

		var candidates = Enum.GetValues<Goods>()
			.Where(good => !unavailableGoods.Contains(good))
			.ToList();
		return candidates[Rng.NextInt(0, candidates.Count)];
	}

	private (Func<Task>, LocString) MakeCost(Costs cost, int amount = 0)
	{
		var costString = cost switch
		{
			Costs.Damage => L10NLookup($"{Id.Entry}.costs.damage"),
			Costs.MaxHealth => L10NLookup($"{Id.Entry}.costs.maxHealth"),
			Costs.Curse => L10NLookup($"{Id.Entry}.costs.curse"),
			_ => throw new ArgumentOutOfRangeException(nameof(cost), cost, null)
		};

		switch (cost)
		{
			case Costs.Damage:
				costString.Add("damage", amount);
				break;
			case Costs.MaxHealth:
				costString.Add("maxHealth", amount);
				break;
		}

		Func<Task> costFunc = cost switch
		{
			Costs.Damage => async () =>
			{
				await CreatureCmd.Damage(
					new ThrowingPlayerChoiceContext(),
					Owner!.Creature,
					amount,
					ValueProp.Unblockable | ValueProp.Unpowered,
					null,
					null
				);
			},

			Costs.MaxHealth => async () =>
			{
				await CreatureCmd.LoseMaxHp(
					new ThrowingPlayerChoiceContext(),
					Owner!.Creature,
					amount,
					isFromCard: false
				);
			},

			Costs.Curse => async () =>
			{
				var availableCurses =
					from c in ModelDb.CardPool<CurseCardPool>()
						.GetUnlockedCards(Owner!.UnlockState, Owner.RunState.CardMultiplayerConstraint)
					where c.CanBeGeneratedByModifiers
					select c;

				var cardModel = Rng.NextItem(availableCurses);
				var card = Owner.RunState.CreateCard(cardModel!, Owner);

				CardCmd.PreviewCardPileAdd(
					[await CardPileCmd.Add(card, PileType.Deck)],
					2f
				);
			},

			_ => throw new ArgumentOutOfRangeException(nameof(cost), cost, null)
		};

		return (costFunc, costString);
	}

	private (Func<Task>, IEnumerable<IHoverTip>) MakeRewardFunc(Goods good)
	{
		return good switch
		{
			Goods.CardDelete => (async () =>
			{
				var cards = (await CardSelectCmd.FromDeckForRemoval(Owner!,
						prefs: new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1)))
					.ToList();
				await CardPileCmd.RemoveFromDeck(cards);
			}, []),
			Goods.CardReward => (async () =>
			{
				var options = new CardCreationOptions([Owner!.Character.CardPool], CardCreationSource.Other,
					CardRarityOddsType.RegularEncounter);
				await RewardsCmd.OfferCustom(Owner, [new CardReward(options, 3, Owner)]);
			}, []),
			Goods.CardUpgrade => (async () =>
			{
				var cardModel = (await CardSelectCmd.FromDeckForUpgrade(Owner!,
					new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1))).FirstOrDefault();
				if (cardModel != null) CardCmd.Upgrade(cardModel);
			}, []),
			Goods.CardTransform => (async () =>
			{
				var item = (await CardSelectCmd.FromDeckForTransformation(
						prefs: new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1), player: Owner!))
					.FirstOrDefault();
				if (item != null) await CardCmd.TransformToRandom(item, Rng, CardPreviewStyle.EventLayout);
			}, [HoverTipFactory.Static(StaticHoverTip.Transform)]),
			Goods.Relic1 => (
				async () => { await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner!).ToMutable(), Owner!); },
				[]),
			Goods.RareCard => (async () =>
			{
				var options = new CardCreationOptions([Owner!.Character.CardPool], CardCreationSource.Other,
					CardRarityOddsType.BossEncounter);
				await RewardsCmd.OfferCustom(Owner, [new CardReward(options, 3, Owner)]);
			}, []),
			Goods.Relic2 => (async () =>
			{
				await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner!).ToMutable(), Owner!);
				await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner!).ToMutable(), Owner!);
			}, []),
			_ => throw new ArgumentOutOfRangeException(nameof(good), good, null)
		};
	}

	private Task LeaveRoom()
	{
		SetEventFinished(PageDescription(_purchaseCount > 0 ? "LEAVE_ROOM_CHOSEN" : "NO_PURCHASE"));
		StopDevilRoomMusic();
		SetLocalPortrait(CustomInitialPortraitPath);
		return Task.CompletedTask;
	}

	private Func<Task> MakeOnChosen(Func<Task> costFunc, Func<Task> rewardFunc, int index)
	{
		return async () =>
		{
			await rewardFunc();
			//await Cmd.Wait(0.25f);
			PlayLocalSfx("res://FBE/audio/devil room deal.wav");
			await costFunc();
			if (Owner!.Creature.IsDead)
			{
				SetEventFinished(L10NLookup($"{Id.Entry}.loss"));
				return;
			}

			_purchaseCount++;
			if (_goodsBackup > 0)
			{
				_goodsBackup--;
				_goodsOptionCaches[index] = MakeOptionCache(RollReplacementGood(index), index);
			}
			else
			{
				_goodsOptionCaches[index] = null;
			}

			var pageKey = _purchaseCount switch
			{
				0 => "ENTER",
				MaxPurchase => "SOLD_OUT",
				_ => "PURCHASE"
			};

			SetEventState(PageDescription(pageKey), MakeOptionsFromCache());
		};
	}

	private LocString GetRewardString(Goods good)
	{
		return L10NLookup($"{Id.Entry}.goods.{good.ToString()}");
	}

	private List<EventOption> MakeOptionsFromCache()
	{
		List<EventOption> options = [];
		foreach (var cache in _goodsOptionCaches)
		{
			if (cache == null)
			{
				options.Add(new EventOption(this, null, $"{Id.Entry}.pages.ENTER.options.SOLD_OUT"));
			}
			else
			{
				var option = new EventOption(this, cache.OnChosen, cache.Title!, cache.Description!,
					$"{Id.Entry}.pages.ENTER.options.GOODS", cache.HoverTips!);
				option = cache.Cost switch
				{
					Costs.Damage => option.ThatDoesDamage(cache.Value),
					Costs.MaxHealth => option.ThatDecreasesMaxHp(cache.Value),
					_ => option
				};
				options.Add(option);
			}
		}

		options.Add(new EventOption(this, LeaveRoom, $"{Id.Entry}.pages.ENTER.options.LEAVE_ROOM"));
		return options;
	}

	private OptionCache MakeOptionCache(Goods good, int index)
	{
		OptionCache ret = new()
		{
			Good = good
		};

		Func<Task>? costFunc;
		var (rewardFunc, hoverTips) = MakeRewardFunc(good);
		ret.HoverTips = hoverTips;
		ret.Title = L10NLookup($"{Id.Entry}.titles.{good.ToString()}");
		LocString? costString;
		var rewardString = GetRewardString(good);

		if (!IsExpensive(good))
		{
			if (Rng.NextFloat() < 0.5)
			{
				var damage = Rng.NextInt(6, 11);
				(costFunc, costString) = MakeCost(Costs.Damage, damage);
				ret.Value = damage;
				ret.Cost = Costs.Damage;
			}
			else
			{
				var health = Rng.NextInt(4, 6);
				(costFunc, costString) = MakeCost(Costs.MaxHealth, health);
				ret.Value = health;
				ret.Cost = Costs.MaxHealth;
			}
		}
		else
		{
			if (Rng.NextFloat() < 0.3)
			{
				var damage = Rng.NextInt(13, 16);
				(costFunc, costString) = MakeCost(Costs.Damage, damage);
				ret.Value = damage;
				ret.Cost = Costs.Damage;
			}
			else if (Rng.NextFloat() < 0.8)
			{
				var health = Rng.NextInt(8, 11);
				(costFunc, costString) = MakeCost(Costs.MaxHealth, health);
				ret.Value = health;
				ret.Cost = Costs.MaxHealth;
			}
			else
			{
				(costFunc, costString) = MakeCost(Costs.Curse);
				ret.Cost = Costs.Curse;
			}
		}

		ret.OnChosen = MakeOnChosen(costFunc, rewardFunc, index);
		ret.Description = L10NLookup($"{Id.Entry}.pages.ENTER.options.GOODS.description");
		ret.Description.Add("cost", costString);
		ret.Description.Add("reward", rewardString);

		return ret;
	}

	private void StartDevilRoomMusic()
	{
		if (!IsLocalOwner())
		{
			return;
		}

		var audioManager = NAudioManager.Instance;
		if (audioManager != null)
		{
			audioManager.SetBgmVol(0f);
			_gameBgmSuppressed = true;
		}

		AudioHelper.PlayLoop(DevilRoomMusicPath);
		RegisterRunExitCleanup();
	}

	private void StopDevilRoomMusic()
	{
		if (!IsLocalOwner() && !_runExitCleanupRegistered)
		{
			return;
		}

		UnregisterRunExitCleanup();
		AudioHelper.StopLoop(DevilRoomMusicPath);

		if (!_gameBgmSuppressed)
		{
			return;
		}

		NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);
		_gameBgmSuppressed = false;
	}

	private void RegisterRunExitCleanup()
	{
		if (_runExitCleanupRegistered)
		{
			return;
		}

		var run = NRun.Instance;
		if (run == null)
		{
			return;
		}

		run.TreeExiting += StopDevilRoomMusic;
		_runExitCleanupRegistered = true;
	}

	private void UnregisterRunExitCleanup()
	{
		if (!_runExitCleanupRegistered)
		{
			return;
		}

		var run = NRun.Instance;
		if (run != null && GodotObject.IsInstanceValid(run))
		{
			run.TreeExiting -= StopDevilRoomMusic;
		}

		_runExitCleanupRegistered = false;
	}

	private void PlayLocalSfx(string audioPath)
	{
		if (!IsLocalOwner())
		{
			return;
		}

		AudioHelper.Play(audioPath);
	}
}
