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


if __name__ == "__main__":
    unittest.main()
