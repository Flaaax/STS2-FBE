namespace FBE.Scripts;

#if !STS2_Stable && !STS2_Beta
#error Missing STS2 version define. Expected STS2_Stable or STS2_Beta.
#endif

#if STS2_Stable && STS2_Beta
#error Multiple STS2 version defines are set. Expected exactly one STS2 version define.
#endif

internal static class VersionGuard
{
}
