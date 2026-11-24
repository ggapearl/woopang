"""문서 점검 — 저장소 루트에서  python docs/claude/docs_lint.py

에러(종료코드 1): 깨진 마크다운 링크 · 옛 회사명 표기 · 없는 「CLAUDE.md N.N」 절 참조 · INDEX.md 에 없는 docs/claude 문서
              · CLAUDE.md 3번 표에 있는데 INDEX.md 지도에 없는 문서
경고: 파일 60KB 초과 · 한 줄 1500자 초과 · CLAUDE.md 200줄 초과 · H1 이 하나가 아닌 문서 · 「최종 갱신」 날짜 없는 docs/claude 문서
검사 대상: CLAUDE.md · AGENTS.md · docs/claude/*.md · .claude/**/*.md · server/voice 의 CLAUDE.md·docs·desk md
제외: docs/archive(보관) · docs/private(비공개) · .claude/worktrees(옛 작업 복사본). 코드 블록(```)·인라인 코드(`) 안은 링크·제목 검사에서 뺀다.
"""
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
ROOT = Path(__file__).resolve().parents[2]

# 옛 표기가 나와도 되는 줄: 금지 목록·교체 기록·옛 지시를 설명하는 줄
BRAND_TERMS = ['QUE.ENT', 'QUE. ENT', '쾌엔터', 'KWAE', 'Kwae', 'KWEA', 'QUE Entertainment', '쾌ENT']
BRAND_OK = re.compile(r'금지|옛 |옛표기|교체|폐기|바뀜|바꿈|바꿨|바뀐|개정|틀린|삭제|BAD_BRAND|❌|→')
LINK = re.compile(r'\[[^\]]*\]\(([^)\s]+)\)')
SEC_REF = re.compile(r'(?<![/\w])CLAUDE\.md\s*(?:의\s*)?(\d+(?:\.\d+)+)')

errors, warns = [], []


def targets():
    out = [ROOT / 'CLAUDE.md', ROOT / 'AGENTS.md']
    out += sorted((ROOT / 'docs' / 'claude').glob('*.md'))
    out += [p for p in sorted((ROOT / '.claude').rglob('*.md')) if 'worktrees' not in p.relative_to(ROOT).parts]
    v = ROOT / 'server' / 'voice'
    out.append(v / 'CLAUDE.md')
    out += sorted((v / 'docs').glob('*.md')) + sorted((v / 'desk').glob('*.md'))
    return [p for p in out if p.exists()]


def prose_lines(text):
    """코드 블록 밖의 (줄번호, 줄)."""
    fence = False
    for n, line in enumerate(text.split('\n'), 1):
        if line.lstrip().startswith('```'):
            fence = not fence
            continue
        if not fence:
            yield n, line.rstrip('\r')


def rel(p):
    return p.relative_to(ROOT).as_posix()


claude_text = (ROOT / 'CLAUDE.md').read_text(encoding='utf-8')
sections = set()
for m in re.finditer(r'^#{2,4} (\d+(?:\.\d+)*)(?:~(\d+(?:\.\d+)*))?', claude_text, re.M):
    sections.update(g for g in m.groups() if g)

for p in targets():
    text = p.read_text(encoding='utf-8', errors='replace')
    name = rel(p)
    lines = text.split('\n')
    if len(text.encode('utf-8')) > 60 * 1024:
        warns.append(f'{name}: {len(text.encode("utf-8")) // 1024}KB — 쪼개기 후보(60KB 초과)')
    fm_end = (next((i for i in range(1, len(lines)) if lines[i].strip() == '---'), 0) + 1) if text.startswith('---') else 0
    for n, l in enumerate(lines, 1):
        if len(l) > 1500 and n > fm_end:  # 앞머리(frontmatter) 의 한 줄 description 은 나눌 수 없다
            warns.append(f'{name}:{n}: 한 줄 {len(l)}자 — 나눠 쓰기 후보')
    h1 = [n for n, l in prose_lines(text) if re.match(r'^# ', l)]
    if not text.startswith('---') and len(h1) != 1:
        warns.append(f'{name}: H1 이 {len(h1)}개(1개여야 함)')
    for n, l in prose_lines(text):
        for href in LINK.findall(re.sub(r'`[^`]*`', '', l)):
            if re.match(r'^(https?:|mailto:|#)', href) or any(c in href for c in '*<>'):
                continue
            target = (p.parent / href.split('#')[0]).resolve()
            if href.split('#')[0] and not target.exists():
                errors.append(f'{name}:{n}: 링크 대상 없음 → {href}')
        if '가중치' not in l:
            for ref in SEC_REF.findall(l):
                if ref not in sections:
                    errors.append(f'{name}:{n}: 「CLAUDE.md {ref}」 절이 CLAUDE.md 에 없음 — 문서 이름으로 가리킬 것')
    for n, l in enumerate(lines, 1):
        for term in BRAND_TERMS:
            if term in l and not BRAND_OK.search(l):
                errors.append(f'{name}:{n}: 옛 회사명 표기 「{term}」 — 「주식회사 쾌」/「QUE Co., Ltd.」/「QUE」로')
                break

if len(claude_text.split('\n')) > 200:
    warns.append(f'CLAUDE.md: {len(claude_text.split(chr(10)))}줄 — 200줄 안쪽 권장')

index = (ROOT / 'docs' / 'claude' / 'INDEX.md').read_text(encoding='utf-8')
for p in sorted((ROOT / 'docs' / 'claude').glob('*.md')):
    if p.name != 'INDEX.md' and p.name not in index:
        errors.append(f'docs/claude/{p.name}: INDEX.md 지도에 없음 — 한 줄 추가할 것')
    if p.name != 'INDEX.md' and not any('최종 갱신' in l for l in p.read_text(encoding='utf-8').split('\n')[:10]):
        warns.append(f'docs/claude/{p.name}: 첫머리에 「> 최종 갱신: YYYY-MM-DD」 가 없음')

# CLAUDE.md 3번 표에 적힌 문서는 INDEX.md 지도에도 있어야 한다 — 표와 지도가 어긋나는 것을 막는다
t0 = claude_text.find('## 3.')
t1 = claude_text.find('\n---', t0)
for ref in sorted(set(re.findall(r'docs/claude/[\w.-]+|server/[\w/.-]+\.md|docs/private/', claude_text[t0:t1]))):
    if ref not in index:
        errors.append(f'CLAUDE.md 3번 표의 「{ref}」 가 INDEX.md 지도에 없음 — 표와 지도를 맞출 것')

for e in errors:
    print('[에러]', e)
for w in warns:
    print('[경고]', w)
print(f'\n에러 {len(errors)}건 · 경고 {len(warns)}건 (검사한 문서 {len(targets())}개)')
sys.exit(1 if errors else 0)
