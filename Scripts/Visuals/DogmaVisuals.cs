namespace FBE.Scripts.Visuals;

/// <summary>一阶段教条的 Idle 形象。</summary>
public sealed partial class DogmaVisuals : DogmaEffectVisuals
{
	private const string GodheadAttackTimelinePath = "res://FBE/animations/Dogma/dogma_godhead_attack/timeline.json";

	protected override string TimelinePath => "res://FBE/animations/Dogma/dogma_idle/timeline.json";
	protected override float VisualScale => 4f;
	// 教条本体的单独贴图微调；需要继续上下或左右调整时只改此处。
	protected override Godot.Vector2 SpriteOffset => new(0f, -12f);
	// SpriteOffset 位于缩放后的 Visuals 子树内，意图锚点需乘相同缩放才会保持同一屏幕位移。
	protected override Godot.Vector2 IntentOffset => SpriteOffset * VisualScale;
	protected override float GlitchIntervalMin => 3f;
	protected override float GlitchIntervalMax => 5f;
	protected override float GlitchDurationMin => 0.5f;
	protected override float GlitchDurationMax => 1.5f;
	protected override float ActiveGlitchStrength => 80f / 255f;

	/// <summary>播放 Godhead 的 Start、三次 Shoot 与 End 拼接动作，并在结束后恢复 Idle。</summary>
	public Task PlayGodheadAttackAsync()
	{
		return PlayOneShotAndReturnAsync(GodheadAttackTimelinePath);
	}
}
