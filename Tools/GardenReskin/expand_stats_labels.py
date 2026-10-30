"""Use the existing three statistics columns; retain all positions and values."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
target = root / 'Assets/Resources/prefabs/playerinfo/PlayerInfo.prefab'
evidence = root / 'Docs/GardenReskin/stats-display-area'
evidence.mkdir(parents=True, exist_ok=True)
backup = evidence / 'PlayerInfo-before.prefab'
if not backup.exists():
    backup.write_bytes(target.read_bytes())

image_ids = {'114313026266067498', '114918264173551851', '114089843903986396'}
rect_ids = {'224621510681728303', '224587397909062814', '224672306508523268'}
text_ids = {'114634108722742392', '114371939378208658', '114060945828524476'}
value_rect_ids = {'224302127217294661', '224694121525821480', '224900271049623953'}
value_text_ids = {'114642988549198181', '114017697238300914', '114518489005915545'}
text = target.read_text(encoding='utf-8')
blocks = re.split(r'(?=^--- !u!)', text, flags=re.M)
for i, block in enumerate(blocks):
    match = re.match(r'--- !u!(\d+) &(\d+)', block)
    if not match:
        continue
    key = match[2]
    if key in image_ids:
        block = re.sub(r'(?m)^  m_Color: .*', '  m_Color: {r: 1, g: 1, b: 1, a: 0}', block)
    elif key in rect_ids:
        block = re.sub(r'(?m)^  m_SizeDelta: .*', '  m_SizeDelta: {x: 282, y: 106.1}', block)
    elif key in text_ids:
        block = re.sub(r'(?m)^  m_margin: .*', '  m_margin: {x: 4, y: 4, z: 4, w: 4}', block)
    elif key in value_rect_ids:
        block = re.sub(r'(?m)^  m_SizeDelta: .*', '  m_SizeDelta: {x: 282, y: 86.55}', block)
    elif key in value_text_ids:
        block = re.sub(r'(?m)^  m_margin: .*', '  m_margin: {x: 4, y: 4, z: 4, w: 4}', block)
        block = re.sub(r'(?m)^  m_enableWordWrapping: .*', '  m_enableWordWrapping: 0', block)
    blocks[i] = block
target.write_text(''.join(blocks), encoding='utf-8', newline='')
print('Expanded 3 label and value widths with safe padding; removed only their decorative backplates.')
