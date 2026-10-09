import ast
import unittest
from pathlib import Path


SOURCE_ROOT = Path(__file__).resolve().parents[1] / "document_operator"
MAX_FUNCTION_LINES = 30
MAX_PARAMETERS = 4
MAX_NESTING_DEPTH = 2
BLOCK_NODES = (
    ast.If,
    ast.For,
    ast.AsyncFor,
    ast.While,
    ast.With,
    ast.AsyncWith,
    ast.Try,
    ast.Match,
)


def source_functions():
    for path in SOURCE_ROOT.glob("*.py"):
        tree = ast.parse(path.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
                yield path, node


def nesting_depth(node, current_depth=0):
    maximum_depth = current_depth
    for child in ast.iter_child_nodes(node):
        is_nested_callable = isinstance(
            child, (ast.FunctionDef, ast.AsyncFunctionDef, ast.Lambda)
        ) and child is not node
        if is_nested_callable:
            continue
        child_depth = current_depth + 1 if isinstance(child, BLOCK_NODES) else current_depth
        maximum_depth = max(maximum_depth, nesting_depth(child, child_depth))
    return maximum_depth


class CodeQualityTests(unittest.TestCase):
    def test_functions_stay_within_line_limit(self):
        violations = [
            f"{path.name}:{node.lineno}:{node.name}"
            for path, node in source_functions()
            if node.end_lineno - node.lineno + 1 > MAX_FUNCTION_LINES
        ]
        self.assertEqual(violations, [])

    def test_functions_stay_within_parameter_limit(self):
        violations = [
            f"{path.name}:{node.lineno}:{node.name}"
            for path, node in source_functions()
            if len(node.args.args) + len(node.args.kwonlyargs) > MAX_PARAMETERS
        ]
        self.assertEqual(violations, [])

    def test_functions_stay_within_nesting_limit(self):
        violations = [
            f"{path.name}:{node.lineno}:{node.name}"
            for path, node in source_functions()
            if nesting_depth(node) > MAX_NESTING_DEPTH
        ]
        self.assertEqual(violations, [])


if __name__ == "__main__":
    unittest.main()
