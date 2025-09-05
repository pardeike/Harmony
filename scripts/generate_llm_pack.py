#!/usr/bin/env python3
from pathlib import Path
import json

import yaml

OUT_DIR = Path('docs', 'llm-pack')
OUT_DIR.mkdir(parents=True, exist_ok=True)
OUT_FILE = OUT_DIR / 'harmony.cards.jsonl'

cards = []

# API
for path in Path('Documentation/api').rglob('*.yml'):
	with path.open(encoding='utf-8') as f:
		data = yaml.safe_load(f)
	for item in data.get('items', []):
		uid = item.get('uid')
		kind = (item.get('type') or '').lower()
		if not uid or not kind:
			continue
		summary = item.get('summary')
		signature = item.get('syntax', {}).get('content')
		remarks = item.get('remarks')
		parent = item.get('parent')
		href = item.get('href')
		if href:
			doc_url = f'https://harmony.pardeike.net/docs/{href.lstrip("/")}'
		else:
			doc_page = uid if not parent or parent == 'HarmonyLib' else parent
			doc_url = f'https://harmony.pardeike.net/docs/api/{doc_page}.html'
		cards.append({
			'id': uid,
			'kind': kind,
			'summary': summary,
			'signature': signature,
			'remarks': remarks,
			'doc_url': doc_url,
			'examples': None
		})

# Articles
for path in Path('Documentation/articles').rglob('*.md'):
	with path.open(encoding='utf-8') as f:
		lines = [line.strip() for line in f]
	lines = [l for l in lines if l]
	if not lines:
		continue
	summary = lines[0]
	remarks = '\n'.join(lines[1:5]) if len(lines) > 1 else None
	rel = path.relative_to('Documentation')
	slug = rel.with_suffix('').as_posix()
	doc_url = f'https://harmony.pardeike.net/docs/{slug}.html'
	cards.append({
		'id': slug,
		'kind': 'article',
		'summary': summary,
		'signature': None,
		'remarks': remarks,
		'doc_url': doc_url,
		'examples': None
	})

with OUT_FILE.open('w', encoding='utf-8') as out:
	for card in cards:
		out.write(json.dumps(card, ensure_ascii=False) + '\n')
