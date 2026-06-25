#!/usr/bin/env bash
# Packages Verdict as a portable, UNSIGNED distributable zip.
# The app is portable by design (one folder), so "install" = copy + shortcut + Win+R launcher.
# Usage:  bash tools/package-release.sh   (run from the repo root)
set -euo pipefail

VER="1.0"   # keep in sync with src/WPEP.Core/AppVersion.cs  (AppVersion.Current)
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
STAGE="$ROOT/dist/Verdict-$VER"
APP="$ROOT/artifacts/app"

echo "== Publishing Release (App + Tray + CLI) =="
( cd "$ROOT" && taskkill //F //IM WPEP.exe //IM wpep-tray.exe 2>/dev/null || true )
dotnet publish "$ROOT/src/WPEP.Cli/WPEP.Cli.csproj" -c Release -o "$ROOT/artifacts"     --disable-build-servers -v q
dotnet publish "$ROOT/src/WPEP.App/WPEP.App.csproj" -c Release -o "$APP"                --disable-build-servers -v q
dotnet publish "$ROOT/src/WPEP.Tray/WPEP.Tray.csproj" -c Release -o "$APP"              --disable-build-servers -v q

echo "== Assembling $STAGE =="
rm -rf "$STAGE"
mkdir -p "$STAGE/app"
# Copy the published app, minus throwaway runtime data.
tar --exclude=runs --exclude=reports --exclude=data -cf - -C "$APP" . | tar -xf - -C "$STAGE/app"

cat > "$STAGE/Installa.cmd" <<'CMD'
@echo off
setlocal
set "DEST=%LOCALAPPDATA%\Verdict"
echo Installazione di Verdict in "%DEST%" ...
robocopy "%~dp0app" "%DEST%" /E /NFL /NDL /NJH /NJS >nul
rem Win+R "verdict" (App Paths, per-utente, nessun admin)
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\verdict.exe" /ve /d "%DEST%\WPEP.exe" /f >nul 2>&1
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\verdict.exe" /v Path /d "%DEST%" /f >nul 2>&1
rem Collegamento sul Desktop
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\Verdict.lnk'); $s.TargetPath='%DEST%\WPEP.exe'; $s.WorkingDirectory='%DEST%'; $s.Save()" >nul 2>&1
echo.
echo Fatto. Apri Verdict con Win+R -^> "verdict", o dal collegamento sul Desktop.
echo (Windows potrebbe avvisare "editore sconosciuto": e' normale per un'app non firmata —
echo  Ulteriori info -^> Esegui comunque.)
pause
CMD

cat > "$STAGE/Disinstalla.cmd" <<'CMD'
@echo off
setlocal
set "DEST=%LOCALAPPDATA%\Verdict"
echo Rimozione di Verdict...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\App Paths\verdict.exe" /f >nul 2>&1
del "%USERPROFILE%\Desktop\Verdict.lnk" >nul 2>&1
rmdir /S /Q "%DEST%" >nul 2>&1
echo Fatto. Verdict e' stato rimosso (i dati nella cartella inclusi).
pause
CMD

cat > "$STAGE/LEGGIMI.txt" <<TXT
VERDICT v$VER — versione portatile

COME INSTALLARE
  1) Estrai questa cartella dove vuoi.
  2) Doppio clic su "Installa.cmd".
     -> Copia Verdict in %LOCALAPPDATA%\Verdict, crea il collegamento sul Desktop
        e il launcher Win+R "verdict".
  3) Apri con Win+R -> "verdict" oppure dal collegamento sul Desktop.

NOTA SMARTSCREEN
  Verdict non e' firmato digitalmente, quindi Windows puo' mostrare "editore
  sconosciuto" al primo avvio. E' normale: clicca "Ulteriori info" -> "Esegui comunque".

PORTATILE
  Tutto sta in una cartella. "Disinstalla.cmd" rimuove app, collegamento e launcher.
  Nessun servizio, nessun installer di sistema.
TXT

echo "== Zipping =="
# GNU tar can't write real .zip; Windows' bundled bsdtar (tar.exe) can.
WINTAR="${SYSTEMROOT:-/c/Windows}/System32/tar.exe"
[ -x "$WINTAR" ] || WINTAR="/c/Windows/System32/tar.exe"
rm -f "$ROOT/dist/Verdict-$VER.zip"
( cd "$ROOT/dist" && "$WINTAR" -a -c -f "Verdict-$VER.zip" "Verdict-$VER" )
echo "== DONE: dist/Verdict-$VER.zip =="
du -sh "$ROOT/dist/Verdict-$VER.zip" 2>/dev/null || true
