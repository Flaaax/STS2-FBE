using System.Reflection;
using FBE.Scripts.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Cards;

// ReSharper disable once InconsistentNaming
public abstract class FBECardModel : ModCardTemplate
{
    protected FBECardModel(int energyCost,
        CardType type,
        CardRarity rarity,
        TargetType targetType,
        bool shouldShowInCardLibrary = true) : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    protected virtual Type PortraitOverride => GetType();
    public override string PortraitPath => $"res://FBE/images/cards/{PortraitOverride.Name}.png";
    public override string? CustomPortraitPath => PortraitPath;
}
