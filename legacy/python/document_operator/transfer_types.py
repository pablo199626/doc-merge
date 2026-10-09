"""Shared transfer classifications without infrastructure dependencies."""

from enum import Enum


class ActionType(Enum):
    ADD = "add"
    REPLACE = "replace"
    KEEP = "keep"
    DELETE = "delete"
    SKIP = "skip"


class IssueType(Enum):
    DUPLICATE_PDF_ENTRY = "pdf_duplicado"
    AMBIGUOUS_DESTINATION = "destino_ambiguo"
    MISSING_SOURCE = "origen_ausente"
    AMBIGUOUS_SOURCE = "origen_ambiguo"
