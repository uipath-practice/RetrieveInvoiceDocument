#!/usr/bin/env bash
#
# Pack a project as net8 and upload it to the logged-in tenant's feed.
#
#   ./pack.sh [RetrieveInvoiceDocument|ExtractInvoiceData]   pack + upload at the project.json version (default: the generator)
#   ./pack.sh --dry-run [project]                            pack only, report the target framework, upload nothing
#
# Run it from under the course's builds dir (~/Desktop/Builds/coding-agents) so the upload hits the right tenant, after
# tenant/repin-entities.sh (generator) or tenant/repin-ixp.sh (extraction) pinned Main.xaml to that tenant. Both toolchain
# pins are load-bearing until UiPath 2026.10 LTS: see ../property-claims/TOOLCHAIN.md (same packer, same .NET 8 SDK).
set -euo pipefail
DRY_RUN=false; [[ "${1:-}" == "--dry-run" ]] && { DRY_RUN=true; shift; }
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NAME="${1:-RetrieveInvoiceDocument}"; PROJ="$HERE/$NAME"; [[ -f "$PROJ/project.json" ]] || { echo "no project at $PROJ"; exit 1; }
RPA_TOOL_VERSION="${RPA_TOOL_VERSION:-1.198.4}"
DOTNET8_ROOT="${DOTNET8_ROOT:-$HOME/.dotnet}"
TOOLCHAIN_DIR="${TOOLCHAIN_DIR:-$HOME/.cache/property-claims-packer}"
GREEN=$'\033[32m'; RED=$'\033[31m'; DIM=$'\033[2m'; OFF=$'\033[0m'
die() { printf '\n%serror:%s %s\n' "$RED" "$OFF" "$1" >&2; exit 1; }

[[ -x "$DOTNET8_ROOT/dotnet" ]] || die "no .NET 8 SDK at $DOTNET8_ROOT — see ../property-claims/TOOLCHAIN.md"
if [[ ! -f "$TOOLCHAIN_DIR/node_modules/@uipath/rpa-tool/dist/index.js" ]]; then
  mkdir -p "$TOOLCHAIN_DIR"; ( cd "$TOOLCHAIN_DIR" && npm i --silent "@uipath/rpa-tool@$RPA_TOOL_VERSION" ) || die "could not install @uipath/rpa-tool@$RPA_TOOL_VERSION"
fi
if [[ "$NAME" == "RetrieveInvoiceDocument" ]]; then
  [[ -f "$PROJ/.entities/EntitiesStore.json" ]] || die "no entity binding yet — run tenant/repin-entities.sh from the course builds dir first"
  grep -q 'EntityId="00000000-' "$PROJ/Main.xaml" && die "Main.xaml still carries placeholder entity ids — run tenant/repin-entities.sh"
else
  grep -q 'ProjectId="00000000-0000-0000-0000-00000000c001"' "$PROJ/Main.xaml" && die "Main.xaml still carries the placeholder IXP project id — run tenant/repin-ixp.sh"
fi

V=$(python3 -c "import json;print(json.load(open('$PROJ/project.json'))['projectVersion'])")
OUT=$(mktemp -d); trap 'rm -rf "$OUT"' EXIT
DIST="$HERE/dist"; mkdir -p "$DIST"
printf '  %s %s\n' "$NAME" "$V"
( export DOTNET_ROOT="$DOTNET8_ROOT"; export PATH="$DOTNET8_ROOT:$PATH"
  node "$TOOLCHAIN_DIR/node_modules/@uipath/rpa-tool/dist/index.js" pack "$PROJ" "$OUT" ) > "$OUT/pack.log" 2>&1 || { tail -40 "$OUT/pack.log"; die "pack failed"; }
NUP=$(find "$OUT" -name '*.nupkg' | head -1); [[ -n "$NUP" ]] || die "pack produced no .nupkg"
# The packer drops its own (Windows) copy of UiPath.DataService.Definition.dll beside the entity assembly; the robot gets the
# right one from the activities package, and the stray copy fails to load on Linux (property-claims/plumbing/pack.sh, 2026-08-28).
if [[ "$(unzip -Z1 "$NUP")" == *"lib/net8.0/UiPath.DataService.Definition.dll"* ]]; then
  zip -q -d "$NUP" 'lib/net8.0/UiPath.DataService.Definition.dll' && printf '    %sstripped%s the toolchain copy of UiPath.DataService.Definition.dll\n' "$DIM" "$OFF"
fi
FW=$(unzip -p "$NUP" '*.nuspec' 2>/dev/null | grep -oE 'targetFramework="[^"]*"' | head -1)
[[ "$FW" == *net8.0* ]] || die "$(basename "$NUP") packed as $FW, expected net8.0"
cp "$NUP" "$DIST/"; printf '    packed %s (%s) → dist/\n' "$(basename "$NUP")" "$FW"
if $DRY_RUN; then printf '  %swould upload%s %s\n' "$DIM" "$OFF" "$(basename "$NUP")"; exit 0; fi
RESP=$(uip or packages upload "$NUP" --output json 2>&1 || true)
if grep -q '"Result": *"Success"' <<<"$RESP"; then printf '  %s✓%s uploaded %s\n' "$GREEN" "$OFF" "$(basename "$NUP")"
elif grep -q '409' <<<"$RESP"; then printf '  %s·%s %s already in the feed — bump projectVersion to ship a change\n' "$DIM" "$OFF" "$(basename "$NUP")"
else die "upload failed: $(head -c 300 <<<"$RESP")"; fi
printf '\n%sNext:%s tenant.py provision deploys it in Tools (passport feed.packages).\n' "$GREEN" "$OFF"
