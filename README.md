# Universal Dreamcast Patcher
<img align="right" src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/screenshot.png?raw=true">Universal Dreamcast Patcher aims to provide an easy, cross-format game patching solution for the Dreamcast community. Such patches typically come in the form of English translations and other forms of ROM hacks.

Universal Dreamcast Patcher is designed to accept any disc image as its source, whether it be formatted as a TOSEC-style GDI or a Redump-style CUE. This application will extract any disc image meeting those standards, overwrite and/or add to its data according to a given patch (DCP file), and then rebuild the disc image with the new data. Due to the flexible nature of acceptable disc image input, file hashes are not utilized for pre/post-patching verification.

Under the hood, this application utilizes [gditools](https://sourceforge.net/projects/dcisotools/), [buildgdi](https://projects.sappharad.com/tools/gdibuilder.html), and a modified version of [RedumpCUE2GDI](https://github.com/AwfulBear/RedumpCUE2GDI).

## Table of Contents
1. [Latest Version](https://github.com/DerekPascarella/UniversalDreamcastPatcher#latest-version)
1. [Changelog](https://github.com/DerekPascarella/UniversalDreamcastPatcher#changelog)
1. [Existing Features](https://github.com/DerekPascarella/UniversalDreamcastPatcher#existing-features)
1. [Known Issues and Limitations](https://github.com/DerekPascarella/UniversalDreamcastPatcher#known-issues-and-limitations)
1. [Roadmap](https://github.com/DerekPascarella/UniversalDreamcastPatcher#roadmap)
1. [Usage: Patching](https://github.com/DerekPascarella/UniversalDreamcastPatcher#usage-patching)
1. [Usage: Creating Patches](https://github.com/DerekPascarella/UniversalDreamcastPatcher#usage-creating-patches)
   1. [Example](https://github.com/DerekPascarella/UniversalDreamcastPatcher#example)

## Latest Version
The latest version of Universal Dreamcast Patcher is [0.4](https://github.com/DerekPascarella/UniversalDreamcastPatcher/releases/download/0.4/Universal.Dreamcast.Patcher.v0.4.zip).

## Changelog
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
* File hashes of patched disc images (output) are not consistent even when using the same source disc image (input) and same patch file.  This is partly due to an issue with gditools which does not preserve timestamps on extracted folders. Instead, the current day and time are used to generate the folder creation timestamp at the moment of extraction. As of now, no ISO extraction utilities (with the necessary LBA options) that I've researched successfully preserve timestamps on folders.  As a workaround, the hardcoded timestamp of 1999-09-09-00:00:00 (UTC) is being used.  However, buildgdi is still inserting some dynamic timestamps (and other metadata) into the modified data track.  After some research, I believe buildgdi's implementation of [DiscUtils](https://github.com/Sappharad/GDIbuilder/tree/master/GDIbuilder/DiscUtils) is to blame, although this may just be part of the ISO 9660 standard and thus considered expected behavior.

## Roadmap
As Universal Dreamcast Patcher evolves and improves over time, the list below represents features which I'd like to implement.

* Support for patched disc image (output) in Redump-style CUE format.
* Support for source and patched disc images (input and output) in CDI format.
* Extend DCP patch format to support modifying CDDA tracks.
* Ensure consistent file hashes for patched disc images (output).
* Research methods for decreasing size of patch file, such as using diffs/deltas on modified files.
* Linux and Mac support.

## Usage: Patching
Universal Dreamcast Patcher is simple to use.  After launching the application, follow the steps below.

1. Click "Select GDI or CUE" to open the source disc image.
2. Click "Select Patch" to open the DCP patch file.
3. Click "Apply Patch" to generate the patched GDI.
   * The patched GDI will be generated in the folder from with the application is launched.
4. Click "Quit" to exit the application.

Details on the current step of the patching process will be updated as they progress.  Any errors encountered during sanity or integrity checks will be presented and the patching process will be halted.

Note that throughout the patching process, several temporary folders and files will be written to the folder from which the application is launched, all of which are cleaned up upon completion.

## Usage: Creating Patches
The DCP patch format was designed specifically for Universal Dreamcast Patcher.  This format is neither complex nor difficult to understand.  It is extremely simple by design and as a result, DCP patches can be created without the use of any special software.  The steps for creating a patch are as follows.

1. Create a ZIP archive containing (in its root) all of the files/folders from the game's data that have been modified or are new.  Be sure to retain original folder structure and hierarchy.
   * If the patch should use a modified IP.BIN file, simply create a folder named "bootsector" in the root of the ZIP archive and place IP.BIN inside of it.  Note that this folder and file will not be included in the patched disc image's (output) filesystem.
2. Change the extension of the file from ZIP to DCP.
   * Note that the base filename of the DCP will be used when this application generates the patched GDI (e.g. a patch file named "My Game (v1.0).dcp" will result in a patched GDI folder named "My Game (v1.0) [GDI]").

### Example
In this example, an existing DCP patch is seen being opened with 7-Zip, a common archive creator and extractor.

<p align="center">
<img src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/example_1.png?raw=true">
</p>

All modified or new files reside in the root of the patch.  Likewise, all folders containing modified or new files exist in the root of the patch just as they did within the game's original disc structure.  Furthmore, a "bootsector" folder is seen in this example, along with its content.

<p align="center">
<img src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshots/example_2.png?raw=true">
</p>
