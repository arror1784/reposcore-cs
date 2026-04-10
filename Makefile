.PHONY: docs

PYTHON      := python3
DOCS_SCRIPT := docs/update-docs-readme.py
DOCS_FILES  := $(wildcard docs/*.md)

## docs/*.md가 README.md보다 새로우면 스크립트 실행, 아니면 스킵
docs/README.md: $(DOCS_FILES)
	$(PYTHON) $(DOCS_SCRIPT)

## 진입점
docs: docs/README.md
