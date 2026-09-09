namespace FBE.Scripts.Visuals;

/// <summary>教条电视的 Idle 形象；不包含原作的大光柱图层。</summary>
public sealed partial class DogmaTvVisuals : DogmaEffectVisuals
{
	protected override string TimelinePath => "res://FBE/animations/Dogma/dogma_tv_idle/timeline.json";
	protected override float VisualScale => 4f;
	protected override float GlitchIntervalMin => 1.5f;
	protected override float GlitchIntervalMax => 3f;
	protected override float GlitchDurationMin => 0.5f;
	protected override float GlitchDurationMax => 1.1f;
	protected override float ActiveGlitchStrength => 160f / 255f;

	/// <summary>将死亡动画转交给独立节点，使电视实体移除后最后一帧仍留在房间内。</summary>
	public void SpawnDeathRemnant()
	{
		var creatureNode = GetParent();
		var container = creatureNode?.GetParent();
		if (creatureNode == null || container == null)
			return;

		var remnant = new DogmaTvDeathRemnant { GlobalPosition = GlobalPosition };
		container.AddChild(remnant);
		container.MoveChild(remnant, creatureNode.GetIndex());
		Visible = false;
	}
}
