import unittest

from document_operator.sorting import (
    INVALID_NAME_KEY,
    get_sort_rule,
    sort_by_given_name_surnames,
    sort_by_surname_given_name,
)


class SortingTests(unittest.TestCase):
    def test_sort_profiles_preserve_existing_ordering_rules(self):
        name = "Prueba Norte ANA"

        surname_key = sort_by_surname_given_name(name)
        given_name_key = sort_by_given_name_surnames(name)

        self.assertEqual(surname_key, ("prueba", "ana", "norte"))
        self.assertEqual(given_name_key, ("ana", "prueba", "norte"))

    def test_incomplete_name_sorts_last(self):
        self.assertEqual(sort_by_surname_given_name("Solo Nombre"), INVALID_NAME_KEY)

    def test_unknown_profile_reports_available_rules(self):
        with self.assertRaisesRegex(ValueError, "Disponibles"):
            get_sort_rule("unknown-profile")


if __name__ == "__main__":
    unittest.main()
