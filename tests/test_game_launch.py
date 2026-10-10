import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from studio import core
from studio.webapp import Application


class GameLaunchTests(unittest.TestCase):
    def test_launch_uses_selected_installation_without_shell(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory)
            executable = game / "TheKarters2.exe"
            executable.touch()
            (game / "GameAssembly.dll").touch()
            app = Application(game)
            with patch("studio.webapp.core.game_running", return_value=False), \
                 patch("studio.webapp.subprocess.Popen") as launch:
                result = app.action("launch-game", {})
            self.assertEqual(result["message"], "The Karters 2 is starting.")
            launch.assert_called_once_with([str(executable)], cwd=str(game), creationflags=core.CREATE_NO_WINDOW)

    def test_launch_refuses_duplicate_game_process(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory)
            (game / "TheKarters2.exe").touch()
            (game / "GameAssembly.dll").touch()
            app = Application(game)
            with patch("studio.webapp.core.game_running", return_value=True), \
                 patch("studio.webapp.subprocess.Popen") as launch:
                with self.assertRaisesRegex(ValueError, "already running"):
                    app.action("launch-game", {})
            launch.assert_not_called()


if __name__ == "__main__":
    unittest.main()
