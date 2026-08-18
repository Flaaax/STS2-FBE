using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace FBE.Scripts.VFX;

/// <summary>
/// Keeps the base game's Hyperbeam behaviour, while making this monster's instance purple.
/// The line material is duplicated per cast so Defect's shared material is never changed.
/// </summary>
public static class DoorMonsterHyperbeamVfx
{
	private const string PurpleLaserLutPath = "res://FBE/materials/vfx/door_monster_hyperbeam_laser_lut.tres";
	private const string LaserLinePath = "laser/vfx_hyperbeam_laser_line";

	private static readonly StringName LutParameter = new("lut");
	private static readonly Color PurpleTint = new(0.86f, 0.32f, 1f, 1f);
	// 门的发射口相对于原版 VFX 锚点的偏移；X 正向右，Y 正向下。
	private static readonly Vector2 BeamSourceOffset = new(0f, -100.0f);
	public static float Duration =>
		NHyperbeamVfx.hyperbeamAnticipationDuration + NHyperbeamVfx.hyperbeamLaserDuration + 2f;

	public static NHyperbeamVfx? CreateBeam(Creature source, Creature target)
	{
		if (!TryGetBeamEndpoints(source, target, out Vector2 sourcePosition, out Vector2 targetPosition))
			return null;

		NHyperbeamVfx? beam = NHyperbeamVfx.Create(sourcePosition, targetPosition);
		if (beam == null)
			return null;

		beam.Modulate = PurpleTint;
		ApplyPurpleLaserPalette(beam);
		return beam;
	}

	public static NHyperbeamImpactVfx? CreateImpact(Creature source, Creature target)
	{
		if (!TryGetBeamEndpoints(source, target, out Vector2 sourcePosition, out Vector2 targetPosition))
			return null;

		NHyperbeamImpactVfx? impact = NHyperbeamImpactVfx.Create(sourcePosition, targetPosition);
		if (impact != null)
			impact.Modulate = PurpleTint;

		return impact;
	}

	private static bool TryGetBeamEndpoints(
		Creature source,
		Creature target,
		out Vector2 sourcePosition,
		out Vector2 targetPosition)
	{
		sourcePosition = Vector2.Zero;
		targetPosition = Vector2.Zero;

		var combatRoom = NCombatRoom.Instance;
		var sourceNode = combatRoom?.GetCreatureNode(source);
		var targetNode = combatRoom?.GetCreatureNode(target);
		if (sourceNode == null || targetNode == null)
			return false;

		sourcePosition = sourceNode.VfxSpawnPosition + BeamSourceOffset;
		targetPosition = targetNode.VfxSpawnPosition;
		return true;
	}

	private static void ApplyPurpleLaserPalette(NHyperbeamVfx beam)
	{
		Line2D? laserLine = beam.GetNodeOrNull<Line2D>(LaserLinePath);
		GradientTexture1D? purpleLut = ResourceLoader.Load<GradientTexture1D>(PurpleLaserLutPath);
		if (laserLine?.Material is not ShaderMaterial sharedMaterial || purpleLut == null)
		{
			GD.PushWarning("[DoorMonsterHyperbeamVfx] Could not apply the purple laser palette.");
			return;
		}

		var instanceMaterial = (ShaderMaterial)sharedMaterial.Duplicate(deep: true);
		instanceMaterial.SetShaderParameter(LutParameter, purpleLut);
		laserLine.Material = instanceMaterial;
	}
}
