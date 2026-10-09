"""Resolve exact identities into a safe, inspectable transfer plan."""

from dataclasses import dataclass
from typing import Dict, List, Optional, Sequence, Tuple

from document_operator.docx_processor import DocxReadResult, DocxRecord
from document_operator.pdf_reader import PdfEntry, PdfReadResult
from document_operator.transfer_types import ActionType, IssueType


ACTION_LABELS = {
    ActionType.ADD: "Añadir",
    ActionType.REPLACE: "Reemplazar",
    ActionType.KEEP: "Conservar",
    ActionType.DELETE: "Eliminar",
    ActionType.SKIP: "Omitir",
}
ACTION_COUNT_LABELS = {
    ActionType.ADD: "anadir",
    ActionType.REPLACE: "reemplazar",
    ActionType.KEEP: "conservar",
    ActionType.DELETE: "eliminar",
    ActionType.SKIP: "omitir",
}


@dataclass(frozen=True)
class TransferIssue:
    issue_type: IssueType
    identity_key: str
    name: str
    detail: str
    references: Tuple[str, ...] = ()


@dataclass(frozen=True)
class TransferAction:
    action_type: ActionType
    identity_key: str
    name: str
    pdf_entries: Tuple[PdfEntry, ...] = ()
    source: Optional[DocxRecord] = None
    destination: Optional[DocxRecord] = None
    reason: Optional[IssueType] = None


@dataclass(frozen=True)
class TransferPlan:
    actions: Tuple[TransferAction, ...]
    issues: Tuple[TransferIssue, ...]
    source_hash: str
    destination_hash: str
    list_hash: str
    pdf_total: int

    def actions_of_type(self, action_type: ActionType) -> Tuple[TransferAction, ...]:
        return tuple(
            action for action in self.actions if action.action_type is action_type
        )

    @property
    def action_counts(self) -> Dict[ActionType, int]:
        return {
            action_type: len(self.actions_of_type(action_type))
            for action_type in ActionType
        }


@dataclass(frozen=True)
class IdentityResolutionRequest:
    identity_key: str
    name: str
    entries: Tuple[PdfEntry, ...]
    source_records: Sequence[DocxRecord]
    destination_record: Optional[DocxRecord]


@dataclass(frozen=True)
class PlanIndexes:
    identity_order: Sequence[str]
    pdf_by_key: Dict[str, List[PdfEntry]]
    source_by_key: Dict[str, List[DocxRecord]]
    destination_by_key: Dict[str, List[DocxRecord]]
    ambiguous_destination_keys: set


def _group_records(records: Sequence[DocxRecord]) -> Dict[str, List[DocxRecord]]:
    grouped_records: Dict[str, List[DocxRecord]] = {}
    for record in records:
        grouped_records.setdefault(record.identity_key, []).append(record)
    return grouped_records


def _group_pdf_entries(
    entries: Sequence[PdfEntry],
) -> Tuple[Dict[str, List[PdfEntry]], List[str]]:
    grouped_entries: Dict[str, List[PdfEntry]] = {}
    identity_order = []
    for entry in entries:
        if entry.identity_key not in grouped_entries:
            grouped_entries[entry.identity_key] = []
            identity_order.append(entry.identity_key)
        grouped_entries[entry.identity_key].append(entry)
    return grouped_entries, identity_order


def _create_pdf_issues(
    entries_by_key: Dict[str, List[PdfEntry]], identity_order: Sequence[str]
) -> List[TransferIssue]:
    issues = []
    for identity_key in identity_order:
        entries = entries_by_key[identity_key]
        if len(entries) <= 1:
            continue
        issues.append(TransferIssue(
            IssueType.DUPLICATE_PDF_ENTRY,
            identity_key,
            entries[0].original_text,
            f"El PDF contiene {len(entries)} entradas para la misma persona.",
            tuple(entry.location for entry in entries),
        ))
    return issues


def _preserve_ambiguous_destinations(
    destination: DocxReadResult,
    destination_by_key: Dict[str, List[DocxRecord]],
    pdf_by_key: Dict[str, List[PdfEntry]],
) -> Tuple[List[TransferAction], List[TransferIssue], set]:
    ambiguous_keys = {
        identity_key
        for identity_key, records in destination_by_key.items()
        if len(records) > 1
    }
    actions = [
        TransferAction(
            ActionType.KEEP,
            record.identity_key,
            record.original_name,
            tuple(pdf_by_key.get(record.identity_key, ())),
            destination=record,
            reason=IssueType.AMBIGUOUS_DESTINATION,
        )
        for record in destination.records
        if record.identity_key in ambiguous_keys
    ]
    issues = [
        _create_destination_issue(identity_key, destination_by_key[identity_key])
        for identity_key in ambiguous_keys
    ]
    return actions, issues, ambiguous_keys


def _create_destination_issue(
    identity_key: str, records: Sequence[DocxRecord]
) -> TransferIssue:
    return TransferIssue(
        IssueType.AMBIGUOUS_DESTINATION,
        identity_key,
        records[0].original_name,
        f"El destino contiene {len(records)} registros con la misma identidad; "
        "se conservan todos.",
        tuple(record.location for record in records),
    )


