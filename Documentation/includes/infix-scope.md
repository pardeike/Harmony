<figure class="patch-figure">
  <figcaption>Infix · a patch around a selected operation</figcaption>
  <div class="patch-scope">
    <span class="scope-label">Outer method · Outer.Run()</span>
    <div class="patch-context">Earlier instructions</div>
    <div class="patch-selected">
      <span class="scope-label">Selected operation · Helper.Decide(value)</span>
      <ol class="patch-flow">
        <li><div class="patch-step patch-prefix"><strong>Inner prefix</strong><small>Before this operation</small></div></li>
        <li><div class="patch-step"><strong>Operation</strong><small>Call, read, write, create, or load</small></div></li>
        <li><div class="patch-step patch-postfix"><strong>Inner postfix</strong><small>After this operation</small></div></li>
      </ol>
      <div class="patch-recovery"><div class="patch-step patch-finalizer"><strong>Inner finalizer</strong><small>Handles this selected operation's patch sequence.</small></div></div>
    </div>
    <div class="patch-context">Later instructions</div>
  </div>
  <p class="diagram-note">The orange border marks the selection. Calls from other methods are unaffected by this Infix. If an exception escapes the selection, normal exception handling in the outer method decides what happens next.</p>
</figure>
