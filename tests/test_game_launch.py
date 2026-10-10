import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from studio import core
from studio.webapp import Application


class GameLaunchTests(unittest.TestCase):
    def test_launch_uses_steam_to_avoid_unity_relaunch_conflicts(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory)
            executable = game / "TheKarters2.exe"
            executable.touch()
            (game / "GameAssembly.dll").touch()
            app = Application(game)
            with patch("studio.webapp.core.game_running", return_value=False), \
                 patch("studio.webapp.os.startfile") as launch:
                result = app.action("launch-game", {})
            self.assertEqual(result["message"], "Steam is starting The Karters 2.")
            launch.assert_called_once_with("steam://rungameid/2269950")

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

    def test_launch_refuses_second_request_while_first_process_is_starting(self):
        with tempfile.TemporaryDirectory() as directory:
            game = Path(directory)
            (game / "TheKarters2.exe").touch()
            (game / "GameAssembly.dll").touch()
            app = Application(game)
            with patch("studio.webapp.core.game_running", return_value=False), \
                 patch("studio.webapp.os.startfile") as launch:
                app.action("launch-game", {})
                with self.assertRaisesRegex(ValueError, "still starting"):
                    app.action("launch-game", {})
            launch.assert_called_once()


if __name__ == "__main__":
    unittest.main()
