<figure class="patch-figure">
  <figcaption>Persistent state · one async call or enumeration</figcaption>
  <div class="patch-scope">
    <span class="scope-label">One generated execution</span>
    <ol class="patch-flow">
      <li><div class="patch-step"><strong>Run</strong><small>A selected patch reads or updates its named value.</small></div></li>
      <li><div class="patch-step"><strong>Suspend</strong><small>At a yield or an await that suspends execution.</small></div></li>
      <li><div class="patch-step"><strong>Resume</strong><small>A later patch in the same type sees the saved value.</small></div></li>
    </ol>
    <div class="state-track"><code>ArgumentMode.Persistent</code> · the same named slot across suspensions</div>
  </div>
  <p class="diagram-note">Each separate call or enumerator gets its own value, starting at <code>default</code>. Ordinary outer <code>__state</code> and named locals start again on each <code>MoveNext</code> invocation. The persistent-state limits describe supported compiler protocols and cleanup.</p>
</figure>
