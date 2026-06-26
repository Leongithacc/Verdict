#!/usr/bin/env bash
# Ship Verdict su GitHub in UN comando: crea repo PUBLIC, push, accende Pages, pubblica release v1.0.
# Richiede GitHub CLI autenticato:  gh auth login   (https://cli.github.com)
# Idempotente dove possibile: se repo/release esistono già, non rifa.
set -euo pipefail

OWNER="Leongithacc"
REPO="Verdict"
VER="1.0"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

command -v gh >/dev/null || {
  echo "Serve GitHub CLI: installa da https://cli.github.com poi 'gh auth login'."
  exit 1
}

echo "== 1) Repo $OWNER/$REPO (public) + push =="
if gh repo view "$OWNER/$REPO" >/dev/null 2>&1; then
  git remote get-url origin >/dev/null 2>&1 || git remote add origin "https://github.com/$OWNER/$REPO.git"
  git push -u origin HEAD:main
else
  gh repo create "$OWNER/$REPO" --public --source=. --remote=origin --push
fi

echo "== 2) GitHub Pages (Source: GitHub Actions, pubblica site/) =="
gh api -X POST "repos/$OWNER/$REPO/pages" -f build_type=workflow >/dev/null 2>&1 \
  || gh api -X PUT "repos/$OWNER/$REPO/pages" -f build_type=workflow >/dev/null 2>&1 \
  || echo "  (Pages forse già attivo, o attivalo a mano: Settings → Pages → Source: GitHub Actions)"
echo "  → https://$OWNER.github.io/$REPO/"

echo "== 3) Release v$VER + zip portatile =="
bash tools/package-release.sh
if ! gh release view "v$VER" -R "$OWNER/$REPO" >/dev/null 2>&1; then
  gh release create "v$VER" "dist/Verdict-$VER.zip" -R "$OWNER/$REPO" \
    -t "Verdict v$VER" -n "Prima release portatile di Verdict. Estrai e lancia Installa.cmd."
else
  echo "  (release v$VER già presente)"
fi

echo ""
echo "== FATTO =="
echo "Auto-update:  wpep update-check   →  deve dire 'Sei aggiornato (v$VER)'"
echo "QR BIOS:      https://$OWNER.github.io/$REPO/bios.html?t=xmp-expo-enable&v=asus"
