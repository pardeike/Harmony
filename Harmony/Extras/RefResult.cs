namespace HarmonyLib;

/// <summary>Delegate type for "ref return" injections</summary>
/// <typeparam name="T">Return type of the original method, without ref modifier</typeparam>
public delegate ref T RefResult<T>();
