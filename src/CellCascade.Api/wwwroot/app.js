// The grid screen: 8 columns by 20 rows. Setting a cell sends 1 request, and every cell that the
// answer lists as changed flashes, which shows how far the change cascaded.
(() => {
  const COLUMNS = 8;
  const ROWS = 20;
  const grid = document.getElementById('grid');
  const sheetBox = document.getElementById('sheet');
  const refBox = document.getElementById('ref');
  const inputBox = document.getElementById('input');
  const status = document.getElementById('status');
  const inputs = new Map();
  let picked = 'A1';

  const columnName = (i) => String.fromCharCode(65 + i);
  const cellId = (ref) => 'c-' + ref;

  function build() {
    const head = grid.createTHead().insertRow();
    head.appendChild(document.createElement('th'));
    for (let c = 0; c < COLUMNS; c++) {
      const th = document.createElement('th');
      th.textContent = columnName(c);
      head.appendChild(th);
    }
    const body = grid.createTBody();
    for (let r = 1; r <= ROWS; r++) {
      const tr = body.insertRow();
      const th = document.createElement('th');
      th.className = 'row';
      th.textContent = r;
      tr.appendChild(th);
      for (let c = 0; c < COLUMNS; c++) {
        const td = tr.insertCell();
        const ref = columnName(c) + r;
        td.id = cellId(ref);
        td.tabIndex = 0;
        td.addEventListener('click', () => pick(ref));
        td.addEventListener('keydown', (e) => { if (e.key === 'Enter') pick(ref); });
      }
    }
  }

  function pick(ref) {
    document.getElementById(cellId(picked))?.classList.remove('picked');
    picked = ref;
    document.getElementById(cellId(ref))?.classList.add('picked');
    refBox.textContent = ref;
    inputBox.value = inputs.get(ref) ?? '';
    inputBox.focus();
  }

  function show(cell, flash) {
    inputs.set(cell.ref, cell.input);
    const td = document.getElementById(cellId(cell.ref));
    if (!td) return;
    td.textContent = cell.value.display;
    td.title = cell.input;
    td.className = cell.value.type === 'number' ? 'number' : cell.value.type === 'error' ? 'error' : '';
    if (cell.ref === picked) td.classList.add('picked');
    if (flash) {
      void td.offsetWidth;
      td.classList.add('flash');
    }
  }

  function say(text, bad) {
    status.textContent = text;
    status.className = bad ? 'bad' : '';
  }

  const sheetUrl = () => '/api/sheets/' + encodeURIComponent(sheetBox.value.trim() || 'demo');

  async function load() {
    inputs.clear();
    for (const td of grid.querySelectorAll('td')) {
      td.textContent = '';
      td.title = '';
      td.className = '';
    }
    const answer = await fetch(sheetUrl());
    if (answer.status === 404) {
      say('This sheet is empty. Setting a cell creates it.');
    } else if (answer.ok) {
      const sheet = await answer.json();
      sheet.cells.forEach((cell) => show(cell, false));
      say(sheet.cells.length + ' cells loaded.');
    } else {
      say('The sheet did not load (' + answer.status + ').', true);
    }
    pick(picked);
  }

  document.getElementById('bar').addEventListener('submit', async (e) => {
    e.preventDefault();
    const answer = await fetch(sheetUrl() + '/cells/' + picked, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ input: inputBox.value }),
    });
    const body = await answer.json();
    if (!answer.ok) {
      say(body.error, true);
      return;
    }
    if (!body.changed.some((cell) => cell.ref === picked)) {
      inputs.set(picked, inputBox.value);
      const td = document.getElementById(cellId(picked));
      if (td) td.title = inputBox.value;
    }
    body.changed.forEach((cell) => show(cell, true));
    const cycle = body.inCycle.length ? ' Circular reference: ' + body.inCycle.join(', ') + '.' : '';
    say(body.recalculated.length + ' re-calculated, ' + body.changed.length + ' changed.' + cycle, body.inCycle.length > 0);
  });

  sheetBox.addEventListener('change', load);
  build();
  load();
})();
