using FBE.Scripts.Cards;
using FBE.Scripts.Powers;
using FBE.Scripts.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;

namespace FBE.Scripts.VanillaModifiers;

/// <summary>Replaces vanilla Genesis with FBE's star-generating Genesis2.</summary>
[Pool(typeof(RegentCardPool))]
public sealed class Genesis2 : FBECardModel
{
    public Genesis2() : base(2, CardType.Power, CardRarity.Rare, TargetType.None)
    {
    }

    public override string PortraitPath =>
        ImageHelper.GetImagePath("atlases/card_atlas.sprites/regent/genesis.tres");

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("StarsPerTurn", 6m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await PowerCmd.Apply<GenesisPower2>(choiceContext, Owner.Creature,
            DynamicVars["StarsPerTurn"].BaseValue, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        EnergyCost.UpgradeBy(-1);
    }
}

/// <summary>Maintains Genesis2's stars for the current turn.</summary>
public sealed class GenesisPower2 : FBEPowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override string CustomIconPath => ImageHelper.GetImagePath("powers/genesis_power.png");

    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player)
        {
            Flash();
            await PlayerCmd.GainStars(Amount, player);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        Flash();
        var player = Owner.Player!;
        await PlayerCmd.LoseStars(player.PlayerCombatState!.Stars, player);
    }
}

/// <summary>Locks vanilla Genesis after adding the replacement to Regent's pool.</summary>
[HarmonyPatch(typeof(CardPoolModel), nameof(CardPoolModel.GetUnlockedCards))]
internal static class LockVanillaGenesis
{
    private static void Postfix(CardPoolModel __instance, ref IEnumerable<CardModel> __result)
    {
        if (__instance is RegentCardPool)
        {
            __result = __result.Where(card => card.GetType() != typeof(Genesis)).ToList();
        }
    }
}
