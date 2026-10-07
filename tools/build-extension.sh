#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
bash tools/prepare-defaults.sh
bundle="dist/Startup Animation.app"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources/web/assets" "$bundle/Contents/Resources/web/extension" .build/module-cache
swiftc -O -module-cache-path .build/module-cache Native/Extension.swift -o "$bundle/Contents/MacOS/CodexStartup" -framework AppKit
cp index.html style.css animation.js image-settings.js "$bundle/Contents/Resources/web/"
cp assets/artwork.jpg assets/avatar.jpg assets/contours.js assets/config.js "$bundle/Contents/Resources/web/assets/"
rm -f "$bundle/Contents/Resources/web/assets/aemeath.png" "$bundle/Contents/Resources/web/assets/avatar.png"
cp extension/*.mjs extension/wallpaper.css "$bundle/Contents/Resources/web/extension/"
cat > "$bundle/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>CodexStartup</string>
<key>CFBundleIdentifier</key><string>local.aemeath.extension</string>
<key>CFBundleName</key><string>Startup Animation</string>
<key>CFBundleDisplayName</key><string>Startup Animation</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>0.3.5</string>
<key>CFBundleVersion</key><string>10</string>
<key>LSMinimumSystemVersion</key><string>13.0</string>
<key>LSUIElement</key><true/>
</dict></plist>
PLIST
codesign --force --sign - "$bundle"
printf 'Built: %s\n' "$PWD/$bundle"
