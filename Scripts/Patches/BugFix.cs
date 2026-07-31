using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

// ReSharper disable InconsistentNaming

namespace FBE.Scripts.Patches;

/// <summary>
/// BUG时机：在AbstractModel.AfterPlayerTurnStart钩子触发，调用PlayerCmd.EndTurn时
/// </summary>

// 用来保存“延后执行 EndTurn”的临时状态。
internal static class DeferredEndTurnState
{
    internal sealed class PendingEndTurn
    {
        public Player Player = null!;
        public bool CanBackOut;
        public Func<Task>? ActionDuringEnemyTurn;
        public int Round;
    }

    internal static readonly Dictionary<ulong, PendingEndTurn> Pending = new();

    internal static void ClearAll()
    {
        Pending.Clear();
        Patch_PlayerCmd_EndTurn.RemoveTurnStartedHandler();
    }
}

/// <summary>
/// Harmony patch：拦截 PlayerCmd.EndTurn
/// </summary>
[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.EndTurn))]
static class Patch_PlayerCmd_EndTurn
{
    /// <summary>
    /// 每次都先退订再重订阅，保证当前 handler 处于 TurnStarted 调用链的最后。
    /// </summary>
    private static void MoveTurnStartedHandlerToLast()
    {
        var cm = CombatManager.Instance;
        cm.TurnStarted -= DeferredEndTurn_OnTurnStarted;
        cm.TurnStarted += DeferredEndTurn_OnTurnStarted;
    }

    internal static void RemoveTurnStartedHandler()
    {
        CombatManager.Instance.TurnStarted -= DeferredEndTurn_OnTurnStarted;
    }

    /// <summary>
    /// Prefix 返回 true 代表继续执行原方法；
    /// 返回 false 代表跳过原方法。
    ///
    /// 这里的策略是：
    /// - 如果 EndTurn 调用时机正常，就不干预
    /// - 如果 EndTurn 调用得“过早”（还没进入 PlayPhase），就先缓存，阻止原方法立刻执行
    /// </summary>
    static bool Prefix(Player player, bool canBackOut, Func<Task>? actionDuringEnemyTurn)
    {
        var cm = CombatManager.Instance;

        // 没有 CombatManager，或者战斗根本不在进行中，就不拦。
        if (!cm.IsInProgress)
            return true;

        // 从 player 取当前战斗状态。
        var state = player.Creature.CombatState;
        if (state == null)
            return true;

        // 判断这次 EndTurn 是否发生得“太早”。
        //
        // 条件解释：
        // 1. 当前必须是玩家回合
        // 2. CombatManager 还没进入 PlayPhase
        // 3. ActionQueueSynchronizer 也还处在 NotPlayPhase
        //    —— 这说明当前仍然属于“玩家回合启动中”的早期时段
        // 4. 当前不在“已经开始结束回合第一阶段”的流程里
        //    —— 避免误拦截正常的结束回合过程
        // 5. 当前玩家还没被标记为 ready-to-end-turn
        //    —— 避免重复缓存
        bool tooEarlyPlayerTurn =
            state.CurrentSide == CombatSide.Player &&
            player.PlayerCombatState!.Phase != PlayerTurnPhase.Play &&
            RunManager.Instance.ActionQueueSynchronizer.CombatState == ActionSynchronizerCombatState.NotPlayPhase &&
            !cm.EndingPlayerTurnPhaseOne &&
            !cm.IsPlayerReadyToEndTurn(player);

        // 如果不是“过早 EndTurn”，说明是正常调用，放行原逻辑。
        if (!tooEarlyPlayerTurn)
            return true;

        // 走到这里，说明这次 EndTurn 的时机太早：
        // 我们先把请求记下来，等稍后真正进入 TurnStarted / PlayPhase 时再补执行。
        DeferredEndTurnState.Pending[player.NetId] = new DeferredEndTurnState.PendingEndTurn
        {
            Player = player,
            CanBackOut = canBackOut,
            ActionDuringEnemyTurn = actionDuringEnemyTurn,
            Round = state.RoundNumber
        };

        // 每次都强制重订阅，确保我们的 handler 位于当前 invocation list 最后。
        MoveTurnStartedHandlerToLast();

        // 返回 false：阻止原始 PlayerCmd.EndTurn 现在就执行。
        return false;
    }

    /// <summary>
    /// 当 CombatManager 发出 TurnStarted 事件时调用。
    ///
    /// 这时已经进入了更安全的时机：
    /// - 玩家回合已经真正开始
    /// - PlayPhase 相关状态已经初始化完成
    ///
    /// 所以这里可以把之前缓存的 EndTurn 请求重新执行。
    /// </summary>
    private static void DeferredEndTurn_OnTurnStarted(CombatState state)
    {
        // 只在玩家回合开始时处理。
        if (state.CurrentSide != CombatSide.Player)
            return;

        // 先收集本次需要执行 / 清理的项，避免一边遍历字典一边修改。
        var toRun = new List<DeferredEndTurnState.PendingEndTurn>();
        var toRemove = new List<ulong>();

        foreach (var (key, pending) in DeferredEndTurnState.Pending)
        {
            var pendingState = pending.Player.Creature.CombatState;

            // 属于当前战斗状态、且回合号一致：本次补执行。
            if (pendingState == state && pending.Round == state.RoundNumber)
            {
                toRun.Add(pending);
                toRemove.Add(key);
                continue;
            }

            // 不属于当前状态或当前回合，说明已经 stale，直接清掉。
            if (pendingState != state || pending.Round != state.RoundNumber)
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            DeferredEndTurnState.Pending.Remove(key);
        }

        // 真正补执行 EndTurn。
        foreach (var pending in toRun)
        {
            // 这里再次调用 PlayerCmd.EndTurn。
            // 因为现在已经进入 TurnStarted，通常不会再被上面的 Prefix 当成“太早”拦掉。
            PlayerCmd.EndTurn(pending.Player, pending.CanBackOut, pending.ActionDuringEnemyTurn);
        }

        // 当前已经没有待处理项时，立刻退订，避免跨战斗残留。
        if (DeferredEndTurnState.Pending.Count == 0)
        {
            RemoveTurnStartedHandler();
        }
    }
}

// 战斗结束后清除缓存
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndCombatInternal), new Type[] { })]
static class Patch_CombatManager_EndCombatInternal
{
    public static void Postfix()
    {
        DeferredEndTurnState.ClearAll();
    }
}

[HarmonyPatch(typeof(CombatManager), "ProcessPendingLoss")]
static class Patch_CombatManager_ProcessPendingLoss
{
    public static void Postfix()
    {
        DeferredEndTurnState.ClearAll();
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
static class Patch_CombatManager_Reset
{
    public static void Postfix()
    {
        DeferredEndTurnState.ClearAll();
    }
}
