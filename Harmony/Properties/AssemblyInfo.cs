using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("HarmonyTests")]
// MonoMod.Common uses IgnoresAccessChecksTo on its end,
// but older versions of the .NET runtime bundled with older versions of Windows
// require Harmony to expose its internals instead.
// This is only relevant for when MonoMod.Common gets merged into Harmony.
[assembly: InternalsVisibleTo("MonoMod.Utils.Cil.ILGeneratorProxy")]
[assembly: Guid("69aee16a-b6e7-4642-8081-3928b32455df")]
