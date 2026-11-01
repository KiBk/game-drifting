#!/usr/bin/env python3
"""Build a subpath-safe Pages artifact without changing source/private inputs."""
import gzip
import json
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]
CONFIG = json.loads((ROOT/'pages.json').read_text())
OUT = ROOT/'_site'
assert OUT.parent == ROOT and OUT.name == '_site' and not OUT.is_symlink()
if OUT.exists():
    shutil.rmtree(OUT)
OUT.mkdir()
TEXT = {'.html','.css','.js','.mjs','.json','.webmanifest','.xml','.svg','.txt','.md'}
HOSTS = {name:'https://kibk.net/'+name+'/' for name in ['car-trip','poland-trip','prague','gjovik','levanger-loop','oppdal-hike','oppdal-round','vev-vind','wheels','suspension-lab','police-chase','gallery','car']}
HOSTS.update(drifting='https://kibk.net/game-drifting/',sicily='https://kibk.net/sicily/planner/')

for part in CONFIG['parts']:
    source = ROOT/part['source']
    dest = OUT/part.get('destination','')
    base = part['base']
    assert base.startswith('/') and base.endswith('/')
    paths = [source/p for p in part['files']] if 'files' in part else sorted(p for p in source.rglob('*') if p.is_file())
    root_assets = {p.name for p in source.iterdir()}
    for p in paths:
        assert p.is_file() and not p.is_symlink()
        rel = p.relative_to(source)
        if rel.name in ('.nojekyll','.DS_Store'):
            continue
        assert not any(x.startswith('.') for x in rel.parts), 'Hidden input is not publishable'
        target = dest/rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if part.get('gunzip') and p.suffix == '.gz':
            target = target.with_suffix('')
            target.write_bytes(gzip.decompress(p.read_bytes()))
        else:
            shutil.copyfile(p,target)
        if target.suffix not in TEXT:
            continue
        if 'vendor' in rel.parts or target.name in ('leaflet.js','WebGL.loader.js','WebGL.framework.js'):
            continue
        text = target.read_text()
        for host,url in HOSTS.items():
            text = re.sub(r'https://'+re.escape(host)+r'\.apps\.kibk\.net/?',url,text)
        # Rebase quoted root-relative URLs; never touch // external URLs or
        # already-correct /repo/... URLs. Relative imports/assets stay relative.
        def rebase(m):
            if ('/'+m[2]).startswith(base): return m[0]
            first = re.split(r'[/#?]',m[2],maxsplit=1)[0]
            if target.suffix in ('.js','.mjs') and first not in root_assets: return m[0]
            return m[1]+base+m[2]
        text = re.sub(r'(["\x27`])/(?!/)([^"\x27`\s<>]*)',rebase,text)
        text = re.sub(r'url\(/(?!/)([^)]+)\)',lambda m:'url('+base+m[1]+')',text)
        if part.get('gunzip') and target.name == 'index.html':
            text = text.replace('.data.gz','.data').replace('.framework.js.gz','.framework.js').replace('.wasm.gz','.wasm')
        target.write_text(text)

# The original exports used only custom map markers, but vendored Leaflet CSS
# also references its standard icons. Include them for a self-contained export.
icons = ROOT/'pages-assets/leaflet-images'
for css in OUT.rglob('leaflet.css'):
    assert icons.is_dir(), 'Reviewed Leaflet icon assets are missing'
    shutil.copytree(icons,css.parent/'images',dirs_exist_ok=True)
(OUT/'.nojekyll').touch()
size = sum(p.stat().st_size for p in OUT.rglob('*') if p.is_file())
assert size < 1_000_000_000, 'Pages artifact exceeds 1 GB'
assert (OUT/'index.html').is_file()
print(f'Pages artifact: {size} bytes')
