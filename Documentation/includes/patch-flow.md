<figure class="patch-figure">
  <figcaption>On each call · the ordinary execution path</figcaption>
  <ol class="patch-flow">
    <li><div class="patch-step patch-prefix"><strong>Prefix</strong><small>Read arguments, change them, or skip the original.</small></div></li>
    <li><div class="patch-step"><strong>Original</strong><small>Run the method body, including any instruction edits.</small></div></li>
    <li><div class="patch-step patch-postfix"><strong>Postfix</strong><small>Read or change the result after completion or a skip.</small></div></li>
  </ol>
  <div class="patch-recovery"><div class="patch-step patch-finalizer"><strong>Finalizer · success or exception</strong><small>When installed, handles the outcome of the sequence above.</small></div></div>
  <p class="diagram-note">A prefix can skip the original; postfixes still run. An exception interrupts the sequence and skips remaining postfixes. A finalizer can observe, change, or suppress that exception.</p>
</figure>
