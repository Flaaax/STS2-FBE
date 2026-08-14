using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content;

namespace FBE.Scripts.Relics;

public abstract class FBERelicModel : ModRelicTemplate
{
	public override string? CustomIconPath => $"res://FBE/images/relics/{GetType().Name}.png";
    public override string? CustomIconOutlinePath => CustomIconPath;
    public override string? CustomBigIconPath => CustomIconPath;
}
