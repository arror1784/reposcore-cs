"""
---------------------
docs/ 폴더의 마크다운 파일을 탐색하여 /docs/README.md의 문서 목록을 자동으로 갱신합니다.

사용법 (프로젝트 루트에서):
    python tools/update_docs_readme.py

    또는 Makefile을 통해 (변경 감지 포함):
    make docs

동작:
    1. docs/ 폴더에서 README.md를 제외한 모든 .md 파일을 탐색
    2. 각 파일의 첫 번째 H1(#) 헤더를 제목으로 추출
    3. 파일명 기준 알파벳 순으로 정렬
    4. docs/README.md의 <!-- DOC_LIST_START --> ~ <!-- DOC_LIST_END --> 구간을 자동 업데이트
"""

import re
from pathlib import Path

# ── 설정 ──────────────────────────────────────────────────────────────────────
DOCS_DIR    = Path(__file__).parent.parent / "docs"

TEMPLATE_README = DOCS_DIR / "README-template.md"
README_PATH = DOCS_DIR / "README.md"

MARKER_START = "<!-- DOC_LIST_START -->"
MARKER_END   = "<!-- DOC_LIST_END -->"

DEFAULT_README_TEMPLATE = """\
# 문서 목록

이 폴더의 모든 문서를 자동으로 관리합니다.

{start}
{end}
"""
# ──────────────────────────────────────────────────────────────────────────────


def extract_title(md_path: Path) -> str:
    """마크다운 파일에서 첫 번째 H1 헤더 텍스트를 추출합니다.

    H1이 없으면 파일명(확장자 제거)을 반환합니다.
    """
    try:
        with open(md_path, encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line.startswith("# "):
                    return line[2:].strip()
    except (OSError, UnicodeDecodeError):
        pass
    return md_path.stem


def collect_docs(docs_dir: Path) -> list[tuple[str, str]]:
    """docs/ 폴더에서 README.md를 제외한 .md 파일을 수집하고 정렬합니다.

    Returns:
        [(filename, title), ...] 파일명 오름차순 정렬
    """
    entries = []
    for md_file in sorted(docs_dir.glob("*.md")):
        if md_file.name.lower() in {"readme.md", "readme-template.md"}:
            continue
        title = extract_title(md_file)
        entries.append((md_file.name, title))
    return entries


def build_list_block(entries: list[tuple[str, str]]) -> str:
    """수집된 문서 목록을 마크다운 리스트 문자열로 변환합니다."""
    if not entries:
        return "_아직 등록된 문서가 없습니다._"
    lines = []
    for filename, title in entries:
        lines.append(f"- [{filename}](./{filename}): {title}")
    return "\n".join(lines)


def update_readme(readme_path: Path,readme_template_path: Path, new_block: str) -> None:
    """README.md의 마커 구간을 새 목록으로 교체합니다."""
    if not readme_template_path.exists():
        content = DEFAULT_README_TEMPLATE.format(
            start=MARKER_START, end=MARKER_END
        )
        readme_template_path.write_text(content, encoding="utf-8")
        print(f"[생성] {readme_path}")


    original = readme_template_path.read_text(encoding="utf-8")

    if MARKER_START not in original:
        appended = original.rstrip() + f"\n\n{MARKER_START}\n{MARKER_END}\n"
        readme_template_path.write_text(appended, encoding="utf-8")
        original = appended
        print("[안내] 마커가 없어 파일 끝에 추가했습니다.")

    pattern = re.compile(
        rf"{re.escape(MARKER_START)}.*?{re.escape(MARKER_END)}",
        re.DOTALL,
    )
    replacement = f"{MARKER_START}\n{new_block}\n{MARKER_END}"
    updated = pattern.sub(replacement, original)

    readme_path.write_text(updated, encoding="utf-8")
    print(f"[업데이트] {readme_path}")


def main() -> None:
    if not DOCS_DIR.is_dir():
        raise FileNotFoundError(f"docs 폴더를 찾을 수 없습니다: {DOCS_DIR}")
    if not TEMPLATE_README.is_file():
        raise FileNotFoundError(f"README 템플릿을 찾을 수 없습니다: {TEMPLATE_README}")


    entries = collect_docs(DOCS_DIR)
    print(f"[탐색] {len(entries)}개 문서 발견:")
    for name, title in entries:
        print(f"  • {name}: {title}")

    new_block = build_list_block(entries)
    update_readme(README_PATH, TEMPLATE_README, new_block)
    print("완료!")


if __name__ == "__main__":
    main()
