#!/usr/bin/env bash
# Pin ExtractInvoiceData/Main.xaml to the IXP project of the CURRENT tenant (id, model name, the version behind the `live` tag).
#
#   ./repin-ixp.sh --dry-run   show the project, model and version it would pin
#   ./repin-ixp.sh             patch Main.xaml
#
# Why: the Extract Document Data activity carries ProjectId, ProjectVersionNumber and the extractor (model) name as literals,
# and every tenant has its own. Run from under the course's builds dir (~/Desktop/Builds/coding-agents).
set -euo pipefail
DRY=false; [[ "${1:-}" == "--dry-run" ]] && DRY=true
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"; XAML="$HERE/../ExtractInvoiceData/Main.xaml"
TITLE="${IXP_TITLE:-InvoicesIXP}"
jd() { python3 -c "import json,sys; raw=sys.stdin.read(); d=json.JSONDecoder(strict=False).decode(raw[raw.find('{'):]); $1"; }
TENANT=$(uip login status --output json 2>/dev/null | jd "print(d['Data'].get('Tenant',''))")
read -r PID PNAME < <(uip ixp projects list --output json 2>/dev/null | jd "
for p in d['Data'].get('Projects',[]):
    if (p.get('Title') or '')=='$TITLE': print(p['Id'], p['Name']); break") || true
[[ -n "${PID:-}" ]] || { echo "no IXP project titled '$TITLE' on $TENANT"; exit 1; }
read -r LIVE MODEL < <(uip ixp projects list-models "$PNAME" --output json 2>/dev/null | jd "
tags={t['Name']:t['Version'] for t in d['Data'].get('Tags',[])}; v=tags.get('live')
m=next((x['ModelName'] for x in d['Data'].get('Models',[]) if v is not None and x['Version']==v), '')
print(v if v is not None else '', m)") || true
[[ -n "${LIVE:-}" ]] || { echo "project $PNAME has no 'live' tag — publish a version first: uip ixp projects publish $PNAME <version> --tag live"; exit 1; }
CUR=$(grep -oE 'ProjectId="[^"]*"' "$XAML" | head -1 | cut -d'"' -f2)
echo "==> $TENANT · $PNAME ($PID) · live = version $LIVE, model $MODEL · Main.xaml pins $CUR"
$DRY && { echo "dry run — nothing changed"; exit 0; }
python3 - "$XAML" "$PID" "$LIVE" "$MODEL" "$TITLE" <<'PY'
import re,sys
p,pid,ver,model,title=sys.argv[1:]; s=open(p).read()
s=re.sub(r'ProjectId="[^"]*"', f'ProjectId="{pid}"', s)
s=re.sub(r'ProjectVersionNumber="[^"]*"', f'ProjectVersionNumber="{ver}"', s)
s=re.sub(r'ProjectName="[^"]*"', f'ProjectName="{title}"', s)
s=re.sub(r'DocType="[^"]*"', f'DocType="{model}"', s)
s=re.sub(r'ExtractorName="[^"]*"', f'ExtractorName="{model}"', s)
open(p,'w').write(s)
PY
echo "    ✓ patched $XAML"
