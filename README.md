# Universal Dreamcast Patcher
<img align="right" width="350" src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/screenshot.png?raw=true">Universal Dreamcast Patcher aims to provide an easy, cross-format game patching solution for the Dreamcast community. Such patches typically come in the form of English translations and other ROM hacks.

Universal Dreamcast Patcher is designed to accept any disc image as its source, whether it be formatted as a TOSEC-style GDI or a Redump-style CUE. This application will extract any disc image meeting those standards, overwrite and/or add to its data according to a given patch (DCP file), and then rebuild the disc image with the new data. Due to the flexible nature of acceptable disc image input, file hashes are not utilized for pre/post-patching verification.

As of version 1.3, Universal Dreamcast Patcher now ships bundled with its own dedicated patch-building utility. This utility, Universal Dreamcast Patch Builder, produces a DCP patch file based on changes between two disc images (modified and original).

Under the hood, this application utilizes [gditools](https://sourceforge.net/projects/dcisotools/), [buildgdi](https://projects.sappharad.com/tools/gdibuilder.html), [bin2iso](http://jj1odm.qp.land.to/#dcpprip), [xdelta](http://xdelta.org/), extract, a version of [RedumpCUE2GDI](https://github.com/AwfulBear/RedumpCUE2GDI) modified by me, and my own [Dreamcast IP.BIN Patcher](https://github.com/DerekPascarella/Dreamcast-IP.BIN-Patcher) utility.

## Table of Contents
1. [Latest Version](https://github.com/DerekPascarella/UniversalDreamcastPatcher#latest-version)
2. [Changelog](https://github.com/DerekPascarella/UniversalDreamcastPatcher#changelog)
3. [Existing Features](https://github.com/DerekPascarella/UniversalDreamcastPatcher#existing-features)
4. [Known Issues and Limitations](https://github.com/DerekPascarella/UniversalDreamcastPatcher#known-issues-and-limitations)
5. [Roadmap](https://github.com/DerekPascarella/UniversalDreamcastPatcher#roadmap)
6. [Usage: Patching](https://github.com/DerekPascarella/UniversalDreamcastPatcher#usage-patching)
7. [Usage: Creating Patches](https://github.com/DerekPascarella/UniversalDreamcastPatcher#usage-creating-patches)
   - [Automatic Method](https://github.com/DerekPascarella/UniversalDreamcastPatcher#automatic-method)
   - [Manual Method](https://github.com/DerekPascarella/UniversalDreamcastPatcher#manual-method)

## Latest Version
The latest version of Universal Dreamcast Patcher is [1.3](https://github.com/DerekPascarella/UniversalDreamcastPatcher/releases/download/1.3/Universal.Dreamcast.Patcher.v1.3.zip).

## Changelog
* Version 1.3 (2022-05-06)
  * Added xdelta support to reduce patch size, as well as eliminate the potential distribution of whole files containing copyrighted material.
  * Introduced separate patch-building utility for developers to automatically produce patch files, analyzing changes between original retail disc image and modified one.
* Version 1.2 (2022-03-19)
  * Enhanced GDI compatibility and integrity checking.
* Version 1.1 (2022-03-17)
  * Eliminated GUI lockup that occurred at different stages of the patching process.
* Version 1.0 (2021-11-26)
  * Fixed bug in LBA calculation used for extracting GDI data tracks.
* Version 0.9 (2021-11-22)
  * Due to many anti-virus tools erroneously flagging the modified version of gditools from v0.8 as malware, this version uses an alternative method for GDI extraction that still delivers the same level of compatibility, leveraging bin2iso and extract.
* Version 0.8 (2021-11-19)
  * Now using modified version of gditools (thanks to [mrneo240](https://github.com/mrneo240)) that can extract the dozen-or-so problematic GDIs which previously failed.
* Version 0.7 (2021-06-01)
  * Fixed bug with temporary folders/files if they're written to a different disk drive than the one from which the application is launched.
* Version 0.6 (2021-05-26)
  * Changed location of temporary folders/files to use Windows' default location rather than the application's working directory.
  * Updated logo (also, watch what happens when the "Apply Patch" button is clicked).
* Version 0.5 (2021-05-19)
  * Fixed bug in file path parsing.
* Version 0.4 (2021-05-18)
  * Fixed bug with incorrect GDI building when source disc image contains CDDA.
* Version 0.3 (2021-05-17)
  * The "bootsector" folder and its IP.BIN were erroneously being included as a folder and file in the patched GDI's filesystem.
* Version 0.2 (2021-05-17)
  * Added support for source disc images with CDDA.
  * Fixed bug with source disc image integrity verification.
* Version 0.1 (2021-05-16)
  * Initial release.

## Existing Features
Below is a specific list of Universal Dreamcast Patcher's current features.

* Source disc image integrity verification.
* Support for source disc images (input) with CDDA.
* Disc image patching with custom IP.BIN.
* Support for xdeltas in order to reduce patch size and mitigate copyright concerns.
* Supported formats for source disc image (input):
  * TOSEC-style GDI
  * Redump-style CUE
* Supported formats for patched disc image (output):
  * TOSEC-style GDI
* Supported formats for patch files:
  * DCP

## Known Issues and Limitations
While Universal Dreamcast Patcher delivers its core features reliably, all known issues and limitations of the application are listed below.

* Patched disc image (output) cannot be created in Redump-style CUE format.
* No CDI format support for source or patched disc images (input and output).
* While source disc images (input) with CDDA are supported, the DCP patch format does not yet include a method for modifying CDDA tracks.
* File hashes of patched disc images (output) are not consistent even when using the same source disc image (input) and same patch file.  This is partly due to an issue with gditools which does not preserve timestamps on extracted folders. Instead, the current day and time are used to generate the folder creation timestamp at the moment of extraction. As of now, no ISO extraction utilities (with the necessary LBA options) that I've researched successfully preserve timestamps on folders.  As a workaround, the hardcoded timestamp of 1999-09-09-12:12:12 (UTC) is being used.  However, buildgdi is still inserting some dynamic timestamps (and other metadata) into the modified data track.  After some research, I believe buildgdi's implementation of [DiscUtils](https://github.com/Sappharad/GDIbuilder/tree/master/GDIbuilder/DiscUtils) is to blame, although this may just be part of the ISO 9660 standard and thus considered expected behavior.

## Roadmap
As Universal Dreamcast Patcher evolves and improves over time, the list below represents features which I'd like to implement.

* Support for patched disc image (output) in Redump-style CUE format.
* Support for source and patched disc images (input and output) in CDI format.
* Extend DCP patch format to support modifying CDDA tracks.
* Ensure consistent file hashes for patched disc images (output).
* Linux and Mac support.

## Usage: Patching
Universal Dreamcast Patcher is simple to use.  After launching the application, follow the steps below.

1. Click "Select GDI or CUE" to open the source disc image.
2. Click "Select Patch" to open the DCP patch file.
3. Click "Apply Patch" to generate the patched GDI.
   * The patched GDI will be generated in the folder from which the application is launched.
4. Click "Quit" to exit the application.

Details on the current step of the patching process will be updated as they progress.  Any errors encountered during sanity or integrity checks will be presented and the patching process will be halted.

## Usage: Creating Patches
The DCP patch format was designed specifically for Universal Dreamcast Patcher.  Presently, there is both an automatic and a manual method one can use to build a patch.

### Automatic Method
<img width="222" align="right" src="https://raw.githubusercontent.com/DerekPascarella/UniversalDreamcastPatcher/main/screenshots/screenshot_builder.png">Use this method when:
* You do not wish to distribute whole files containing copyrighted content.
* Your aim is to keep patch files as small as possible.
* You want all the work done for you!

As of version 1.3, Universal Dreamcast Patcher now ships bundled with its own dedicated patch-building utility.  This utility, Universal Dreamcast Patch Builder, produces a DCP patch file based on changes between two disc images (unpatched and patched).

For example, a translation patch developer would supply the original retail disc image along with their modified one.  Universal Dreamcast Patch Builder would analyze the two for changes and then automatically generate the DCP patch file.

Furthermore, Universal Dreamcast Patch Builder includes options to apply additional modifications to IP.BIN, like enabling VGA mode, setting the game as region-free, or giving the game a custom name label.

The steps for automatically creating a patch are as follows.

1. In the "Step 1" box, select the original, unmodified disc image as "Unpatched GDI".  Then, select the modified disc image as "Patched GDI".
2. In the "Step 2" box, supply a filename for the DCP patch.  Note that the base filename of the DCP will be used when the patching application generates the patched GDI (e.g. a patch file named "My Game (v1.0).dcp" will result in a patched GDI folder named "My Game (v1.0) [GDI]").
3. In the "Step 3" box, select whether a custom IP.BIN file should be used when building the patch.  In many cases, patch developers won't bother with this step, but there are several advantages in enabling these options:
   * **Region-Free** - Patched disc image (output) can be booted on any ODE or emulator, regardless of region setting, and without enabling region-free options within the ODE or emulator itself.
   * **VGA** - If supported, patched disc image (output) can be booted in VGA mode on any ODE or emulator, regardless of VGA auto-patching settings within the ODE or emulator itself.
   * **Custom Game Name** - Patched disc image (output) will be displayed using a custom name within tools like the various SD card managers for GDEMU, thus giving another degree of creative control to patch developers opting for a localized game title.
4. Click "Build Patch".

### Manual Method
Use this method when:
* The distribution of copyrighted content is not a concern.
* Reducing the size of a patch file is not a priority.

The steps for manually creating a patch are as follows.

1. Create a ZIP archive containing (in its root) all of the files/folders from the game's data that have been modified or are new.  Be sure to retain original folder structure and hierarchy.
   * If the patch should use a modified IP.BIN file, simply create a folder named "bootsector" in the root of the ZIP archive and place IP.BIN inside of it.  Note that this folder and file will not be included in the patched disc image's (output) filesystem.
   * If the patch's new IP.BIN has been region-modified, ensure the text region (not just the single-byte flag in the header) has been modified, as well.  This ensures compatibility with certain emulators that fail to boot when there's a discrepancy.  If unsure of how to make those necessary changes, leverage my [Dreamcast IP.BIN Patcher](https://github.com/DerekPascarella/Dreamcast-IP.BIN-Patcher) utility.
2. Change the extension of the file from ZIP to DCP.
   * Note that the base filename of the DCP will be used when this application generates the patched GDI (e.g. a patch file named "My Game (v1.0).dcp" will result in a patched GDI folder named "My Game (v1.0) [GDI]").

#### Example
In this example, an existing DCP patch is seen being opened with 7-Zip, a common archive creator and extractor.

<p align="center">
<img src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/example_1.png?raw=true">
</p>

All modified or new files reside in the root of the patch.  Likewise, all folders containing modified or new files exist in the root of the patch just as they did within the game's original disc structure.  Furthermore, a "bootsector" folder is seen in this example, along with its content.

<p align="center">
<img src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/example_2.png?raw=true">
</p>
