"""Shared rules for exact person identity matching."""

import unicodedata


def normalize_name(name: str) -> str:
    """Normalize identity without discarding accents, ñ, hyphens or word order."""
    normalized_name = unicodedata.normalize("NFC", name)
    return " ".join(normalized_name.split()).casefold()
