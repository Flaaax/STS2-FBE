namespace FBE.Scripts;

#if !STS2_0_107_1 && !STS2_0_110_0
#error Missing STS2 version define. Expected STS2_0_107_1 or STS2_0_110_0.
#endif

#if STS2_0_107_1 && STS2_0_110_0
#error Multiple STS2 version defines are set. Expected exactly one STS2 version define.
#endif

internal static class VersionGuard
{
}
