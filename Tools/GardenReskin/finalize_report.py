"""Check final asset hashes and collect the latest successful runtime evidence."""
import hashlib
import json
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / 'Docs/GardenReskin'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

structure = read(DOCS / 'structure-report.json')
assert structure['passed'] and not structure['newFiles']
manifest = read(DOCS / 'import-manifest.json')
imported = read(DOCS / 'import-report.json')
assert imported['status'] == 'PASS'
# A dry-run report can re-audit already imported artwork: every expected output
# must still match the actual Assets bytes below, so unimported changes fail.
assert len(manifest['entries']) == imported['targetCount']
for entry in imported['entries']:
    assert sha(ROOT / entry['target']) == entry['outputSha256'].lower(), entry['target']

sources = [
    ('main', 'runtime-polish-final'),
    ('welcome', 'runtime-final'),
    ('teaching', 'runtime-teaching-final'),
    ('success', 'runtime-final'),
    ('failure', 'runtime-final'),
    ('withdrawal', 'runtime-withdrawal-final'),
    ('tooloffer', 'runtime-tooloffer-final'),
]
evidence = []
for suite, folder in sources:
    results = read(DOCS / folder / 'runtime-validation.json')
    if isinstance(results, dict):
        results = [results]
    case = next(item for item in results if item['suite'] == suite)
    assert case['passed'] and case['exitCode'] == 0, suite
    assert case['marker'] in Path(case['log']).read_text(encoding='utf-8-sig'), suite
    for shot in case['screenshots']:
        assert sha(Path(shot['path'])) == shot['sha256'].lower(), shot['path']
    evidence.append(case)

capture_audit = read(DOCS / 'runtime-withdrawal-final.patch-audit/capture-patch-audit.json')
assert capture_audit['sourceRestored']
assert sha(Path(capture_audit['source'])) == capture_audit['sourceOriginalSha256'].lower()

coverage = read(DOCS / 'coverage.json')
assert not coverage['uncovered']
with zipfile.ZipFile(DOCS / 'OriginalAssets/before-reskin.zip') as archive:
    for relative in coverage['preservedCandidates']:
        path = 'Assets/' + relative
        assert (ROOT / path).read_bytes() == archive.read(path), path
    old_png_bytes = sum(archive.getinfo(e['target']).file_size for e in manifest['entries'])
new_png_bytes = sum((ROOT / e['target']).stat().st_size for e in manifest['entries'])
report = {
    'status': 'PASS',
    'project': str(ROOT),
    'evidenceType': 'Unity Play Mode real rendering with explicit test fixtures; not AI previews or device captures',
    'textureReplacements': len(manifest['entries']),
    'prefabsWithColourOnlyChanges': sum(e['kind'] == 'structure' and e['path'].endswith('.prefab') for e in structure['changed']),
    'materialsWithPaletteChanges': sum(e['kind'] == 'material' for e in structure['changed']),
    'structureBaselineFiles': structure['baselineFiles'],
    'newAssets': len(structure['newFiles']),
    'runtimeSuiteCount': len(evidence),
    'runtimeScreenshotCount': sum(len(case['screenshots']) for case in evidence),
    'sourcePngBytes': {'before': old_png_bytes, 'after': new_png_bytes},
    'preservedCandidates': coverage['preservedCandidates'],
    'runtimeEvidence': evidence,
    'withdrawalValidationSourceRestored': True,
    'limitations': ['No Android/iOS physical-device build or test performed.',
                    'Tool offer advertising uses the existing simulated callback fixture.',
                    'Older gameplay/tools validators with stale reward/offer assertions were not counted as passing.',
                    'Other infrequent dialogs use replaced shared assets and static structural checks; not every dialog state has a runtime capture.'],
}
revisions = []
for revision_folder in ['border-thinning', 'top-hints-match', 'withdraw-label-match', 'bottom-toolbar-match', 'tutorial-progress-match']:
    revision_path = DOCS / revision_folder / 'revision.json'
    if revision_path.exists():
        revision = read(revision_path)
        assert sha(Path(revision['screenshot'])) == revision['screenshotSha256'].lower()
        for file in revision.get('verifiedFiles', []):
            assert sha(ROOT / file['path']) == file['sha256'].lower(), file['path']
        revisions.append(revision)
if revisions:
    report['visualRevisions'] = revisions
    report['visualRevisionAssetAdditions'] = [path for revision in revisions for path in revision.get('newAssets', [])]
    report['visualRevisionNewTextureCount'] = sum(revision.get('newTextureCount', 0) for revision in revisions)
functional_revision_path = DOCS / 'ui-click-feedback/revision.json'
if functional_revision_path.exists():
    functional_revision = read(functional_revision_path)
    for file in functional_revision['verifiedFiles']:
        assert sha(ROOT / file['path']) == file['sha256'].lower(), file['path']
    report['functionalRevisions'] = [functional_revision]
    report['structureReportScope'] = 'Historical visual-reskin-only baseline, including its newAssets=0 field. Later runtime code and Editor validation additions are recorded in functionalRevisions; the replay background texture/Sprite and Image reference changes are recorded in visualRevisions and visualRevisionAssetAdditions.'
(DOCS / 'delivery-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({k: report[k] for k in ['status','textureReplacements','runtimeSuiteCount','runtimeScreenshotCount','newAssets']}, ensure_ascii=False))