def _resolve_pdf_identity(
    identity_key: str,
    entries: Tuple[PdfEntry, ...],
    source_records: Sequence[DocxRecord],
    destination_records: Sequence[DocxRecord],
) -> Tuple[TransferAction, Optional[TransferIssue]]:
    destination_record = _first_record(destination_records)
    name = entries[0].original_text
    if len(source_records) == 1:
        action_type = _source_action_type(destination_record)
        action = TransferAction(
            action_type,
            identity_key,
            name,
            entries,
            source_records[0],
            destination_record,
        )
        return action, None
    return _resolve_missing_or_ambiguous_source(IdentityResolutionRequest(
        identity_key,
        name,
        entries,
        source_records,
        destination_record,
    ))


def _first_record(records: Sequence[DocxRecord]) -> Optional[DocxRecord]:
    if records:
        return records[0]
    return None


def _source_action_type(destination_record: Optional[DocxRecord]) -> ActionType:
    if destination_record is not None:
        return ActionType.REPLACE
    return ActionType.ADD


def _source_issue_type(source_records: Sequence[DocxRecord]) -> IssueType:
    if source_records:
        return IssueType.AMBIGUOUS_SOURCE
    return IssueType.MISSING_SOURCE


def _safe_action_type(destination_record: Optional[DocxRecord]) -> ActionType:
    if destination_record is not None:
        return ActionType.KEEP
    return ActionType.SKIP


def _resolve_missing_or_ambiguous_source(
    request: IdentityResolutionRequest,
) -> Tuple[TransferAction, TransferIssue]:
    issue_type = _source_issue_type(request.source_records)
    detail = "La persona del PDF no aparece en el documento de origen."
    if request.source_records:
        detail = (
            f"El origen contiene {len(request.source_records)} registros "
            "con la misma identidad."
        )
    references = tuple(entry.location for entry in request.entries) + tuple(
        record.location for record in request.source_records
    )
    action_type = _safe_action_type(request.destination_record)
    action = TransferAction(
        action_type,
        request.identity_key,
        request.name,
        request.entries,
        destination=request.destination_record,
        reason=issue_type,
    )
    issue = TransferIssue(
        issue_type, request.identity_key, request.name, detail, references
    )
    return action, issue


def _resolve_listed_people(indexes):
    actions = []
    issues = []
    for identity_key in indexes.identity_order:
        if identity_key in indexes.ambiguous_destination_keys:
            continue
        action, issue = _resolve_pdf_identity(
            identity_key,
            tuple(indexes.pdf_by_key[identity_key]),
            indexes.source_by_key.get(identity_key, ()),
            indexes.destination_by_key.get(identity_key, ()),
        )
        actions.append(action)
        if issue is not None:
            issues.append(issue)
    return actions, issues


def _create_deletion_actions(destination, pdf_by_key, ambiguous_destination_keys):
    pdf_keys = set(pdf_by_key)
    return [
        TransferAction(
            ActionType.DELETE,
            record.identity_key,
            record.original_name,
            destination=record,
        )
        for record in destination.records
        if record.identity_key not in pdf_keys
        and record.identity_key not in ambiguous_destination_keys
    ]


def create_transfer_plan(
    pdf_result: PdfReadResult,
    source: DocxReadResult,
    destination: DocxReadResult,
) -> TransferPlan:
    """Build transfer actions without modifying any input document."""
    pdf_by_key, identity_order = _group_pdf_entries(pdf_result.entries)
    source_by_key = _group_records(source.records)
    destination_by_key = _group_records(destination.records)
    issues = _create_pdf_issues(pdf_by_key, identity_order)
    actions, destination_issues, ambiguous_keys = _preserve_ambiguous_destinations(
        destination, destination_by_key, pdf_by_key
    )
    issues.extend(destination_issues)
    indexes = PlanIndexes(
        identity_order, pdf_by_key, source_by_key, destination_by_key, ambiguous_keys
    )
    listed_actions, listed_issues = _resolve_listed_people(indexes)
    actions.extend(listed_actions)
    issues.extend(listed_issues)
    actions.extend(_create_deletion_actions(destination, pdf_by_key, ambiguous_keys))
    return TransferPlan(
        tuple(actions),
        tuple(issues),
        source.sha256,
        destination.sha256,
        pdf_result.sha256,
        pdf_result.declared_total,
    )


def format_plan_summary(plan: TransferPlan, heading: str) -> str:
    """Return a deterministic, human-readable transfer summary."""
    lines = [heading]
    for action in plan.actions:
        reason = _format_action_reason(action)
        lines.append(f"- {ACTION_LABELS[action.action_type]}: {action.name}{reason}")
    counts = plan.action_counts
    count_text = ", ".join(
        f"{ACTION_COUNT_LABELS[action_type]}={counts[action_type]}"
        for action_type in ActionType
    )
    lines.append(f"Totales: {count_text}")
    return "\n".join(lines)


def format_simulation_summary(plan: TransferPlan) -> str:
    return format_plan_summary(plan, "Simulación de transferencia:")


def format_execution_summary(plan: TransferPlan) -> str:
    return format_plan_summary(plan, "Resumen de transferencia:")


def format_issue_summary(plan: TransferPlan) -> str:
    """Explain unresolved cases and the safe action taken for each one."""
    if not plan.issues:
        return "Sin incidencias pendientes de revisión manual."
    lines = [f"Incidencias para revisión manual ({len(plan.issues)}):"]
    for issue in plan.issues:
        references = _format_issue_references(issue)
        lines.append(f"- {issue.name}: {issue.detail}{references}")
    return "\n".join(lines)


def _format_action_reason(action: TransferAction) -> str:
    if action.reason is not None:
        return f" ({action.reason.value})"
    return ""


def _format_issue_references(issue: TransferIssue) -> str:
    if issue.references:
        return f" [{'; '.join(issue.references)}]"
    return ""
