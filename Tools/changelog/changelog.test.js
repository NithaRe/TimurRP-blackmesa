// SPDX-License-Identifier: AGPL-3.0-or-later
const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const yaml = require('js-yaml');
const { parseChanges, updateChangelog } = require('./changelog');

const sample = 'Name: BlackM\nOrder: -1\nEntries:\n- author: first\n  changes:\n  - type: Add\n    message: old\n  id: 1\n  time: \'2026-01-01T00:00:00.0000000+00:00\'\n';
const pr = { number: 123, merged: true, merged_at: '2026-11-07T06:00:00Z', user: { login: 'tester' }, body: ':cl:\n- add: Добавлен лифт\n- fix: Исправлена дверь\n' };
test('writes all :cl: entries and preserves existing text and metadata', () => {
    const out = updateChangelog(sample, pr);
    assert.ok(out.startsWith(sample));
    const data = yaml.load(out, { schema: yaml.JSON_SCHEMA });
    assert.equal(data.Name, 'BlackM'); assert.equal(data.Order, -1);
    assert.equal(data.Entries.length, 2); assert.equal(data.Entries[1].id, 2);
    assert.equal(data.Entries[1].author, 'tester');
    assert.deepEqual(data.Entries[1].changes, [{ type: 'Add', message: 'Добавлен лифт' }, { type: 'Fix', message: 'Исправлена дверь' }]);
    assert.equal(updateChangelog(out, pr), null);
});
test('skips commented-out, missing and empty changelog blocks', () => {
    assert.equal(parseChanges('<!--\n:cl:\n- add: example\n-->', 'test'), null);
    assert.equal(parseChanges('no changelog', 'test'), null);
    assert.equal(parseChanges(':cl:\n- unknown: test', 'test'), null);
});
test('supports explicit author, CRLF and YAML special characters', () => {
    const out = updateChangelog(sample, { ...pr, body: ':cl: other-user\r\n- tweak: Исправлено: \"да\" #1\r\n' });
    const entry = yaml.load(out, { schema: yaml.JSON_SCHEMA }).Entries[1];
    assert.equal(entry.author, 'other-user'); assert.equal(entry.changes[0].message, 'Исправлено: "да" #1');
});
test('rejects invalid base and unmerged PR', () => {
    assert.throws(() => updateChangelog(sample.replace('BlackM', 'Gooblog'), pr));
    assert.throws(() => updateChangelog(sample, { ...pr, merged: false }));
});
test('appends correctly to the actual BlackM changelog', () => {
    const original = fs.readFileSync('Resources/Changelog/bmchangelog.yml', 'utf8');
    const result = updateChangelog(original, pr);
    const old = yaml.load(original, { schema: yaml.JSON_SCHEMA });
    const updated = yaml.load(result, { schema: yaml.JSON_SCHEMA });
    assert.ok(result.startsWith(original)); assert.equal(updated.Entries.length, old.Entries.length + 1);
    assert.equal(updated.Entries.at(-1).id, Math.max(...old.Entries.map(x => x.id)) + 1);
});
