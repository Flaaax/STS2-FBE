using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace FBE.Scripts.Powers;

/// <summary>教条本体的纯展示能力，为后续机制预留。</summary>
[RegisterPower]
public sealed class DogmaPower : FBEPowerModel
{
	public override PowerType Type => PowerType.Buff;
	public override PowerStackType StackType => PowerStackType.Single;
}
