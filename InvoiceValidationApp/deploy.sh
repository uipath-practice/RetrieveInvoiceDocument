#!/usr/bin/env bash
# Pack, publish (Action) and deploy the Invoice Validation app into the course tenant's Tools folder.
#   ./deploy.sh <version> [builds-dir] [folder-key]
# The uip login is folder-local under the course's builds dir, so the build is staged there and every uip command runs from it.
set -euo pipefail
VERSION="${1:?usage: deploy.sh <version> [builds-dir] [folder-key]}"
BUILDS="${2:-$HOME/Desktop/Builds/coding-agents}"
FOLDER_KEY="${3:-ba4c9a24-636a-475f-a499-52fb7da022f0}"   # Tools in CodingAgentsCourse
HERE="$(cd "$(dirname "$0")" && pwd)"
STAGE="$BUILDS/invoice-validation-app"

[ -d "$HERE/dist" ] || { echo "dist/ missing — run npm run build first" >&2; exit 1; }
mkdir -p "$STAGE" && rm -rf "$STAGE/dist" && cp -R "$HERE/dist" "$STAGE/dist" && cp "$HERE/uipath.json" "$HERE/action-schema.json" "$STAGE/"
cd "$STAGE"
uip login status --output json | grep -q '"Status": "Logged in"' || { echo "not logged in under $BUILDS" >&2; exit 1; }
uip codedapp pack dist -n invoice-validation --display-name "Invoice Validation" --version "$VERSION" --author "WTFactory" \
  --description "Invoice Matching exercise: the reviewer compares the invoice with its purchase order, edits the supplier e-mail and approves or rejects." \
  --repository-url https://github.com/uipath-practice/RetrieveInvoiceDocument --output json | grep -E '"Result"|Output'
uip codedapp publish -n invoice-validation --version "$VERSION" -t Action --tenant-feed --output json | grep -E '"Result"|Deploy Version'
UIPATH_FOLDER_KEY="$FOLDER_KEY" uip codedapp deploy -n invoice-validation --display-name "Invoice Validation" --output json | grep -E '"Result"|Version'
grep -E 'appVersion|deployedAt' .uipath/app.config.json
