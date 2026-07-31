using System.Reflection;
using FBE.Scripts;
using FBE.Scripts.Cards;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace FBE.Scripts.Patches;

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayFinished))]
internal static class InvestmentPromotionDynamicPreview
{
	private static AccessTools.FieldRef<NCardHolder, bool> _isFocused = null!;
	private static MethodInfo _createHoverTips = null!;

	private static bool Prepare()
	{
		if (!Entry.EnableInvestmentPromotionDynamicPreview) return false;

		_isFocused = AccessTools.FieldRefAccess<NCardHolder, bool>("_isFocused");
		_createHoverTips = AccessTools.DeclaredMethod(typeof(NCardHolder), "CreateHoverTips");
		return true;
	}

	private static void Postfix(CardPlay cardPlay)
	{
		var hand = NPlayerHand.Instance;
		if (hand is null) return;

		foreach (var holder in hand.ActiveHolders)
		{
			if (holder.CardModel is not InvestmentPromotion investmentPromotion) continue;
			if (cardPlay.Card.Owner == investmentPromotion.Owner) continue;
			if (!_isFocused(holder)) continue;

			holder.UpdateCard();
			NHoverTipSet.Remove(holder);
			_createHoverTips.Invoke(holder, null);
		}
	}
}
