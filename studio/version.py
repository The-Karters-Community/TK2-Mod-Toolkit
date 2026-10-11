"""Next release version. Source checkouts display a development suffix."""
APP_VERSION = "0.6.24"


def display_version(frozen=False):
    return APP_VERSION if frozen else APP_VERSION + "-dev"
