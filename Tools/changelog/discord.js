// SPDX-License-Identifier: AGPL-3.0-or-later
const fs = require('fs');
const { parseChanges } = require('./changelog');

function buildPayload(pr) {
    if (!pr || !pr.merged) return null;
    const parsed = parseChanges(pr.body, pr.user && pr.user.login);
    if (!parsed) return null;
    const icons = { Add: '✨', Fix: '🔧', Tweak: '⚙️', Remove: '🗑️' };
    let description = parsed.changes.map(change => `${icons[change.type]} ${change.message}`).join('\n');
    if (description.length > 3900) description = description.slice(0, 3890) + '\n…';
    const title = `🚀 Обновление: ${pr.title || 'Изменения'}`.slice(0, 256);
    const embed = { title, description, color: 0x47b85d, fields: [{ name: '👤 Автор', value: parsed.author.slice(0, 256), inline: false }] };
    if (typeof pr.html_url === 'string' && /^https:\/\/github\.com\//.test(pr.html_url)) embed.url = pr.html_url;
    if (pr.merged_at && !Number.isNaN(Date.parse(pr.merged_at))) embed.timestamp = new Date(pr.merged_at).toISOString();
    return { allowed_mentions: { parse: [] }, embeds: [embed] };
}

async function main() {
    const event = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
    const payload = buildPayload(event.pull_request);
    if (!payload) { console.log('No :cl: changes to announce.'); return; }
    const secret = process.env.DISCORD_CHANGELOG_WEBHOOK;
    if (!secret) { console.log('Discord webhook secret is not configured; skipping announcement.'); return; }
    let url;
    try { url = new URL(secret); } catch { throw new Error('Invalid Discord webhook URL'); }
    if (url.protocol !== 'https:' || url.hostname !== 'discord.com' || !/^\/api\/webhooks\/\d+\/[A-Za-z0-9._-]+\/?$/.test(url.pathname) || url.search || url.hash) {
        throw new Error('Discord webhook URL must be a standard discord.com webhook');
    }
    let response;
    try {
        response = await fetch(url, { method: 'POST', redirect: 'error', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload), signal: AbortSignal.timeout(15000) });
    } catch {
        throw new Error('Could not connect to Discord webhook');
    }
    if (!response.ok) throw new Error(`Discord webhook returned HTTP ${response.status}`);
    console.log('Discord announcement sent.');
}

if (require.main === module) main().catch(error => { console.error(error.message); process.exitCode = 1; });
module.exports = { buildPayload };
