using FBE.Scripts.Monsters;

namespace FBE.Scripts.Visuals;

/// <summary>一阶段教条的 Idle 形象。</summary>
public sealed partial class DogmaVisuals : DogmaEffectVisuals
{
	private const string GodheadAttackTimelinePath = "res://FBE/animations/Dogma/dogma_godhead_attack/timeline.json";
	private const string BrimstoneAttackTimelinePath = "res://FBE/animations/Dogma/dogma_brimstone_attack/timeline.json";
	private const string ScreamTimelinePath = "res://FBE/animations/Dogma/dogma_scream/timeline.json";

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
	// 参考渲染中的嘴部锚点，使用未缩放的 Dogma 动画坐标；光束须随本体的视觉缩放和贴图微调同步。
	private static readonly Godot.Vector2 BrimstoneMouthInAnimationSpace = new(10f, -28.75f);
	internal Godot.Vector2 BrimstoneBeamAnchor => (BrimstoneMouthInAnimationSpace + SpriteOffset) * VisualScale;

	/// <summary>播放 Godhead 的 Start、三次 Shoot 与 End 拼接动作，并在结束后恢复 Idle。</summary>
	public Task PlayGodheadAttackAsync()
	{
		return PlayOneShotAndReturnAsync(GodheadAttackTimelinePath);
	}

	/// <summary>播放 Scream 单次动作，并在结束后恢复 Idle。</summary>
	public Task PlayScreamAsync()
	{
		return PlayOneShotAndReturnAsync(ScreamTimelinePath);
	}

	/// <summary>本体与独立硫磺火并行播放；光束压缩完毕后才视为动作视觉结束。</summary>
	public Task PlayBrimstoneAttackAsync()
	{
		DogmaBattleAudio.PlayBrimstoneCharge();
		var bodyPlayback = PlayOneShotAndReturnAsync(BrimstoneAttackTimelinePath);
		var beam = new DogmaBrimstoneBeam();
		AddChild(beam);
		beam.Start(EffectFrame, BrimstoneBeamAnchor);
		return Task.WhenAll(bodyPlayback, beam.Playback);
	}
}
