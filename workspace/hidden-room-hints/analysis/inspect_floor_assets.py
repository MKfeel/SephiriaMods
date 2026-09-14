import json
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / '.tools' / 'unitypy'))
import UnityPy

root = Path(r'E:\steam\steamapps\common\Sephiria\Sephiria_Data')
env = UnityPy.load(*(str(root / name) for name in ('resources.assets', 'sharedassets0.assets', 'sharedassets1.assets', 'sharedassets2.assets')))
rows = []
for obj in env.objects:
    if obj.type.name != 'MonoBehaviour':
        continue
    try:
        data = obj.read(check_read=False)
        script = data.m_Script.read()
        cls = script.m_ClassName
        if cls not in ('EnhancedProceduralFloorGenerator', 'LibraryFloorGenerator'):
            continue
        name = data.m_GameObject.read().m_Name
        rows.append(dict(name=name, generator=cls, path_id=obj.path_id))
    except Exception as exc:
        if 'FloorGenerator' in locals().get('cls', ''):
            rows.append(dict(error=str(exc), path_id=obj.path_id))
out = Path(__file__).with_name('floor-prefabs.json')
out.write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(rows, ensure_ascii=False, indent=2))
