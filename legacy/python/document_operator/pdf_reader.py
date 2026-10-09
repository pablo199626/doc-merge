"""Extract a traceable, validated list of people from a PDF."""

import hashlib
import io
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Sequence, Tuple

from pypdf import PdfReader
from pypdf.errors import PdfReadError

from document_operator.identity import normalize_name


SECTION_PATTERN = re.compile(r"^Alumnado\s*\(\s*(\d+)\s*\)\s*$", re.IGNORECASE)
HEADER_PATTERN = re.compile(r"^Nombre\s+y\s+apellidos\s*$", re.IGNORECASE)
ORDINAL_PATTERN = re.compile(r"^(\d+)\s*[.)º°]?$")
NUMBER_PATTERN = re.compile(r"^N[.º°oª]*$", re.IGNORECASE)
LIST_TITLE = "lista de nombres"


class PdfInputError(ValueError):
    """Base error for PDF input that cannot be interpreted safely."""


class PdfTextMissingError(PdfInputError):
    """The PDF has no extractable text and may require OCR."""


class PdfStructureError(PdfInputError):
    """The PDF text does not match the supported numbered-list profile."""


@dataclass(frozen=True)
class PdfEntry:
    """One list entry and its location in the source PDF."""

    original_text: str
    identity_key: str
    page_number: int
    ordinal: int

    @property
    def location(self) -> str:
        return f"página {self.page_number}, entrada {self.ordinal}"


@dataclass(frozen=True)
class PdfReadResult:
    """Validated entries, retaining duplicates for later diagnostics."""

    entries: Tuple[PdfEntry, ...]
    declared_total: int
    page_count: int
    sha256: str = ""

    @property
    def duplicates(self) -> Dict[str, Tuple[PdfEntry, ...]]:
        grouped_entries: Dict[str, List[PdfEntry]] = {}
        for entry in self.entries:
            grouped_entries.setdefault(entry.identity_key, []).append(entry)
        return {
            identity_key: tuple(entries)
            for identity_key, entries in grouped_entries.items()
            if len(entries) > 1
        }


def _normalize_lines(text: str) -> List[str]:
    return [" ".join(line.split()) for line in text.splitlines()]


def _is_control_line(line: str) -> bool:
    patterns = (SECTION_PATTERN, HEADER_PATTERN, NUMBER_PATTERN)
    if not line:
        return True
    if any(pattern.fullmatch(line) for pattern in patterns):
        return True
    return line.casefold() == LIST_TITLE


def _find_page_content_start(
    lines: Sequence[str], page_number: int, has_started: bool
) -> Tuple[int, bool]:
    section_indexes = [
        index for index, line in enumerate(lines) if SECTION_PATTERN.fullmatch(line)
    ]
    header_indexes = [
        index for index, line in enumerate(lines) if HEADER_PATTERN.fullmatch(line)
    ]
    if section_indexes:
        following_headers = [
            index for index in header_indexes if index > section_indexes[-1]
        ]
        if not following_headers:
            raise PdfStructureError(
                f"La sección Alumnado de la página {page_number} no tiene la cabecera "
                "Nombre y apellidos."
            )
        return following_headers[-1] + 1, True
    if has_started and header_indexes:
        return header_indexes[-1] + 1, True
    if has_started:
        return 0, True
    return len(lines), False


def _extract_page_candidates(
    lines: Sequence[str], page_number: int, has_started: bool
) -> Tuple[List[Tuple[int, str, int]], bool]:
    content_start, has_started = _find_page_content_start(
        lines, page_number, has_started
    )
    ordinal_positions = [
        (index, int(match.group(1)))
        for index in range(content_start, len(lines))
        for match in [ORDINAL_PATTERN.fullmatch(lines[index])]
        if match
    ]
    candidates = []
    for position, (line_index, ordinal) in enumerate(ordinal_positions):
        end_index = _candidate_end_index(position, ordinal_positions, len(lines))
        name_parts = [
            line
            for line in lines[line_index + 1:end_index]
            if not _is_control_line(line)
        ]
        if not name_parts:
            raise PdfStructureError(
                f"La entrada {ordinal} de la página {page_number} no contiene un nombre."
            )
        candidates.append((ordinal, " ".join(name_parts), page_number))
    return candidates, has_started


