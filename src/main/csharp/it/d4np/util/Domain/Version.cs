// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Daniel Polo
namespace It.D4np.Util;

/// <summary>
/// Single source of truth for the library version. Kept in lockstep with the README
/// <c>Status</c> badge and the latest release by <c>tools/consistency_lint.py</c>
/// (the version-lockstep congruence check). Bump this in the same PR as the badge.
/// </summary>
public static class LibraryVersion
{
    /// <summary>The current SemVer version of egl-util-csharp.</summary>
    public const string Version = "0.0.0";
}
