using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1813")]
[assembly: SuppressMessage("Performance", "CA1810")]
[assembly: SuppressMessage("Performance", "CA1822")]
[assembly: SuppressMessage("Performance", "CA1825")]

[assembly: SuppressMessage("Design", "CA1031")]
[assembly: SuppressMessage("Design", "CA1018")]

[assembly: SuppressMessage("CodeQuality", "IDE0051")]
[assembly: SuppressMessage("CodeQuality", "IDE0057")]
[assembly: SuppressMessage("CodeQuality", "IDE0079")]

[assembly: SuppressMessage("", "IDE0251")]
[assembly: SuppressMessage("", "IDE0130")]
[assembly: SuppressMessage("Style", "IDE0270")]
[assembly: SuppressMessage("Usage", "CA2211")]
[assembly: SuppressMessage("GeneratedRegex", "SYSLIB1045")]

[assembly: SuppressMessage("Reliability", "CA2020:Prevent from behavioral change", Justification = "<Pending>", Scope = "member", Target = "~M:HarmonyLib.FileLog.LogBytes(System.Int64,System.Int32)")]
[assembly: SuppressMessage("Performance", "CA1850:Prefer static 'HashData' method over 'ComputeHash'", Justification = "<Pending>", Scope = "member", Target = "~M:HarmonyLib.FileLog.LogBytes(System.Int64,System.Int32)")]

#if NET8_0_OR_GREATER
[assembly: SuppressMessage("Maintainability", "CA1510")]
#endif