def _candidate_end_index(
    position: int, ordinal_positions: Sequence[Tuple[int, int]], line_count: int
) -> int:
    next_position = position + 1
    if next_position < len(ordinal_positions):
        return ordinal_positions[next_position][0]
    return line_count


def _extract_declared_total(page_lines: Sequence[Sequence[str]]) -> int:
    totals = [
        int(match.group(1))
        for lines in page_lines
        for line in lines
        for match in [SECTION_PATTERN.fullmatch(line)]
        if match
    ]
    if not totals:
        raise PdfStructureError('No se encontró una sección "Alumnado (n)".')
    if len(set(totals)) != 1:
        raise PdfStructureError("Las cabeceras Alumnado declaran totales diferentes.")
    declared_total = totals[0]
    if declared_total <= 0:
        raise PdfStructureError(
            "La lista de alumnado está vacía; no se autoriza el procesamiento."
        )
    return declared_total


def _validate_candidates(
    candidates: Sequence[Tuple[int, str, int]], declared_total: int
) -> None:
    if len(candidates) != declared_total:
        raise PdfStructureError(
            f"La lista declara {declared_total} personas, pero se extrajeron "
            f"{len(candidates)}."
        )
    actual_ordinals = [ordinal for ordinal, _, _ in candidates]
    expected_ordinals = list(range(1, declared_total + 1))
    if actual_ordinals != expected_ordinals:
        raise PdfStructureError(
            f"La numeración extraída no es consecutiva: {actual_ordinals}; "
            f"se esperaba {expected_ordinals}."
        )


def parse_pdf_pages(page_texts: Sequence[str]) -> PdfReadResult:
    """Interpret extracted page text using the supported PDF profile."""
    if not page_texts or not any((text or "").strip() for text in page_texts):
        raise PdfTextMissingError(
            "El PDF no contiene texto extraíble; puede necesitar OCR."
        )
    page_lines = [_normalize_lines(text or "") for text in page_texts]
    declared_total = _extract_declared_total(page_lines)
    candidates: List[Tuple[int, str, int]] = []
    has_started = False
    for page_number, lines in enumerate(page_lines, start=1):
        page_candidates, has_started = _extract_page_candidates(
            lines, page_number, has_started
        )
        candidates.extend(page_candidates)
    _validate_candidates(candidates, declared_total)
    entries = tuple(
        PdfEntry(name, normalize_name(name), page_number, ordinal)
        for ordinal, name, page_number in candidates
    )
    return PdfReadResult(entries, declared_total, len(page_texts))


def _extract_pdf_text(content: bytes) -> List[str]:
    reader = PdfReader(io.BytesIO(content))
    if reader.is_encrypted and reader.decrypt("") == 0:
        raise PdfInputError("El PDF está cifrado y requiere una contraseña.")
    return [page.extract_text() or "" for page in reader.pages]


def read_pdf_names(pdf_path: Path) -> PdfReadResult:
    """Read a PDF and return a validated, traceable list of people."""
    path = Path(pdf_path)
    if path.suffix.casefold() != ".pdf":
        raise PdfInputError("El listado debe tener extensión .pdf.")
    if not path.is_file():
        raise PdfInputError(f"No existe el archivo PDF: {path}")
    try:
        content = path.read_bytes()
        page_texts = _extract_pdf_text(content)
    except PdfInputError:
        raise
    except (OSError, PdfReadError, ValueError) as error:
        raise PdfInputError(f"No se pudo leer el PDF: {error}") from error
    result = parse_pdf_pages(page_texts)
    return PdfReadResult(
        result.entries,
        result.declared_total,
        result.page_count,
        hashlib.sha256(content).hexdigest(),
    )
