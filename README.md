# Universal Dreamcast Patcher
<img align="right" width="320" src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/screenshot.png?raw=true">Universal Dreamcast Patcher is a cross-platform toolkit for Dreamcast disc image operations. A single unified GUI combines four workflows: applying patches, building patches, editing `IP.BIN`, and converting between disc image formats. Patches typically come in the form of English translations and other ROM hacks.

The application accepts any TOSEC-style GDI, Redump-style CUE/BIN, or compressed CHD as its source. Every operation is fully deterministic: the same inputs always produce byte-identical output, so any two users running the same operation end up with results that match checksum-for-checksum.

## Table of Contents
1. [Latest Version](https://github.com/DerekPascarella/UniversalDreamcastPatcher#latest-version)
2. [Changelog](https://github.com/DerekPascarella/UniversalDreamcastPatcher#changelog)
3. [Current Features](https://github.com/DerekPascarella/UniversalDreamcastPatcher#current-features)
4. [Roadmap](https://github.com/DerekPascarella/UniversalDreamcastPatcher#roadmap)
5. [Applying Patches](https://github.com/DerekPascarella/UniversalDreamcastPatcher#applying-patches)
6. [Building Patches](https://github.com/DerekPascarella/UniversalDreamcastPatcher#building-patches)
   - [Automatic Method](https://github.com/DerekPascarella/UniversalDreamcastPatcher#automatic-method)
   - [Manual Method](https://github.com/DerekPascarella/UniversalDreamcastPatcher#manual-method)
7. [Editing IP.BIN](https://github.com/DerekPascarella/UniversalDreamcastPatcher#editing-ipbin)
8. [Converting Disc Images](https://github.com/DerekPascarella/UniversalDreamcastPatcher#converting-disc-images)
   - [Single Mode](https://github.com/DerekPascarella/UniversalDreamcastPatcher#single-mode)
   - [Batch Mode](https://github.com/DerekPascarella/UniversalDreamcastPatcher#batch-mode)
   - [Using External DAT Files](https://github.com/DerekPascarella/UniversalDreamcastPatcher#using-external-dat-files)
9. [Legal and Licensing](https://github.com/DerekPascarella/UniversalDreamcastPatcher#legal-and-licensing)

## Latest Version
The latest version of Universal Dreamcast Patcher is [2.1.1](https://github.com/DerekPascarella/UniversalDreamcastPatcher/releases/tag/2.1.1).

## Changelog
- **Version 2.1.1** (2026-05-17)
  - New batch mode added to "Converter" tab (see [Issue 12](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/12)).
  - External DATs can now be used for disc image convert operations (see [Issue 13](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/13)).
  - "Build Patch" tab's IP.BIN modifications section redesigned (see [Issue 14](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/14)).
- **Version 2.1.0** (2026-05-15)
  - Added tooltip text to all selectable UI elements (see [Issue 7](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/7)).
  - Patched output disc image now supports writing CUE/BIN and CHD (see [Issue 8](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/8)).
  - New "IP.BIN Editor" tab added (see [Issue 9](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/9)).
  - New "Converter" tab added (see [Issue 10](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/10)).
  - Dialog box titles modified for Information/Confirmation/Error consistency (see [Issue 11](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/11)).
- **Version 2.0.2** (2026-05-08)
  - In macOS, fixed System Bar's "About <Application Name>" string (see [Issue 5](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/5)).
  - Fixed potentially destructive behavior in Windows/Linux auto-update (see [Issue 6](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/6)).
- **Version 2.0.1** (2026-05-05)
  - Fixed potential auto-updater breakage in the future due to build pipeline version mismatch (see [Issue 4](https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/4)).
- **Version 2.0.0** (2026-05-04)
  - Complete rewrite, combining the patcher and patch builder into a single cross-platform application (Windows, Linux, macOS).
  - Removed all dependencies on external helper utilities, natively implementing all disc-image extract, rebuild, and binary diffing operations.
  - Added support for CHD as a source disc image format (automatically decompressed and, if necessary, converted to GDI from CUE/BIN).
  - Added built-in auto-update on Windows and Linux.
  - Patched disc images and DCP files are now byte-identical across platforms for the same inputs, finally delivering reliable checksums.
- **Version 1.8** (2024-11-08)
  - Fixed bug in both patcher and patch builder when processing GDIs with over 90 CDDA tracks.
- **Version 1.7** (2024-11-08)
  - For both the patcher and patch builder, added support for GDI source disc images that use 2048-byte-per-sector ISO data tracks instead of standard 2352-byte-per-sector BIN data tracks.
- **Version 1.6** (2023-07-17)
  - Fixed bug in processing Redump-style CUE/BIN disc images with more than 10 tracks (<a href="https://github.com/DerekPascarella/UniversalDreamcastPatcher/issues/2">Issue #2</a>).
  - Enhanced integrity checking of patched GDI output.
- **Version 1.5** (2023-05-13)
  - Eliminated IP.BIN-patching helper tools in patch-building utility, with code now directly implemented in the application itself.
  - Changed some labels in the patch-building utility for clarity ("Unpatched GDI" is now "Original GDI" and "Patched GDI" is now "Modified GDI").
- **Version 1.4** (2022-11-24)
  - If patch utilizes xdelta, the current filename being patched is now displayed in the progress status message, avoiding the appearance of failure when processing a large quantity of files.
- **Version 1.3** (2022-05-06)
  - Added xdelta support to reduce patch size, as well as eliminate the potential distribution of whole files containing copyrighted material.
  - Introduced separate patch-building utility for developers to automatically produce patch files, analyzing changes between original retail disc image and modified one.
- **Version 1.2** (2022-03-19)
  - Enhanced GDI compatibility and integrity checking.
- **Version 1.1** (2022-03-17)
  - Eliminated GUI lockup that occurred at different stages of the patching process.
- **Version 1.0** (2021-11-26)
  - Fixed bug in LBA calculation used for extracting GDI data tracks.
- **Version 0.9** (2021-11-22)
  - Due to many anti-virus tools erroneously flagging the modified version of gditools from v0.8 as malware, this version uses an alternative method for GDI extraction that still delivers the same level of compatibility, leveraging bin2iso and extract.
- **Version 0.8** (2021-11-19)
  - Now using modified version of gditools (thanks to [mrneo240](https://github.com/mrneo240)) that can extract the dozen-or-so problematic GDIs which previously failed.
- **Version 0.7** (2021-06-01)
  - Fixed bug with temporary folders/files if they're written to a different disk drive than the one from which the application is launched.
- **Version 0.6** (2021-05-26)
  - Changed location of temporary folders/files to use Windows' default location rather than the application's working directory.
  - Updated logo (also, watch what happens when the "Apply Patch" button is clicked).
- **Version 0.5** (2021-05-19)
  - Fixed bug in file path parsing.
- **Version 0.4** (2021-05-18)
  - Fixed bug with incorrect GDI building when source disc image contains CDDA.
- **Version 0.3** (2021-05-17)
  - The "bootsector" folder and its IP.BIN were erroneously being included as a folder and file in the patched GDI's filesystem.
- **Version 0.2** (2021-05-17)
  - Added support for source disc images with CDDA.
  - Fixed bug with source disc image integrity verification.
- **Version 0.1** (2021-05-16)
  - Initial release.

## Current Features
Below is a specific list of Universal Dreamcast Patcher's current features.

- Cross-platform support (Windows, Linux, macOS).
- Unified GUI for applying patches, building patches, editing `IP.BIN`, and converting disc images.
- Source disc image integrity verification.
- Support for source disc images containing CDDA.
- Patches can include a custom `IP.BIN` that replaces the source disc's bootsector on apply.
- xdelta-based patching to reduce patch size and mitigate copyright concerns.
- Deterministic, byte-identical output when applying the a given patch to the same source disc image.
- Built-in auto-update on Windows and Linux (update notifications on macOS).
- Robust `IP.BIN` editor, usable with standalone `IP.BIN` files or disc images (GDI, CUE/BIN, CHD).
- Disc image converter between GDI, CUE/BIN, and CHD, with both single and batch modes.
- Optional support for user-supplied Logiqx XML DATs to override the built-in TOSEC and Redump conversion references.
- Supported disc image formats (input and output):
  - TOSEC-style GDI
  - Redump-style CUE/BIN
  - CHD
- Supported patch formats:
  - DCP

## Roadmap
The list below represents features planned for future versions of Universal Dreamcast Patcher.

* Extend DCP patch format to support modifying CDDA tracks.

## Applying Patches
Universal Dreamcast Patcher is simple to use. After launching the application, follow the steps below.

1. The **Apply Patch** tab is selected by default.
2. Next to **Source disc image**, click "Browse..." and select the .gdi, .cue, or .chd to be patched.
3. Next to **Patch file**, click "Browse..." and select the .dcp patch file.
4. Next to **Output folder**, click "Browse..." and select the folder where the patched disc image should be written.
5. Next to **Patched disc image format**, select the format for the patched disc image (i.e., GDI, CUE/BIN, or CHD).
6. Click **Apply Patch**.

The patched disc image will be written to a subfolder of the output folder, named after the patch file (e.g., a patch file named `My Game (v1.0).dcp` produces a folder named `My Game (v1.0) [GDI]`, `My Game (v1.0) [CUE-BIN]`, or `My Game (v1.0) [CHD]`). If that subfolder already exists, the application appends `[2]`, `[3]`, and so on to avoid overwriting prior results.

Details on the current step of the patching process will be updated as they progress. Any errors encountered during sanity or integrity checks will be presented and the patching process will be halted.

## Building Patches
The DCP patch format was designed specifically for Universal Dreamcast Patcher. Presently, there is both an automatic and a manual method one can use to build a patch.

### Automatic Method
<img width="220" align="right" src="https://raw.githubusercontent.com/DerekPascarella/UniversalDreamcastPatcher/main/screenshots/screenshot_builder.png">Users should use this method when:
* They do not wish to distribute whole files containing copyrighted content.
* Their aim is to keep patch files as small as possible.
* They want all the work done for them!

As of version 2.0.0, Universal Dreamcast Patcher includes a dedicated patch-building flow on its **Build Patch** tab (rather than shipping a separate companion application). This flow produces a DCP patch file based on changes between two disc images (original and modified).

For example, a translation patch developer would supply the original retail disc image along with their modified one. Universal Dreamcast Patcher would analyze the two for changes and then automatically generate the DCP patch file.

Furthermore, the Build Patch flow includes options to apply additional modifications to `IP.BIN`, like enabling VGA mode, setting the game as region-free, or giving the game a custom name label.

The steps for automatically creating a patch are as follows.

1. Select the **Build Patch** tab.
2. Next to **Original disc image**, click "Browse..." and select the unpatched .gdi, .cue, or .chd.
3. Next to **Modified disc image**, click "Browse..." and select the patched .gdi, .cue, or .chd.
4. In the **Patch filename** field, type the desired name for the DCP patch. Note that the base filename of the DCP will be used when the patching application generates the patched disc image (e.g., a patch file named "My Game (v1.0).dcp" will result in a patched GDI folder named "My Game (v1.0) [GDI]").
5. Next to **Output folder**, click "Browse..." and select where the DCP should be written.
6. Optionally, check **Customize IP.BIN** to bundle a custom `IP.BIN` with the patch. In many cases, patch developers won't bother with this step, but there are several advantages in enabling these options:
   * **Region-free** - Patched disc image (output) can be booted on any ODE or emulator, regardless of region setting, and without enabling region-free options within the ODE or emulator itself.
   * **Enable VGA** - If supported, patched disc image (output) can be booted in VGA mode on any ODE or emulator, regardless of VGA auto-patching settings within the ODE or emulator itself.
   * **Use custom game name** - Patched disc image (output) will be displayed using a custom name within tools like the various SD card managers for GDEMU, thus giving another degree of creative control to patch developers opting for a localized game title.
7. Click **Build Patch**.

### Manual Method
Use this method when:
* The distribution of copyrighted content is not a concern.
* Reducing the size of a patch file is not a priority.

The steps for manually creating a patch are as follows.

1. Create a ZIP archive containing (in its root) all of the files/folders from the game's data that have been modified or are new. Be sure to retain original folder structure and hierarchy.
   * If the patch should use a modified `IP.BIN` file, simply create a folder named "bootsector" in the root of the ZIP archive and place `IP.BIN` inside of it. Note that this folder and file will not be included in the patched disc image's (output) filesystem.
2. Change the extension of the file from ZIP to DCP.
   * Note that the base filename of the DCP will be used when this application generates the patched GDI (e.g., a patch file named "My Game (v1.0).dcp" will result in a patched GDI folder named "My Game (v1.0) [GDI]").

#### Example
In this example, an existing DCP patch is seen being opened with 7-Zip, a common archive creator and extractor.

<p align="center">
<img src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/example_1.png?raw=true">
</p>

All modified or new files reside in the root of the patch. Likewise, all folders containing modified or new files exist in the root of the patch just as they did within the game's original disc structure. Furthermore, a "bootsector" folder is seen in this example, along with its content.

<p align="center">
<img src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/example_2.png?raw=true">
</p>

## Editing IP.BIN
<img align="right" width="320" src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/screenshot_ipbin_editor.png?raw=true"> Universal Dreamcast Patcher includes a full `IP.BIN` editor for inspecting and modifying the Dreamcast bootsector. The editor accepts either a disc image (GDI, CUE/BIN, or CHD) or a standalone `IP.BIN` file. When a disc image is loaded, the editor reads `IP.BIN` from the disc and, on save, writes the modified bootsector back into the original disc image in place, preserving the disc's format.

Editable fields are organized across three sub-tabs.

* **Identification** - Product number, version, release date, maker name, game title, and boot filename.
* **Disc / Region** - Media type (GD-ROM or CD-ROM), disc number and total disc count, and region flags (Japan, USA, Europe).
* **Peripherals** - System features (Windows CE, VGA box), optional peripherals (memory card, Puru Puru, microphone, light gun, keyboard, mouse, etc.), and controller minimums (standard pad, analog axes, button minimums).

The steps for editing `IP.BIN` are as follows.

1. Select the **IP.BIN Editor** tab.
2. Next to **Source disc image or IP.BIN**, click "Browse..." and select a `.gdi`, `.cue`, `.chd`, or `IP.BIN` file.
3. Edit fields across the **Identification**, **Disc / Region**, and **Peripherals** sub-tabs.
4. Click **Save Changes**.

If the source is a disc image, the modified `IP.BIN` is written directly back into the original disc image in place, preserving the disc's format. If the source is a standalone `IP.BIN` file, it is overwritten directly.

## Converting Disc Images
<img align="right" width="320" src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/screenshot_converter.png?raw=true"> Universal Dreamcast Patcher includes a built-in disc image converter for cross-format conversions between GDI, CUE/BIN, and CHD without going through the patching pipeline. Both single and batch conversion are supported.

The converter ships with embedded TOSEC and Redump DATs, producing byte-perfect output for any catalogued disc when converting between GDI and CUE/BIN. Discs not present in either DAT (e.g., uncatalogued development builds, patches) still convert correctly, but without the DAT-backed byte-exact guarantee.

All conversions write to a subfolder of the chosen output folder, named after the source disc image (e.g., converting `My Game.gdi` to CUE/BIN produces a folder named `My Game [CUE-BIN]`). If that subfolder already exists, the converter appends `[2]`, `[3]`, and so on to avoid overwriting prior conversions.

### Single Mode
Use this mode to convert a single disc image. The steps are as follows.

1. Select the **Converter** tab.
2. The **Single** sub-tab is selected by default.
3. Next to **Source disc image**, click "Browse..." and select the `.gdi`, `.cue`, or `.chd` to be converted.
4. Next to **Converted disc image output folder**, click "Browse..." and select where the converted disc image should be written.
5. Next to **Converted disc image format**, select the target format (i.e., GDI, CUE/BIN, or CHD).
6. Click **Convert**.

### Batch Mode
Use this mode to convert many disc images in one run. The steps are as follows.

1. Select the **Converter** tab.
2. Select the **Batch** sub-tab.
3. Click **Add files...** to add one or more disc images, or **Add folder...** to recursively add every disc image found within a folder.
4. Optionally select one or more rows and click **Remove**, or click **Clear** to empty the queue entirely.
5. Next to **Converted disc image format**, select the target format for all queued disc images (i.e., GDI, CUE/BIN, or CHD).
6. Next to **Converted disc image output folder**, click "Browse..." and select where the converted disc images should be written.
7. Click **Convert All**.

Each queued disc image is converted in turn and written to its own subfolder of the output folder. The queued list updates in real time, showing the current status (queued, running, done, copied, failed, cancelled, or skipped) for each disc image.

When the batch completes, a summary dialog is shown with totals for converted, copied, failed, and cancelled disc images. If any disc images failed to convert, a separate window then opens listing the full path of each failed disc image alongside the specific error encountered.

### Using External DAT Files
By default, the converter uses its built-in TOSEC and Redump DATs to produce byte-perfect output when converting between GDI and CUE/BIN. If users prefer using their own DAT files (e.g., a newer Redump revision, a TOSEC variant, or a custom catalogue), the **DAT Source** controls above the **Single** and **Batch** sub-tabs let them do so.

1. Above the **Single** and **Batch** sub-tabs, switch **DAT Source** from **Internal** to **External**. The **Manage** button becomes enabled.
2. Click **Manage** to open the **Manage External DATs** window.
3. Click **Add DAT...** to browse for one or more Logiqx XML `.dat` files. Each selected file is parsed and validated. Files that aren't valid Logiqx DATs are rejected.
4. Use **Move Up** and **Move Down** to set the order in which the DATs are consulted (top to bottom).
5. Click **Save** to commit the list, or **Cancel** to discard changes.

The chosen DAT Source and DAT file list are remembered between sessions.

When **External** is selected, conversions consult the listed DATs in order. If a disc isn't found in any of them, the built-in TOSEC and Redump DATs are used as a fallback. If any listed DAT file is no longer present on disk, it appears in the **Manage External DATs** window with a red "Missing" status, and a confirmation dialog is shown at the start of any conversion so users can proceed anyway or cancel and edit the list.

## Legal and Licensing

Universal Dreamcast Patcher is licensed under the GNU General Public License v3.0 (GPL-3.0).

**Copyright 2021-2026, Derek Pascarella (ateam)**

For the full license text, see [LICENSE.txt](LICENSE.txt).

### Third-Party Components
Universal Dreamcast Patcher includes the following third-party code and libraries, each with its own license:

- **DiscUtilsGD** (MIT) - ISO9660 / GD-ROM filesystem reader and builder. Copyright 2008-2011 Kenneth Bell, 2014 Quamotion, 2014-2024 Paul Kratt. Sourced from [Sappharad/GDIbuilder](https://github.com/Sappharad/GDIbuilder), itself a fork of upstream [DiscUtils](https://github.com/DiscUtils/DiscUtils).
- **xdelta3** (Apache 2.0) - VCDIFF binary diff and patch library. Copyright Joshua MacDonald. Sourced from [jmacd/xdelta](https://github.com/jmacd/xdelta).
- **liblzma / XZ Utils** (0BSD) - LZMA compression library, used by xdelta3 for VCDIFF LZMA secondary compression. Copyright Lasse Collin; LZMA algorithm by Igor Pavlov. Sourced from [tukaani-project/xz](https://github.com/tukaani-project/xz).
- **libchdr** (BSD 3-Clause) - native CHD disc image decompression library, implementing the MAME CHD format. Sourced from [rtissera/libchdr](https://github.com/rtissera/libchdr). Internally bundles zlib (zlib license) and FLAC (BSD).
- **libchdw** (BSD 3-Clause) - native CHD disc image compression library. Sourced from [mamedev/mame](https://github.com/mamedev/mame) (MAME 0.238 `src/lib/util/chd.cpp`, `cdrom.cpp`, `chdcd.cpp`, `chdcodec.cpp`) via the [charlesthobe/chdman](https://github.com/charlesthobe/chdman) standalone build. Internally bundles zlib (zlib license), LZMA SDK (public domain), and FLAC (BSD).
- **gdidrop** (BSD 2-Clause) - Redump GD-ROM CUE/BIN to GDI conversion reference. Copyright © 2019 Feyris-Tan. Universal Dreamcast Patcher's `GdiConverter` is adapted from gdidrop's `DoConversion` routine. Sourced from [feyris-tan/gdidrop](https://github.com/feyris-tan/gdidrop).
- **reDumpStudio** - rolling-CRC32 reconstruction algorithm reference, used by the Redump DAT byte-exact GDI to CUE/BIN path. Copyright © 2021 LedZeppelin68. Universal Dreamcast Patcher's `RedumpReconstructor` is adapted from reDumpStudio's `Form1.Roll` routine. Sourced from [LedZeppelin68/reDumpStudio](https://github.com/LedZeppelin68/reDumpStudio).
- **BizHawk** (MIT) - CD-ROM Mode-1 EDC / ECC algorithms used by raw sector synthesis. Copyright © 2012 BizHawk team. Sourced from [TASEmulators/BizHawk](https://github.com/TASEmulators/BizHawk) via DiscUtilsGD's bundled `ECM.cs`.
- **Avalonia 11.2.3** (MIT) - cross-platform .NET UI framework. Includes Avalonia.Desktop, Avalonia.Themes.Fluent, and Avalonia.Fonts.Inter (which ships the Inter typeface, SIL OFL 1.1).
- **MessageBox.Avalonia 3.2.0** (MIT) - modal dialog helpers.

All original copyright notices and licenses for these components have been preserved.
