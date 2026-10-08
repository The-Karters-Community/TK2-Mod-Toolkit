import argparse
import json
from pathlib import Path
from . import core


def main():
    parser = argparse.ArgumentParser(description="TK2 local mod tools")
    parser.add_argument("--game", type=Path, default=core.DEFAULT_GAME)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("diagnose")
    index = sub.add_parser("index")
    index.add_argument("--dump", type=Path)
    build = sub.add_parser("build")
    build.add_argument("--project", type=Path, default=core.ROOT / "plugins/TK2.Customization/TK2.Customization.csproj")
    deploy = sub.add_parser("deploy")
    deploy.add_argument("--artifact", type=Path, default=core.ROOT / "artifacts/TK2.Customization")
    args = parser.parse_args()
    if args.command == "diagnose":
        result = core.diagnose(args.game)
        core.write_json(core.ROOT / "local/installation.json", result)
    elif args.command == "index":
        result = core.index_dump(args.dump or args.game / "Il2CppDumperOutput/dump.cs", core.ROOT / "local/catalog.json")
        result = {"types": result["typeCount"], "source": result["source"], "sha256": result["sha256"]}
    elif args.command == "build":
        result = core.build_plugin(args.game, args.project, print)
        result = {"artifact": result["artifact"], "runtimeTested": False}
    else:
        result = {"backup": str(core.deploy_plugin(args.game, args.artifact))}
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
