import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from app.command_interpreter import core_fallback, normalize_text


class CommandInterpreterTests(unittest.TestCase):
    def test_normalization_removes_spacing_and_punctuation(self):
        self.assertEqual(normalize_text(" 강아지야, 쫓아가! "), "강아지야쫓아가")

    def test_bite_fallback_is_none(self):
        result = core_fallback("물어", "DOG")
        self.assertEqual(result.intent, "NONE")

    def test_chase_fallback_is_track(self):
        result = core_fallback("저 도둑을 쫓아가", "DOG")
        self.assertEqual(result.intent, "TRACK")

    def test_dog_search_fallback_is_search(self):
        result = core_fallback("저 골목을 수색해", "DOG")
        self.assertEqual(result.intent, "SEARCH")

    def test_cat_roof_fallback_is_roof(self):
        result = core_fallback("지붕으로 올라가", "CAT")
        self.assertEqual(result.intent, "ROOF")

    def test_cat_steal_words_are_not_supported(self):
        result = core_fallback("저 보석 훔쳐", "CAT")
        self.assertEqual(result.intent, "NONE")

    def test_cat_distract_fallback_is_distract(self):
        result = core_fallback("경찰 시선 좀 교란해", "CAT")
        self.assertEqual(result.intent, "DISTRACT")


if __name__ == "__main__":
    unittest.main()
