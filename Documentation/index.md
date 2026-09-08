---
title: Harmony 3 documentation
_harmonyHome: true
_disableToc: false
_disableAffix: true
_disableNextArticle: true
_disableContribution: true
---

<section class="intro-panel" aria-labelledby="harmony-3-preview">
  <div>
    <div class="eyebrow">A .NET library for runtime patching</div>
    <h1 id="harmony-3-preview">Change the code.<br><span>Keep the original.</span></h1>
    <p class="intro-description">Run your code before, after, or inside existing methods. Combine patches from multiple authors without changing files on disk.</p>
    <div class="intro-actions"><a class="harmony-button" href="articles/basics.md">Get started <span aria-hidden="true">↗</span></a><a href="api/index.md">Explore the API <span aria-hidden="true">→</span></a></div>
  </div>
  <div class="starter-panel">
    <div class="starter-heading"><span class="icon-tile patch-postfix" aria-hidden="true">↳</span><span>A small patch. A different result.</span></div>
    <pre><code class="lang-csharp">[HarmonyPatch(typeof(Player), "GetSpeed")]
static class SpeedPatch
{
    static void Postfix(ref float __result)
    {
        __result *= 1.5f;
    }
}</code></pre>
    <a href="articles/patching-postfix.md">See how postfixes work <span aria-hidden="true">→</span></a>
  </div>
</section>

<p class="version-notice"><span class="preview-dot" aria-hidden="true"></span>You're reading the <strong>Harmony 3 preview</strong>. For the current stable version, use the <a href="https://harmony.pardeike.net/v2/">2.x documentation</a>.</p>

<h2 id="documentation">Find your way in</h2>

<div class="entry-grid">
  <section class="entry-card">
    <span class="icon-tile patch-prefix" aria-hidden="true">↗</span>
    <h3>Your first patch</h3>
    <p>Install Harmony, pick a method, and apply a patch. Start with a complete working example.</p>
    <a href="articles/intro.md#hello-world-example">Follow the example <span aria-hidden="true">→</span></a>
  </section>
  <section class="entry-card">
    <span class="icon-tile patch-finalizer" aria-hidden="true">⊙</span>
    <h3>Meet Infix</h3>
    <p>Target an operation inside a method using familiar patch types. New in v3.</p>
    <a href="articles/patching-infix.md">Explore Infix <span aria-hidden="true">→</span></a>
  </section>
  <section class="entry-card">
    <span class="icon-tile patch-postfix" aria-hidden="true">{ }</span>
    <h3>API reference</h3>
    <p>Find types, signatures, parameters, and overloads generated directly from Harmony.</p>
    <a href="api/index.md">Browse the API <span aria-hidden="true">→</span></a>
  </section>
</div>

## How a patch fits together

[!include[Patch execution](includes/patch-flow.md)]

<section class="home-section" id="introduction">
  <h2>Built for patches that coexist</h2>
  <p>Harmony combines patches on the same method and lets their authors coordinate execution order. It supports .NET and Mono environments, including many Unity games. Runtime capabilities still matter: see the <a href="articles/intro.md#limits-of-runtime-patching">limits of runtime patching</a>.</p>
  <p><a href="articles/patching.md">Choose a patch type</a> · <a href="articles/priorities.md">Understand patch ordering</a></p>
</section>

<section class="home-section" id="getting-started">
  <h2>Get Harmony into your project</h2>
  <p>Use <a href="https://www.nuget.org/packages/Lib.Harmony">Lib.Harmony</a> for one DLL with its dependencies merged in. Use <a href="https://www.nuget.org/packages/Lib.Harmony.Thin">Lib.Harmony.Thin</a> when you want to manage those dependencies yourself.</p>
  <p>This site describes v3 preview development on the <a href="https://github.com/pardeike/Harmony/tree/v3">v3 branch</a>. Check the version your application loads before using new APIs. <a href="articles/basics.md">The installation guide</a> covers the steps.</p>
</section>

<section class="home-section" id="community">
  <h2>Made for the modding community</h2>
  <p>Andreas Pardeike created Harmony for RimWorld and its modding community. Today it is used in many games and applications.</p>
  <p>Discuss patches on <a href="https://discord.gg/xXgghXR">Discord</a>, report a problem on <a href="https://github.com/pardeike/Harmony/issues">GitHub</a>, or contribute an improvement to the guides.</p>
  <p id="contact">Contact Andreas at <a href="mailto:andreas@pardeike.net">andreas@pardeike.net</a>. <span id="donations">Support Harmony on <a href="https://www.patreon.com/pardeike">Patreon</a>.</span></p>
</section>
