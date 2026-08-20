using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Enchantments;

// ReSharper disable once InconsistentNaming
[STS2RitsuLib.Interop.AutoRegistration.RegisterEnchantment]
public sealed class UbwEnchantment : FBEEnchantmentModel
{
	public override string CustomIconPath => "res://FBE/images/powers/UnlimitedBladeWorksPower.png";

	public override decimal EnchantDamageMultiplicative(decimal originalDamage, ValueProp props)
	{
		return !props.IsPoweredAttack() ? 1m : 1m / Amount;
	}

	public override bool CanEnchant(CardModel card)
	{
		return card is SovereignBlade;
	}
}