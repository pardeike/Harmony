## Troubleshooting

### Dll

- make sure you're actually running the built DLL you expect you are (post-build copy problems)

### Log

- make sure your patch is being run by logging something out, from a Postfix or Finalizer

### Problems finding methods and other members

- specify signature arguments
- enumerate and select with reflection (use AccessTools https://harmony.pardeike.net/api/HarmonyLib.AccessTools.html)
- nested class (wrong hierarchical level)

### Something isn't changing

- do you need `ref` on your argument?
