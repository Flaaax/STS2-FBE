using FBE.Scripts.Cards;
using FBE.Scripts.Visuals;
using FBE.Scripts.VFX;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine.Backends;

namespace FBE.Scripts.Monsters;

/// <summary>
/// 门：用于验证精英遭遇、意图和非 Spine 形象的完整接入链路。
/// </summary>
[RegisterMonster]
public class DoorMonster : ModMonsterTemplate
{
	private const int EyesOpenDamage = 8;
	private const int EyesOpenRepeat = 2;
	private const int WhisperDebuffAmount = 2;
	private static readonly LocString OpenDoorBanter =
		new("monsters", "FBE_MONSTER_DOOR_MONSTER.moves.OPEN_DOOR.banter");
	private static readonly LocString WhisperBanter =
		new("monsters", "FBE_MONSTER_DOOR_MONSTER.moves.WHISPER.banter");

	public override int MinInitialHp =>
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 240, 210);

	public override int MaxInitialHp =>
		AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 200, 190);

	private int OpenDoorDamage =>
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 21, 17);

	private int EmpowerStrength =>
		AscensionHelper.GetValueIfAscension(AscensionLevel.DeadlyEnemies, 5, 4);

	// 占位敌人没有专属 FMOD 死亡音效，避免默认路径指向不存在的事件。
	public override bool HasDeathSfx => false;

	protected override NCreatureVisuals TryCreateCreatureVisuals()
	{
		return new DoorMonsterVisuals();
	}

	protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(Node visualsRoot, MonsterModel monster)
	{
		if (visualsRoot is not DoorMonsterVisuals visuals)
			return null;

		// 待机序列帧同样接住游戏的战斗触发器；后续可为攻击等状态换用专属动画。
		return ModAnimStateMachineBuilder.Create()
			.AddState("idle", loop: true).AsInitial().Done()
			.AddAnyState("Idle", "idle")
			.AddAnyState("Hit", "idle")
			.AddAnyState("Dead", "idle")
			.AddAnyState("Attack", "idle")
			.AddAnyState("Cast", "idle")
			.Build(new AnimatedSprite2DBackend(visuals.Sprite));
	}

	protected override MonsterMoveStateMachine GenerateMoveStateMachine()
	{
		var openDoor = new MoveState(
			"OPEN_DOOR",
			OpenDoorMove,
			new SingleAttackIntent(OpenDoorDamage),
			new StatusIntent(3));
		var empower = new MoveState("EMPOWER", EmpowerMove, new BuffIntent());
		var whisper = new MoveState("WHISPER", WhisperMove, new DebuffIntent());
		var eyesOpen = new MoveState(
			"EYES_OPEN",
			EyesOpenMove,
			new MultiAttackIntent(EyesOpenDamage, EyesOpenRepeat));

		openDoor.FollowUpState = whisper;
		whisper.FollowUpState = empower;
		empower.FollowUpState = eyesOpen;
		eyesOpen.FollowUpState = openDoor;
		return new MonsterMoveStateMachine([openDoor, empower, whisper, eyesOpen], openDoor);
	}

	private async Task OpenDoorMove(IReadOnlyList<Creature> targets)
	{
		TalkCmd.Play(OpenDoorBanter, Creature, VfxColor.Purple, VfxDuration.Standard);
		await DamageCmd.Attack(OpenDoorDamage).FromMonster(this)
			.BeforeDamage(() => PlayHyperbeam(targets))
			.Execute(null);
		await AddOpenDoorStatusCards(targets);
	}

	private async Task EmpowerMove(IReadOnlyList<Creature> targets)
	{
		TalkCmd.Play(WhisperBanter, Creature, VfxColor.Purple, VfxDuration.Standard);
		await PowerCmd.Apply<StrengthPower>(
			new ThrowingPlayerChoiceContext(),
			Creature,
			EmpowerStrength,
			Creature,
			null);
	}

	private async Task WhisperMove(IReadOnlyList<Creature> targets)
	{
		await PowerCmd.Apply<WeakPower>(
			new ThrowingPlayerChoiceContext(),
			targets,
			WhisperDebuffAmount,
			Creature,
			null);
		await PowerCmd.Apply<VulnerablePower>(
			new ThrowingPlayerChoiceContext(),
			targets,
			WhisperDebuffAmount,
			Creature,
			null);
	}

	private async Task EyesOpenMove(IReadOnlyList<Creature> targets)
	{
		await DamageCmd.Attack(EyesOpenDamage).WithHitCount(EyesOpenRepeat).FromMonster(this)
			.OnlyPlayAnimOnce()
			.Execute(null);
	}

	private async Task PlayHyperbeam(IReadOnlyList<Creature> targets)
	{
		if (targets.Count == 0)
			return;

		var beam = DoorMonsterHyperbeamVfx.CreateBeam(Creature, targets[0]);
		if (beam != null)
		{
			NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(beam);
			_ = TaskHelper.RunSafely(RestartIdleAfterHyperbeam());
			await Cmd.Wait(NHyperbeamVfx.hyperbeamAnticipationDuration);
		}

		foreach (var target in targets)
		{
			var impact = DoorMonsterHyperbeamVfx.CreateImpact(Creature, target);
			if (impact != null)
				NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(impact);
		}
	}

	private static async Task AddOpenDoorStatusCards(IReadOnlyList<Creature> targets)
	{
		foreach (var target in targets)
		{
			var player = target.Player ?? target.PetOwner;
			var combatState = target.CombatState;
			if (player == null || combatState == null)
				continue;

			var hunger = combatState.CreateCard<Hunger>(player);
			var scrutiny = combatState.CreateCard<Scrutiny>(player);
			var grasp = combatState.CreateCard<Grasp>(player);

			CardCmd.PreviewCardPileAdd(
				await CardPileCmd.AddGeneratedCardToCombat(hunger, PileType.Hand, null));
			CardCmd.PreviewCardPileAdd(
				await CardPileCmd.AddGeneratedCardToCombat(scrutiny, PileType.Draw, null));
			CardCmd.PreviewCardPileAdd(
				await CardPileCmd.AddGeneratedCardToCombat(grasp, PileType.Discard, null));
		}
	}

	private async Task RestartIdleAfterHyperbeam()
	{
		await Cmd.Wait(DoorMonsterHyperbeamVfx.Duration);
		if (NCombatRoom.Instance?.GetCreatureNode(Creature)?.Visuals is DoorMonsterVisuals visuals)
			visuals.RestartIdleAnimation();
	}
}
