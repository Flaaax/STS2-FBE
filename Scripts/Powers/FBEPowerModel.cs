using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Powers;

public abstract class FBEPowerModel : ModPowerTemplate
{
    public override string? CustomIconPath => $"res://FBE/images/powers/{GetType().Name}.png";
    public override string? CustomBigIconPath => CustomIconPath;
}
