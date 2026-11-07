// SPDX-FileCopyrightText: 2024 Piras314 <92357316+Piras314@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-License-Identifier: AGPL-3.0-or-later

// Reads the merged PR event. Never checks out or executes the PR branch.
const fs = require('fs');
const yaml = require('js-yaml');

function parseChanges(body, fallbackAuthor) {
    const clean = (body || '').replace(/<!--[^]*?-->/g, '');
    const header = /^[ \t]*(?::cl:|🆑)(?:[ \t]+([a-z0-9_, -]+))?[ \t]*$/im.exec(clean);
    if (!header) return null;
    const author = (header[1] || fallbackAuthor || '').trim();
    if (!author) throw new Error('Changelog author is missing');
    const types = { add: 'Add', remove: 'Remove', tweak: 'Tweak', fix: 'Fix' };
    const changes = [];
    const lines = clean.slice(header.index + header[0].length).split(/\r?\n/);
    for (const line of lines) {
        if (/^[ \t]*#{1,6}[ \t]/.test(line) || /^[ \t]*(?::cl:|🆑)/i.test(line)) break;
        const match = /^[ \t]*(?:[-*][ \t]*)?(add|remove|tweak|fix):[ \t]*(\S.*?)[ \t]*$/i.exec(line);
        if (match) changes.push({ type: types[match[1].toLowerCase()], message: match[2] });
    }
    return changes.length ? { author, changes } : null;
}

function updateChangelog(text, pr) {
    if (!pr || !pr.merged || !pr.merged_at || !Number.isSafeInteger(pr.number) || pr.number < 1) {
        throw new Error('A merged PR with a valid number and merge time is required');
    }
    const parsed = parseChanges(pr.body, pr.user && pr.user.login);
    if (!parsed) return null;
    const marker = `# PR #${pr.number}`;
    if (text.split(/\r?\n/).includes(marker)) return null;
    const data = yaml.load(text, { schema: yaml.JSON_SCHEMA });
    if (!data || data.Name !== 'BlackM' || !Array.isArray(data.Entries)) {
        throw new Error('Expected the BlackM changelog with an Entries list');
    }
    const lines = text.split(/\r?\n/);
    const entryHeader = lines.findIndex(line => /^Entries:\s*$/.test(line));
    if (entryHeader < 0 || lines.slice(entryHeader + 1).some(line => /^[A-Za-z_][\w-]*:/.test(line))) {
        throw new Error('Entries must be the last top-level property');
    }
    const ids = data.Entries.map(entry => entry.id);
    if (ids.some(id => !Number.isSafeInteger(id) || id < 1)) throw new Error('Invalid existing changelog id');
    const date = new Date(pr.merged_at);
    if (Number.isNaN(date.getTime())) throw new Error('Invalid PR merge time');
    const entry = {
        author: parsed.author,
        changes: parsed.changes,
        id: Math.max(0, ...ids) + 1,
        time: date.toISOString().slice(0, 19) + '.0000000+00:00',
    };
    const result = text.replace(/\s*$/, '\n') + marker + '\n' + yaml.dump([entry], { lineWidth: -1, noRefs: true });
    const verified = yaml.load(result, { schema: yaml.JSON_SCHEMA });
    if (verified.Name !== data.Name || verified.Order !== data.Order || verified.Entries.length !== data.Entries.length + 1) {
        throw new Error('Changelog validation failed');
    }
    return result;
}

function main() {
    const event = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
    const file = 'Resources/Changelog/bmchangelog.yml';
    const original = fs.readFileSync(file, 'utf8');
    const updated = updateChangelog(original, event.pull_request);
    if (updated === null) {
        console.log('No valid :cl: entries, or this PR was already processed.');
        return;
    }
    fs.writeFileSync(file, updated);
    console.log(`Added changelog for PR #${event.pull_request.number}.`);
}

if (require.main === module) main();
module.exports = { parseChanges, updateChangelog };
