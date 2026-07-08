#!/usr/bin/env python3
from __future__ import annotations

import argparse
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer


class UnityWebGlHandler(SimpleHTTPRequestHandler):
    extensions_map = {
        **SimpleHTTPRequestHandler.extensions_map,
        ".data": "application/octet-stream",
        ".wasm": "application/wasm",
        ".js": "application/javascript",
        ".symbols.json": "application/json",
    }

    def guess_type(self, path: str) -> str:
        if path.endswith(".gz"):
            return super().guess_type(path[:-3])

        return super().guess_type(path)

    def end_headers(self) -> None:
        if self.path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")

        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()


def main() -> None:
    parser = argparse.ArgumentParser(description="Serve a Unity WebGL build with correct compressed asset headers.")
    parser.add_argument("--directory", default="unity/HeavySuvPrototype/Builds/WebGL")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8088)
    args = parser.parse_args()

    handler = functools.partial(UnityWebGlHandler, directory=args.directory)
    server = ThreadingHTTPServer((args.host, args.port), handler)
    print(f"Serving Unity WebGL build at http://{args.host}:{args.port}/ from {args.directory}")
    server.serve_forever()


if __name__ == "__main__":
    main()
