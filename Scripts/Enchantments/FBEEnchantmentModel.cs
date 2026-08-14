using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Enchantments;

// ReSharper disable once InconsistentNaming
public abstract class FBEEnchantmentModel : ModEnchantmentTemplate
{
    public override string? CustomIconPath => $"res://FBE/images/enchantments/{GetType().Name}.png";
}
