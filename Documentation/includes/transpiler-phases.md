<figure class="patch-figure">
  <figcaption>Two different moments · building a patch and running it</figcaption>
  <div class="patch-phase">
    <strong>1. When Harmony builds or rebuilds the replacement</strong>
    <ol class="patch-flow">
      <li><div class="patch-step"><strong>Original IL</strong><small>The method's instructions</small></div></li>
      <li><div class="patch-step patch-build"><strong>Transpiler</strong><small>Edit the instruction sequence</small></div></li>
      <li><div class="patch-step"><strong>Replacement</strong><small>Combine the edits with registered patches</small></div></li>
    </ol>
  </div>
  <div class="patch-phase">
    <strong>2. Each time the patched method is called</strong>
    <ol class="patch-flow">
      <li><div class="patch-step patch-prefix"><strong>Prefix</strong><small>Before the body</small></div></li>
      <li><div class="patch-step"><strong>Edited body</strong><small>Run the instructions produced above</small></div></li>
      <li><div class="patch-step patch-postfix"><strong>Postfix</strong><small>After completion or a skip</small></div></li>
    </ol>
  </div>
  <p class="diagram-note">The transpiler itself runs during generation. Code it inserts runs when execution reaches it. The second row shows the ordinary path; finalizers can also be installed to handle success and exceptions.</p>
</figure>
