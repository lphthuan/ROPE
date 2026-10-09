"""Small MCP client for diagnostics and repeatable Unity verification.

Run with Library/MCPRuntime/Scripts/python.exe after starting the local server.
"""
import argparse
import asyncio
import base64
import json
import sys
from datetime import timedelta
from pathlib import Path

from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client


async def run(options):
    async with streamablehttp_client(options.url) as (reader, writer, _):
        async with ClientSession(reader, writer, read_timeout_seconds=timedelta(seconds=options.timeout)) as session:
            await session.initialize()
            if options.list_tools:
                result = await session.list_tools()
            elif options.list_resources:
                result = await session.list_resources()
            elif options.resource:
                result = await session.read_resource(options.resource)
            else:
                result = await session.call_tool(options.tool, json.loads(options.arguments))
            data = result.model_dump(mode="json", exclude_none=True)
            structured = data.get("structuredContent", {})
            payload = structured.get("result", structured)
            failed = data.get("isError", False) or payload.get("success") is False
            for index, block in enumerate(data.get("content", [])):
                if block.get("type") == "image":
                    directory = Path(options.output).parent if options.output else Path("Logs/MCP")
                    directory.mkdir(parents=True, exist_ok=True)
                    path = directory / f"{options.tool}-{index}.png"
                    path.write_bytes(base64.b64decode(block.pop("data")))
                    block["path"] = str(path.resolve())
            if options.output:
                path = Path(options.output)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps(data, indent=2), encoding="utf-8")
            if options.quiet:
                print(json.dumps({"saved": options.output, "isError": failed, "error": payload.get("error")}))
            elif options.list_tools or options.list_resources:
                key = "tools" if options.list_tools else "resources"
                print(json.dumps([entry.get("name", entry.get("uri")) for entry in data.get(key, [])]))
            else:
                print(json.dumps(data, ensure_ascii=False))
            if failed:
                raise SystemExit(1)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:8080/mcp")
    parser.add_argument("--timeout", type=float, default=60)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument("--list-tools", action="store_true")
    action.add_argument("--list-resources", action="store_true")
    action.add_argument("--resource")
    action.add_argument("--tool")
    parser.add_argument("--arguments", default="{}")
    parser.add_argument("--output")
    parser.add_argument("--quiet", action="store_true", help="Save the full response and print only the output path.")
    asyncio.run(run(parser.parse_args()))
