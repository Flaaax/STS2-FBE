using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace FBE.Scripts.Cards;

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(TokenCardPool))]
public class TheD6ChoiceCard() : FBECardModel(-1, CardType.Skill, CardRarity.Token, TargetType.None, false)
{
	private string? _runtimeTitle;
	public override int MaxUpgradeLevel => 0;
	public override bool CanBeGeneratedInCombat => false;
	protected override Type PortraitOverride => typeof(TheD6);

	public int Index { get; private set; }

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new StringVar("property", "unknown_property"),
		new IntVar("value", 0),
		new IntVar("MinRange", 0),
		new IntVar("MaxRange", 0)
	];

	public void Init(string property, int value, int index, int minRange, int maxRange)
	{
		AssertMutable();
		_runtimeTitle = property;
		Index = index;

		((StringVar)DynamicVars["property"]).StringValue = property;
		DynamicVars["value"].BaseValue = value;
		DynamicVars["MinRange"].BaseValue = minRange;
		DynamicVars["MaxRange"].BaseValue = maxRange;
	}

	public override string Title => _runtimeTitle ?? base.Title;

	// protected override void AddExtraArgsToDescription(LocString description)
	// {
	// 	description.Add("property", _runtimeTitle!);
	// 	description.Add("value", DynamicVars["value"].IntValue);
	// 	description.Add("MinRange", DynamicVars["MinRange"].IntValue);
	// 	description.Add("MaxRange", DynamicVars["MaxRange"].IntValue);
	// }
}

public class TheD6Base
{
	private LocString? _propertySelectionPrompt;
	private LocString? _handSelectionPrompt;
	private static readonly string Id = ModelDb.Card<TheD6ChoiceCard>().Id.Entry;

	private LocString HandSelectionPrompt =>
		_handSelectionPrompt ??= new LocString("cards", Id + ".handSelectionPrompt");

	private LocString PropertySelectionPrompt =>
		_propertySelectionPrompt ??=
			new LocString("cards", Id + ".propertySelectionPrompt");

	private static List<(string SortKey, string Name, int value, Action<int> Modifier)> GetModifiers(CardModel card)
	{
		List<(string, string, int, Action<int>)> ret = [];
		if (!card.EnergyCost.CostsX)
		{
			ret.Add(("energy", new LocString("cards", Id + ".modifier.energy").GetFormattedText(),
				card.EnergyCost.GetResolved(), // Could this work well?
				i => card.EnergyCost.SetThisCombat(i)));
		}

		foreach (var dynVar in card.DynamicVars.Values.Where(dynVar => dynVar is not StringVar))
		{
			var name = dynVar.Name;
			if (TheD6Helper.TryGetPowerVar(dynVar, out var t) && t != null && dynVar.Name == t.Name)
			{
				name = TheD6Helper.GetPower(t).Title.GetFormattedText();
			}
			else if (LocString.GetIfExists("cards", Id + ".modifier.named." + dynVar.Name) is
			         { } name1) // This means name1 is not null
			{
				name = name1.GetFormattedText();
			}

			ret.Add((dynVar.Name, name, dynVar.IntValue, i => dynVar.BaseValue = i));
		}

		return ret;
	}

	public async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay, CardModel self)
	{
		var card = (await CardSelectCmd.FromHand(
			prefs: new CardSelectorPrefs(HandSelectionPrompt, 1),
			context: choiceContext, player: self.Owner,
			filter: null,
			source: self)).FirstOrDefault();

		if (card == null)
		{
			return;
		}

		var modifiers = GetModifiers(card);

		// var selectionCount = self.IsUpgraded ? 999 : self.DynamicVars["Selections"].IntValue;
		// modifiers = modifiers.Take(selectionCount).ToList();
		var selectionCount = modifiers.Count;
		modifiers = modifiers
			.OrderBy(modifier => modifier.Name, StringComparer.Ordinal)
			.ThenBy(modifier => modifier.SortKey, StringComparer.Ordinal)
			.ToList();

		var minRange = self.DynamicVars["MinRange"].IntValue;
		var maxRange = self.DynamicVars["MaxRange"].IntValue;

		List<CardModel> selections = [];
		for (var i = 0; i < modifiers.Count; i++)
		{
			var (_, name, value, _) = modifiers[i];
			var card1 = self.CombatState!.CreateCard<TheD6ChoiceCard>(self.Owner);
			card1.Init(name, value, i, minRange, maxRange);
			selections.Add(card1);
		}

		TheD6ChoiceCard? selected;
		if (selectionCount <= 3)
		{
			selected = (TheD6ChoiceCard?)await CardSelectCmd.FromChooseACardScreen(choiceContext, selections,
				self.Owner);
		}
		else
		{
			selected = (TheD6ChoiceCard?)(await CardSelectCmd.FromSimpleGrid(choiceContext,
					selections,
					self.Owner, new CardSelectorPrefs(PropertySelectionPrompt, 1) { RequireManualConfirmation = true }))
				.FirstOrDefault();
		}

		if (selected == null)
		{
			return;
		}

		var action = modifiers[selected.Index].Item4;
		var rollValue = self.Owner.RunState.Rng.CombatCardSelection.NextInt(minRange, maxRange + 1);
		action(rollValue);
	}
}

[STS2RitsuLib.Interop.AutoRegistration.RegisterCard(typeof(ColorlessCardPool))]
public class TheD6() : FBECardModel(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
{
	private readonly TheD6Base _myBase = new();

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		// new IntVar("Selections", 2),
		new IntVar("MinRange", 1),
		new IntVar("MaxRange", 6)
	];

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await _myBase.OnPlay(choiceContext, cardPlay, this);
	}

	protected override void OnUpgrade()
	{
		EnergyCost.UpgradeBy(-1);
		//DynamicVars["StaticDischargePower"].UpgradeValueBy(1m);
	}
}
