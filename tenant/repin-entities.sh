#!/usr/bin/env bash
# Pin Main.xaml to the PurchaseOrders entity of the CURRENT tenant.
#
#   ./repin-entities.sh            install the entity binding (uip rpa data-fabric-entities) and patch the ids in Main.xaml
#   ./repin-entities.sh --dry-run  show what would change
#
# Why: the Data Service activities carry the entity id and the field ids as literals (EntityId=…, DynamicEntityField Id=…),
# and every tenant has its own. Run it from under the course's builds dir (~/Desktop/Builds/coding-agents) so the
# folder-local login is the tenant whose entity you mean; tenant/create-entities.sh must have run there first.
set -euo pipefail
DRY=false; [[ "${1:-}" == "--dry-run" ]] && DRY=true
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"; PROJ="$HERE/../RetrieveInvoiceDocument"; XAML="$PROJ/Main.xaml"
TENANT=$(uip login status --output json 2>/dev/null | python3 -c "import json,sys; raw=sys.stdin.read(); print(json.JSONDecoder(strict=False).decode(raw[raw.find('{'):])['Data'].get('Tenant',''))")
echo "==> tenant $TENANT"
$DRY || uip rpa data-fabric-entities install --add PurchaseOrders --project-dir "$PROJ" --namespace RetrieveInvoiceDocument --output json >/dev/null
read -r EID POID PODATA POHTML < <(python3 - "$PROJ/.entities/EntitiesStore.json" <<'PY'
import json,sys
d=json.load(open(sys.argv[1])); ej=d['EntitiesJson']; ej=json.loads(ej) if isinstance(ej,str) else ej
def find(o):
    if isinstance(o,dict):
        if o.get('Name')=='PurchaseOrders' and 'Fields' in o: return o
        for v in o.values():
            r=find(v)
            if r: return r
    elif isinstance(o,list):
        for i in o:
            r=find(i)
            if r: return r
e=find(ej) or sys.exit("PurchaseOrders not in the entities store — run tenant/create-entities.sh and install first")
f={x['Name']:x['Id'] for x in e['Fields']}
print(e['Id'], f['POID'], f['PODATA'], f['POHTML'])
PY
)
CUR=$(grep -oE 'EntityId="[^"]*"' "$XAML" | head -1 | cut -d'"' -f2)
echo "    Main.xaml pins entity $CUR; tenant has $EID (POID $POID, PODATA $PODATA, POHTML $POHTML)"
if $DRY; then echo "dry run — nothing changed"; exit 0; fi
python3 - "$XAML" "$EID" "$POID" "$PODATA" <<'PY'
import re,sys
p,eid,poid,podata=sys.argv[1:]; s=open(p).read()
s=re.sub(r'EntityId="[^"]*"', f'EntityId="{eid}"', s)
s=re.sub(r'(<udam:DynamicEntityField Id=")[^"]*(" IsRequired="[^"]*" Name="POID")', rf'\g<1>{poid}\g<2>', s)
s=re.sub(r'(<udam:DynamicEntityField Id=")[^"]*(" IsRequired="[^"]*" Name="PODATA")', rf'\g<1>{podata}\g<2>', s)
open(p,'w').write(s)
PY
echo "    ✓ patched $XAML"
