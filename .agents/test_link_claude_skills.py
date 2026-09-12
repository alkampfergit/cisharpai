"""Run with: python3 -m unittest discover -s .agents -p 'test_*.py'."""

from pathlib import Path
import tempfile
import unittest

from link_claude_skills import link_skills


class LinkSkillsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='skills with spaces ')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.skill = self.root / '.claude/skills/example'
        self.skill.mkdir(parents=True)
        (self.skill / 'SKILL.md').write_text('Example skill', encoding='utf-8')
        (self.skill / 'references').mkdir()
        (self.skill / 'references/guide.md').write_text('Guide', encoding='utf-8')
        self.destination = self.root / '.agents/skills'

    def test_relative_links_resources_and_idempotence(self):
        self.assertEqual(link_skills(self.root), 1)
        link = self.destination / 'example'
        self.assertTrue(link.is_symlink())
        self.assertFalse(link.readlink().is_absolute())
        self.assertEqual((link / 'references/guide.md').read_text(), 'Guide')
        self.assertEqual(link_skills(self.root), 0)

    def test_conflicts_preserved_before_any_links_are_created(self):
        for kind in ('file', 'directory', 'broken_link'):
            with self.subTest(kind=kind):
                self.destination.mkdir(parents=True, exist_ok=True)
                conflict = self.destination / 'example'
                if kind == 'file':
                    conflict.write_text('Keep me')
                elif kind == 'directory':
                    conflict.mkdir()
                else:
                    conflict.symlink_to('missing', target_is_directory=True)
                extra = self.root / '.claude/skills/aaa'
                extra.mkdir(exist_ok=True)
                (extra / 'SKILL.md').touch()
                with self.assertRaises(ValueError):
                    link_skills(self.root)
                self.assertFalse((self.destination / 'aaa').exists())
                if kind == 'file':
                    self.assertEqual(conflict.read_text(), 'Keep me')
                if kind == 'directory':
                    conflict.rmdir()
                else:
                    conflict.unlink()

    def test_missing_source(self):
        with self.assertRaises(ValueError):
            link_skills(self.root / 'missing')

    def test_non_skill_directories_are_ignored(self):
        (self.skill.parent / 'not-a-skill').mkdir()
        self.assertEqual(link_skills(self.root), 1)
        self.assertFalse((self.destination / 'not-a-skill').exists())

    def test_symlink_destination_is_rejected(self):
        self.destination.parent.mkdir()
        self.destination.symlink_to(self.skill.parent, target_is_directory=True)
        with self.assertRaises(ValueError):
            link_skills(self.root)


if __name__ == '__main__':
    unittest.main()
