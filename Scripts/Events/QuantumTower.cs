using System.Diagnostics;
using FBE.Scripts.Cards;
using FBE.Scripts.Enchantments;
using FBE.Scripts.Relics;
using FBE.Scripts.Utils;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;

namespace FBE.Scripts.Events;

[STS2RitsuLib.Interop.AutoRegistration.RegisterSharedEvent]
public sealed class QuantumTower : FBEEventModel
{
    private bool _gameBgmSuppressed;
    private bool _runExitCleanupRegistered;

    // 背景图位置
    public override string CustomInitialPortraitPath => NoTowerPortrait;
    private const string TowerPortrait = "res://FBE/images/events/QuantumTower1.png";
    private const string NoTowerPortrait = "res://FBE/images/events/QuantumTower2.png";
    private const string MusicPath = "res://FBE/audio/music/The Nomai.mp3";

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("Count", 5),
        new StringVar("Enchantment", ModelDb.Enchantment<Quantinized>().Title.GetFormattedText())
    ];

    public override void OnRoomEnter()
    {
        StartMusic();
    }

    protected override void OnEventFinished()
    {
        StopMusic();
    }

    public override bool IsAllowed(IRunState runState) => runState.CurrentActIndex <= 1 || true;

    private async Task DoBlinkVfx(Action onBlack)
    {
        if (!IsLocalOwner())
        {
            onBlack.Invoke();
            return;
        }

        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        NGame.Instance!.AddChildSafely(overlay);
        var tween = overlay.CreateTween();
        tween.TweenProperty(overlay, "color:a", 0.95f, 0.35);
        tween.TweenProperty(overlay, "color:a", 0.95f, 0.1);
        tween.TweenProperty(overlay, "color:a", 0f, 0.45);
        await Cmd.Wait(0.35f);
        onBlack.Invoke();
        await Cmd.Wait(0.6f);
        overlay.QueueFreeSafely();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        Option(Blink)
    ];

    private async Task Blink()
    {
        await DoBlinkVfx(() =>
        {
            SetLocalPortrait(TowerPortrait);
            SetEventState(PageDescription("BLINK"), [
                Option(EnterTower, HoverTipFactory.FromEnchantment<Quantinized>(), "BLINK"),
                Option(BlinkAgain, [], "BLINK")
            ]);
        });
    }

    private async Task EnterTower()
    {
        var prompt = L10NLookup("FBE_EVENT_QUANTUM_TOWER.selectionScreenPrompt");
        prompt.Add(DynamicVars["Count"]);
        prompt.Add(DynamicVars["Enchantment"]);
        var count = DynamicVars["Count"].IntValue;
        var perfs = new CardSelectorPrefs(prompt, 0, count);

        var enchantment = ModelDb.Enchantment<Quantinized>();

        var selectedCards = await CardSelectCmd.FromDeckGeneric(Owner!, perfs, enchantment.CanEnchant);

        foreach (var card in selectedCards)
        {
            CardCmd.Enchant<Quantinized>(card, 1m);
            await ((Quantinized)card.Enchantment!).TransformSelf();
        }

        await DoBlinkVfx(() =>
        {
            SetLocalPortrait(NoTowerPortrait);
            SetEventFinished(PageDescription("ENTER_TOWER_CHOSEN"));
        });
    }

    private async Task BlinkAgain()
    {
        await DoBlinkVfx(() =>
        {
            SetLocalPortrait(NoTowerPortrait);
            SetEventFinished(PageDescription("BLINK_AGAIN_CHOSEN"));
        });
    }

    private void StartMusic()
    {
        if (!IsLocalOwner())
        {
            return;
        }

        var audioManager = NAudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.SetBgmVol(0f);
            _gameBgmSuppressed = true;
        }

        AudioHelper.PlayLoop(MusicPath);
        RegisterRunExitCleanup();
    }

    private void StopMusic()
    {
        if (!IsLocalOwner() && !_runExitCleanupRegistered)
        {
            return;
        }

        UnregisterRunExitCleanup();
        AudioHelper.StopLoop(MusicPath);

        if (!_gameBgmSuppressed)
        {
            return;
        }

        NAudioManager.Instance?.SetBgmVol(SaveManager.Instance.SettingsSave.VolumeBgm);
        _gameBgmSuppressed = false;
    }

    private void RegisterRunExitCleanup()
    {
        if (_runExitCleanupRegistered)
        {
            return;
        }

        var run = NRun.Instance;
        if (run == null)
        {
            return;
        }

        run.TreeExiting += StopMusic;
        _runExitCleanupRegistered = true;
    }

    private void UnregisterRunExitCleanup()
    {
        if (!_runExitCleanupRegistered)
        {
            return;
        }

        var run = NRun.Instance;
        if (run != null && GodotObject.IsInstanceValid(run))
        {
            run.TreeExiting -= StopMusic;
        }

        _runExitCleanupRegistered = false;
    }
}
