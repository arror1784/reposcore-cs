.PHONY: all docs synopsis

PYTHON      := python3
DOCS_SCRIPT := tools/update-readme.py
DOCS_FILES  := $(wildcard docs/*.md)
DOCS_README_TPL    := docs/README-template.md
DOCS_README_SRC    := $(filter-out docs/README.md $(DOCS_README_TPL),$(DOCS_FILES))

SYNOPSIS_SCRIPT := tools/update-synopsis.py
SYNOPSIS_DEPENDENCIES := Program.cs reposcore-cs.csproj

all: docs

## docs/README-template.md 또는 docs/*.md가 README.md보다 새로우면 스크립트 실행
docs/README.md: $(DOCS_README_SRC) $(DOCS_README_TPL)
	$(PYTHON) $(DOCS_SCRIPT)

## SYNOPSIS_DEPENDENCIES 파일들이 README.md보다 최근에 수정되었으면 스크립트 실행
README.md: $(SYNOPSIS_DEPENDENCIES)
	$(PYTHON) $(SYNOPSIS_SCRIPT)

## 진입점
docs: docs/README.md README.md
