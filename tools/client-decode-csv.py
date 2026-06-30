#!/usr/bin/env python3
"""Export selected FFXIV 1.x client sheets to FFXIVTool-style CSV.

This is a local reverse-engineering helper. It reads user-owned 1.23b client
data files and writes decoded CSVs under Data/client_exports by default.
Generated CSVs are client-derived data and must stay uncommitted.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO, Iterable


DEFAULT_OUTPUT = Path("Data/client_exports/ffxivtool/decode_csv")
ECHO_GATE_PROFILE = Path.home() / "Library/Application Support/Demi Dev Unit/Echo Gate/profile.json"


KNOWN_SHEETS = {
    "actorclass": {"file_id": 0x01030008, "output": "actorclass.csv"},
    "actorclass_graphic": {"file_id": 0x01030042, "output": "actorclass_graphic.csv"},
    "xtx_displayName": {"file_id": 0x0B450000, "output": "xtx_displayName.csv", "language": "en"},
}


@dataclass
class Parameter:
    order: int
    index: int
    data_type: str


@dataclass
class FileBlock:
    begin: int
    count: int
    enable_file_id: int
    offset_file_id: int
    file_id: int


@dataclass
class Sheet:
    name: str
    language: str
    column_max: int
    column_count: int
    info_file: int
    parameters: list[Parameter]
    file_blocks: list[FileBlock]


def client_dir_from_profile() -> Path | None:
    if not ECHO_GATE_PROFILE.is_file():
        return None
    try:
        with ECHO_GATE_PROFILE.open("r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, json.JSONDecodeError):
        return None

    value = data.get("ClientRootPath", "")
    if not value:
        return None
    path = Path(value).expanduser()
    return path if path.is_dir() else None


def resolve_client_dir(cli_value: str | None) -> Path:
    candidates: list[Path] = []
    if cli_value:
        candidates.append(Path(cli_value).expanduser())

    profile_path = client_dir_from_profile()
    if profile_path is not None:
        candidates.append(profile_path)

    candidates.extend(
        [
            Path.home() / ".wine/drive_c/Program Files (x86)/SquareEnix/FINAL FANTASY XIV",
            Path.home() / ".wine/drive_c/Program Files/SquareEnix/FINAL FANTASY XIV",
            Path.home() / "Desktop/FINAL FANTASY XIV",
        ]
    )

    for candidate in candidates:
        if (candidate / "ffxivgame.exe").is_file() and (candidate / "data").is_dir():
            return candidate

    tried = "\n  ".join(str(path) for path in candidates)
    raise SystemExit(f"Could not locate a FFXIV 1.x client directory. Tried:\n  {tried}")


def data_file_id_to_relative_path(file_id: int) -> Path:
    return Path(
        "data",
        f"{(file_id >> 24) & 0xFF:02X}",
        f"{(file_id >> 16) & 0xFF:02X}",
        f"{(file_id >> 8) & 0xFF:02X}",
        f"{file_id & 0xFF:02X}.DAT",
    )


def read_data_file(client_dir: Path, file_id: int) -> bytes:
    path = client_dir / data_file_id_to_relative_path(file_id)
    if not path.is_file():
        raise FileNotFoundError(f"Missing data file {file_id} at {path}")
    return path.read_bytes()


def _xor_u16_lane(buffer: bytearray, start: int, value: int) -> None:
    pos = start
    while pos + 1 < len(buffer):
        word = buffer[pos] | (buffer[pos + 1] << 8)
        word ^= value
        buffer[pos] = word & 0xFF
        buffer[pos + 1] = (word >> 8) & 0xFF
        pos += 4
    if pos < len(buffer):
        buffer[pos] ^= value & 0xFF


def decode_sheet_descriptor(data: bytes) -> bytes:
    if not data or data[-1] != 0xF1:
        return data

    decoded = bytearray(data[:-1])
    left = 0
    right = len(decoded) - 1
    while left < right:
        decoded[left], decoded[right] = decoded[right], decoded[left]
        left += 2
        right -= 2

    encoded_len = len(decoded)
    lane_a = (7 * encoded_len) & 0xFFFF
    lane_b = ((decoded[6] | (decoded[7] << 8)) ^ 0x6C6D) & 0xFFFF
    _xor_u16_lane(decoded, 0, lane_a)
    _xor_u16_lane(decoded, 2, lane_b)
    return bytes(decoded)


def parse_descriptor(client_dir: Path, file_id: int) -> list[Sheet]:
    data = decode_sheet_descriptor(read_data_file(client_dir, file_id))
    text = data.decode("utf-8-sig")
    sheets: list[Sheet] = []

    for sheet_match in re.finditer(r"<sheet\b([^>]*)>(.*?)</sheet>", text, re.DOTALL):
        attrs = parse_attrs(sheet_match.group(1))
        body = sheet_match.group(2)
        info_file = int(attrs.get("infofile", "0"))
        parameters: list[Parameter] = []
        blocks: list[FileBlock] = []

        if info_file == 0:
            type_body = first_tag_body(body, "type")
            index_body = first_tag_body(body, "index")
            type_params = re.findall(r"<param>(.*?)</param>", type_body, re.DOTALL)
            index_params = [int(value.strip() or "0") for value in re.findall(r"<param>(.*?)</param>", index_body, re.DOTALL)]
            for idx, data_type in enumerate(type_params):
                parameters.append(Parameter(order=idx + 1, index=index_params[idx], data_type=data_type.strip()))

            for file_match in re.finditer(r"<file\b([^>]*)>(.*?)</file>", body, re.DOTALL):
                file_attrs = parse_attrs(file_match.group(1))
                blocks.append(
                    FileBlock(
                        begin=int(file_attrs.get("begin", "0")),
                        count=int(file_attrs.get("count", "0")),
                        enable_file_id=int(file_attrs.get("enable", "0")),
                        offset_file_id=int(file_attrs.get("offset", "0")),
                        file_id=int(file_match.group(2).strip() or "0"),
                    )
                )

        sheets.append(
            Sheet(
                name=attrs.get("name", ""),
                language=attrs.get("lang", ""),
                column_max=int(attrs.get("column_max", "0")),
                column_count=int(attrs.get("column_count", "0")),
                info_file=info_file,
                parameters=parameters,
                file_blocks=blocks,
            )
        )

    return sheets


def parse_attrs(text: str) -> dict[str, str]:
    return {match.group(1): match.group(2) for match in re.finditer(r'([A-Za-z_][A-Za-z0-9_]*)="([^"]*)"', text)}


def first_tag_body(text: str, tag: str) -> str:
    match = re.search(rf"<{tag}\b[^>]*>(.*?)</{tag}>", text, re.DOTALL)
    return match.group(1) if match else ""


def read_string(handle: BinaryIO) -> str:
    raw_len = handle.read(2)
    if len(raw_len) != 2:
        raise EOFError("Unexpected end of string length")
    strlen = struct.unpack("<h", raw_len)[0]
    marker = handle.read(1)
    if len(marker) != 1:
        raise EOFError("Unexpected end of string marker")
    payload = bytearray(handle.read(max(strlen - 1, 0)))
    for idx, value in enumerate(payload):
        payload[idx] = value ^ 0x73
    return payload.decode("utf-8", errors="replace").rstrip("\x00")


def read_value(handle: BinaryIO, data_type: str):
    if data_type == "str":
        return read_string(handle)
    if data_type == "s8":
        return struct.unpack("<b", handle.read(1))[0]
    if data_type == "u8":
        return struct.unpack("<B", handle.read(1))[0]
    if data_type == "s16":
        return struct.unpack("<h", handle.read(2))[0]
    if data_type == "u16":
        return struct.unpack("<H", handle.read(2))[0]
    if data_type == "s32":
        return struct.unpack("<i", handle.read(4))[0]
    if data_type == "u32":
        return struct.unpack("<I", handle.read(4))[0]
    if data_type == "float":
        return struct.unpack("<f", handle.read(4))[0]
    if data_type == "f16":
        return struct.unpack("<e", handle.read(2))[0]
    if data_type == "bool":
        return struct.unpack("<B", handle.read(1))[0] != 0
    raise ValueError(f"Unsupported sheet data type: {data_type}")


def iter_enable_ranges(data: bytes) -> Iterable[tuple[int, int]]:
    if len(data) % 8 != 0:
        raise ValueError(f"Enable block length {len(data)} is not divisible by 8")
    for offset in range(0, len(data), 8):
        yield struct.unpack_from("<ii", data, offset)


def load_sheet_rows(client_dir: Path, sheet: Sheet) -> list[list[object]]:
    if sheet.info_file != 0:
        raise ValueError(f"Sheet {sheet.name} is a reference sheet and cannot be exported directly")

    width = max(sheet.column_max, *(param.index + 1 for param in sheet.parameters), 0)
    rows: list[list[object]] = []

    for block in sheet.file_blocks:
        enable_data = read_data_file(client_dir, block.enable_file_id)
        data_path = client_dir / data_file_id_to_relative_path(block.file_id)
        with data_path.open("rb") as data_handle:
            for start_id, count in iter_enable_ranges(enable_data):
                for idx in range(count):
                    row: list[object] = [""] * (width + 1)
                    row[0] = start_id + idx
                    for param in sheet.parameters:
                        row[param.index + 1] = read_value(data_handle, param.data_type)
                    rows.append(row)

    return rows


def select_sheet(sheets: list[Sheet], language: str | None) -> Sheet:
    if language:
        for sheet in sheets:
            if sheet.language == language:
                return sheet
        raise ValueError(f"Could not find language '{language}' in descriptor")

    direct = [sheet for sheet in sheets if sheet.info_file == 0]
    if len(direct) != 1:
        raise ValueError(f"Expected one direct sheet, found {len(direct)}")
    return direct[0]


def export_sheet(client_dir: Path, sheet_key: str, output_dir: Path) -> dict[str, object]:
    spec = KNOWN_SHEETS[sheet_key]
    file_id = int(spec["file_id"])
    sheets = parse_descriptor(client_dir, file_id)
    sheet = select_sheet(sheets, spec.get("language"))
    rows = load_sheet_rows(client_dir, sheet)

    output_path = output_dir / str(spec["output"])
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerows(rows)

    return {
        "sheet": sheet.name,
        "language": sheet.language,
        "file_id": file_id,
        "descriptor": str(data_file_id_to_relative_path(file_id)),
        "rows": len(rows),
        "columns": len(rows[0]) if rows else 0,
        "output": str(output_path),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Export selected FFXIV 1.x client sheets to CSV.")
    parser.add_argument("--client-dir", help="Path to the FINAL FANTASY XIV 1.x client root.")
    parser.add_argument("--output-dir", default=str(DEFAULT_OUTPUT), help="Output directory for decoded CSVs.")
    parser.add_argument(
        "--sheet",
        action="append",
        choices=sorted(KNOWN_SHEETS.keys()),
        help="Sheet to export. May be passed multiple times. Defaults to actorclass and actorclass_graphic.",
    )
    parser.add_argument("--include-display-names", action="store_true", help="Also export English xtx_displayName.csv.")
    args = parser.parse_args()

    client_dir = resolve_client_dir(args.client_dir)
    output_dir = Path(args.output_dir)
    sheet_keys = args.sheet or ["actorclass", "actorclass_graphic"]
    if args.include_display_names and "xtx_displayName" not in sheet_keys:
        sheet_keys.append("xtx_displayName")

    manifest = {
        "client_dir": str(client_dir),
        "game_ver": (client_dir / "game.ver").read_text(encoding="utf-8", errors="replace").strip()
        if (client_dir / "game.ver").is_file()
        else "",
        "exports": [],
    }

    for sheet_key in sheet_keys:
        result = export_sheet(client_dir, sheet_key, output_dir)
        manifest["exports"].append(result)
        print(f"exported {result['sheet']} rows={result['rows']} cols={result['columns']} -> {result['output']}")

    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"wrote manifest -> {manifest_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
