#!/usr/bin/env bash
# Create the Invoice Matching exercise's two Data Fabric entities in the logged-in tenant (idempotent: skips the ones that exist).
# Run it from under the course's builds dir (~/Desktop/Builds/coding-agents) so the folder-local login hits CodingAgentsCourse;
# tenant.py provision calls it as data_fabric.entities[].managed_by.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
have=$(uip df entities list --output json 2>/dev/null | python3 -c "import json,sys; d=json.load(sys.stdin)['Data']; d=d.get('value',d) if isinstance(d,dict) else d; print(' '.join(e.get('Name','') for e in d))")
for name in PurchaseOrders PaymentsQueue; do
  if [[ " $have " == *" $name "* ]]; then echo "  · $name exists"; continue; fi
  out=$(uip df entities create "$name" --file "$HERE/entities/$name.json" --output json 2>&1) || true
  if grep -q '"Result": *"Success"' <<<"$out"; then echo "  ✓ created $name"; else echo "  ! $name: $(head -c 600 <<<"$out")"; exit 1; fi
done
