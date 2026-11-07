import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(os.environ['TEMP']) / 'codex-blackmesa-yaml'))
import yaml
from yaml.nodes import MappingNode, ScalarNode, SequenceNode

root = Path(__file__).resolve().parents[1]
files = {}
recipes = {}
packs = {}
bounties = []

def fields(node):
    return {key.value: value for key, value in node.value} if isinstance(node, MappingNode) else {}

def scalar(node, default=None):
    return node.value if isinstance(node, ScalarNode) else default

for path in (root / 'Resources/Prototypes').rglob('*.yml'):
    text = path.read_text(encoding='utf-8-sig')
    if not any(kind in text for kind in ('type: latheRecipe', 'type: cargoBounty')):
        continue
    document = yaml.compose(text)
    if not isinstance(document, SequenceNode):
        continue
    files[path] = text
    for node in document.value:
        data = fields(node)
        kind = scalar(data.get('type'))
        identifier = scalar(data.get('id'))
        if kind == 'latheRecipe':
            recipes[identifier] = (path, data)
        elif kind == 'latheRecipePack':
            pack = data.get('recipes')
            packs[identifier] = [item.value for item in pack.value] if isinstance(pack, SequenceNode) else []
        elif kind == 'cargoBounty' and scalar(data.get('group'), 'StationBounty') == 'StationBounty':
            bounties.append((path, data))

effective = {}
def materials(identifier):
    if identifier in effective:
        return effective[identifier]
    _, data = recipes[identifier]
    result = {}
    parents = data.get('parent')
    parent_ids = [item.value for item in parents.value] if isinstance(parents, SequenceNode) else [parents.value] if isinstance(parents, ScalarNode) else []
    for parent in reversed(parent_ids):
        result.update(materials(parent))
    result.update({key: int(value.value) for key, value in fields(data.get('materials')).items()})
    effective[identifier] = result
    return result

targets = set()
for identifier, pack in packs.items():
    if identifier.startswith('SecurityAmmo') or identifier in ('SecurityPracticeStatic', 'SyndicateAmmoLightStatic', 'SyndicateAmmoHeavyStatic'):
        targets.update(item for item in pack if not item.startswith('Weapon') and 'Empty' not in item)

edits = {}
def replace(path, node, value):
    edits.setdefault(path, []).append((node.start_mark.index, node.end_mark.index, str(value)))

ammo_count = 0
for identifier in sorted(targets):
    path, data = recipes[identifier]
    amounts = materials(identifier)
    if not amounts:
        continue
    own = fields(data.get('materials'))
    missing = {}
    for material, amount in amounts.items():
        if material in own:
            replace(path, own[material], amount * 3)
        else:
            missing[material] = amount * 3
    if missing:
        anchor = data['materials'].start_mark.index if own else data['id'].end_mark.index
        position = files[path].find('\n', anchor) + 1
        if own:
            position = files[path].rfind('\n', 0, data['materials'].start_mark.index) + 1
            addition = ''.join(f'    {key}: {value}\n' for key, value in missing.items())
        else:
            addition = '  materials:\n' + ''.join(f'    {key}: {value}\n' for key, value in missing.items())
        edits.setdefault(path, []).append((position, position, addition))
    ammo_count += 1

for path, data in bounties:
    reward = data['reward']
    replace(path, reward, (int(reward.value) * 3 + 2) // 4)

for path, changes in edits.items():
    text = files[path]
    for start, end, value in sorted(changes, reverse=True):
        text = text[:start] + value + text[end:]
    path.write_text(text, encoding='utf-8', newline='')

print(f'Adjusted {len(bounties)} station cargo bounties and {ammo_count} ammunition recipes across {len(edits)} files.')
