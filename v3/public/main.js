function addCodeControls() {
  const status = document.createElement('div');
  status.className = 'visually-hidden';
  status.setAttribute('role', 'status');
  document.body.append(status);

  for (const code of document.querySelectorAll('article pre > code')) {
    const pre = code.parentElement;
    const wrapper = document.createElement('div');
    wrapper.className = 'harmony-code';
    const toolbar = document.createElement('div');
    toolbar.className = 'code-toolbar';
    const label = document.createElement('span');
    const language = code.className.match(/(?:lang|language)-([^ ]+)/)?.[1] || 'text';
    label.textContent = { csharp: 'C#', bash: 'Shell', cmd: 'Command Prompt', text: 'Output' }[language] || language;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'copy-button';
    button.textContent = 'Copy code';
    button.setAttribute('aria-label', `Copy ${label.textContent} code`);
    let reset;
    button.addEventListener('click', async () => {
      clearTimeout(reset);
      try {
        await navigator.clipboard.writeText(code.textContent);
        button.textContent = 'Copied';
        status.textContent = 'Code copied to clipboard.';
        reset = setTimeout(() => { button.textContent = 'Copy code'; }, 1800);
      } catch {
        const range = document.createRange();
        range.selectNodeContents(code);
        const selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        button.textContent = 'Code selected';
        status.textContent = 'Use your browser’s copy command to copy the selected code.';
      }
    });
    toolbar.append(label, button);
    pre.before(wrapper);
    wrapper.append(toolbar, pre);
  }
}

function addMemberIndex() {
  const article = document.querySelector('article[data-uid]:not([data-uid=""])');
  if (!article) return;
  const headings = [...article.querySelectorAll(':scope > h3[data-uid]')];
  if (headings.length < 2) return;

  const groups = new Map();
  for (const heading of headings) {
    const name = heading.firstChild.textContent.trim().split('(')[0];
    const group = groups.get(name) || { heading, count: 0 };
    group.count++;
    groups.set(name, group);
  }

  const index = document.createElement('details');
  index.className = 'member-index';
  index.open = groups.size <= 20;
  const summary = document.createElement('summary');
  summary.textContent = `Browse ${headings.length} members`;
  const table = document.createElement('table');
  table.innerHTML = '<thead><tr><th scope="col">Member</th><th scope="col">Overloads</th></tr></thead>';
  const body = document.createElement('tbody');
  for (const [name, { heading, count }] of groups) {
    const row = body.insertRow();
    const link = document.createElement('a');
    link.href = `#${heading.id}`;
    link.textContent = name;
    row.insertCell().append(link);
    row.insertCell().textContent = count;
  }
  table.append(body);
  // DocFX wraps tables for responsive scrolling after this hook runs.
  index.append(summary, table);
  article.querySelector(':scope > h2.section')?.before(index);
}

export default {
  defaultTheme: 'light',
  start() {
    // Keep classic DocFX section links without copying the API renderers.
    for (const alias of document.querySelectorAll('[data-section-alias]')) {
      document.querySelector(`article ${alias.dataset.sectionAlias}`)?.before(alias);
    }
    addCodeControls();
    addMemberIndex();
    const picker = document.querySelector('.version-picker');
    if (!picker) return;
    document.addEventListener('click', event => {
      if (!picker.contains(event.target)) picker.open = false;
    });
    document.addEventListener('keydown', event => {
      if (event.key === 'Escape' && picker.open) {
        picker.open = false;
        picker.querySelector('summary').focus();
      }
    });
  }
};
