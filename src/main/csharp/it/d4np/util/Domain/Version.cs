// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Daniel Polo
using System.Reflection;

namespace It.D4np.Util;

/// <summary>
/// Single source of truth for the library version at <b>runtime</b>. The value is resolved from this
/// assembly's <see cref="AssemblyInformationalVersionAttribute"/>, which the SDK stamps from the one
/// <c>&lt;Version&gt;</c> in <c>Directory.Build.props</c> — so there is no hand-duplicated literal (#48) and
/// the reported version always reflects the <em>loaded</em> binary rather than a value inlined into a
/// consumer at its own compile time (#86). Kept in lockstep with the README <c>Status</c> badge and the
/// latest release by <c>tools/consistency_lint.py</c>.
/// </summary>
public static class LibraryVersion
{
    /// <summary>The current SemVer version of egl-util-csharp, read from the loaded assembly at runtime.</summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        Assembly assembly = typeof(LibraryVersion).Assembly;

        // Prefer the informational version (carries the full SemVer, including any pre-release label). The
        // SDK may append build metadata (e.g. "+<git-sha>" once SourceLink is enabled), which is not part of
        // the SemVer core/pre-release, so trim anything from the first '+'.
        string? informational =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(informational))
        {
            int metadata = informational.IndexOf('+');
            return metadata >= 0 ? informational[..metadata] : informational;
        }

        // Fall back to the assembly version (always present) as a three-part string.
        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
