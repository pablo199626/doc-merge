"""Configurable ordering rules for rows in a people table."""

from dataclasses import dataclass
from typing import Callable, Dict, Optional, Tuple


INVALID_NAME_KEY = ("\uffff", "\uffff", "\uffff")
SortKey = Tuple[str, str, str]
SortRule = Callable[[str], SortKey]


@dataclass(frozen=True)
class NameParts:
    first_surname: str
    second_surname: str
    given_name: str


def split_name(full_name: str) -> Optional[NameParts]:
    """Split ``FirstSurname SecondSurname GivenName`` for the legacy profile."""
    parts = full_name.split()
    if len(parts) < 3:
        return None
    return NameParts(" ".join(parts[:-2]), parts[-2], parts[-1])


def sort_by_surname_given_name(full_name: str) -> SortKey:
    """Sort by first surname, given name, then second surname."""
    name_parts = split_name(full_name)
    if name_parts is None:
        return INVALID_NAME_KEY
    return (
        name_parts.first_surname.casefold(),
        name_parts.given_name.casefold(),
        name_parts.second_surname.casefold(),
    )


def sort_by_given_name_surnames(full_name: str) -> SortKey:
    """Sort by given name, then first and second surname."""
    name_parts = split_name(full_name)
    if name_parts is None:
        return INVALID_NAME_KEY
    return (
        name_parts.given_name.casefold(),
        name_parts.first_surname.casefold(),
        name_parts.second_surname.casefold(),
    )


SORT_RULES: Dict[str, SortRule] = {
    "apellido-nombre-segundo-apellido": sort_by_surname_given_name,
    "nombre-apellidos": sort_by_given_name_surnames,
}


def get_sort_rule(rule_name: str) -> SortRule:
    """Return a configured sort-key function or raise an actionable error."""
    if rule_name in SORT_RULES:
        return SORT_RULES[rule_name]
    available_rules = ", ".join(SORT_RULES)
    raise ValueError(
        f"Regla de ordenación no válida: {rule_name}. Disponibles: {available_rules}"
    )
