// SPDX-License-Identifier: AGPL-3.0-or-later
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { buildPayload } = require('./discord');
const pr = { merged: true, body: ':cl:\n- add: Добавлен лифт\n- fix: Починена дверь', user: { login: 'tester' }, title: 'Подземный комплекс', html_url: 'https://github.com/NithaRe/TimurRP-blackmesa/pull/1', merged_at: '2026-11-07T09:00:00Z' };
test('creates announcement with changelog and no mentions', () => {
    const p = buildPayload(pr);
    assert.deepEqual(p.allowed_mentions, { parse: [] });
    assert.match(p.embeds[0].title, /Подземный комплекс/);
    assert.match(p.embeds[0].description, /✨ Добавлен лифт/);
    assert.match(p.embeds[0].description, /🔧 Починена дверь/);
    assert.equal(p.embeds[0].url, pr.html_url);
    assert.equal(p.embeds[0].fields[0].value, 'tester');
});
test('skips missing or commented changelog and unmerged PRs', () => {
    assert.equal(buildPayload({ ...pr, body: '<!-- :cl:\n- add: test -->' }), null);
    assert.equal(buildPayload({ ...pr, merged: false }), null);
});
test('handles long text within Discord embed limits', () => {
    const p = buildPayload({ ...pr, title: 'x'.repeat(500), body: ':cl:\n- add: ' + 'a'.repeat(9000) });
    assert.ok(p.embeds[0].title.length <= 256);
    assert.ok(p.embeds[0].description.length <= 4096);
});
