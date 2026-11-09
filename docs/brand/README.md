# Typee logo pack

Concept: lowercase **t** + a blinking text **cursor**. Purple `#7C3AED`, cursor amber `#FDE68A`, white glyph.

## Windows (Typee desktop)
| File | Use |
|---|---|
| `windows/typee.ico` | App icon (16–256 px). Set as `<ApplicationIcon>` in `Typee.App.csproj` and as the window icon |
| `windows/typee-tray-white.ico` | Tray icon on a dark taskbar (default on Windows 11 dark mode) |
| `windows/typee-tray-black.ico` | Tray icon on a light taskbar |
| `windows/typee-tray-paused.ico` | Tray icon when suggestions are paused or the engine is offline |
| `windows/typee-256.png` | Installer, About box, Velopack setup image |

Pick the tray icon at runtime from the Windows theme (`SystemUsesLightTheme` registry value) and switch when it changes.

## Android (Typee Keyboard)
Copy `android/res/` into `app/src/main/res/` (merge folders). It contains:
- Adaptive icon (Android 8+): `mipmap-anydpi-v26/ic_launcher.xml` + `ic_launcher_round.xml`, foreground vector, purple background color
- **Themed icon** (Android 13+): `drawable/ic_launcher_monochrome.xml`
- Legacy PNGs for older devices: `mipmap-mdpi` … `mipmap-xxxhdpi`

In `AndroidManifest.xml`: `android:icon="@mipmap/ic_launcher"` and `android:roundIcon="@mipmap/ic_launcher_round"`.

`android/playstore/typee-playstore-512.png` — Google Play listing icon (512×512, Play adds its own rounded mask).

## GitHub
- `github/github-social-1280x640.png` — repo **Settings → Social preview** (light). Dark version also included.
- `github/readme-header-1600x400.png` — put at the top of `README.md`.

## Master files
`svg/` holds the editable vectors. Use `*-small.svg` (thicker strokes, wider gap) for anything 32 px or smaller.

Do not combine the mark with the Android robot or the Windows logo — those are Google and Microsoft trademarks.
