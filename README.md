# Universal Dreamcast Patcher
<img align="right" src="https://github.com/DerekPascarella/UniversalDreamcastPatcher/blob/main/screenshot.png?raw=true">Universal Dreamcast Patcher aims to provide an easy, cross-format game patching solution to the Dreamcast community.  Such patches typically come in the form of English translations and other forms of ROM hacks.  Universal Dreamcast Patcher is designed to accept any disc image as its source, whether it be formatted as a TOSEC-style GDI or a Redump-style CUE.  This application will extract any disc image meeting those standards, overwrite and/or add to its data according to a given patch (DCP file), and then rebuild the disc image with the new data.  Due to the flexible nature of acceptable disc image input, file hashes are not utilized for pre/post-patching verification.

## Latest Version

The latest version of Universal Dreamcast Patcher is [0.1](http://xxxxx).

## Table of Contents

* [Existing Features](https://github.com/DerekPascarella/UniversalDreamcastPatcher#existing-features)
* [Known Issues and Limitations](https://github.com/DerekPascarella/UniversalDreamcastPatcher#known-issues-and-limitations)
* [Roadmap](https://github.com/DerekPascarella/UniversalDreamcastPatcher#roadmap)
* [Usage: Patching](https://github.com/DerekPascarella/UniversalDreamcastPatcher#usage-patching)
* [Usage: Creating Patches](https://github.com/DerekPascarella/UniversalDreamcastPatcher#usage-creating-patches)

## Existing Features
Below is a specific list of Universal Dreamcast Patcher's current features.

* Source disc image integrity verification.
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
* File hashes of the patched disc image (output) are not consistent even when using the same source disc image (input).  This is due to an issue with [gditools](https://sourceforge.net/projects/dcisotools/) which does not preserve timestamps on extracted folders.  Instead, the current day and time are used to generate the folder creation timestamp at the moment of extraction.  As of now, no ISO extraction utilities (with the necessary LBA options) that I've researched successfully preserve timestamps on folders.

## Roadmap
As Universal Dreamcast Patcher evolves and improves over time, the list below represents features which I'd like to implement.

* Support for patched disc image (output) in Redump-style CUE format.
* Support for source and patched disc images (input and output) in CDI format.

## Usage: Patching
Universal Dreamcast Patcher is simple to use.  After launching the application, follow the steps below.

* Click "Select GDI or CUE" to open the source disc image.
* Click "Select Patch" to open the DCP patch file.
* Click "Apply Path" to generate the patched GDI.
  * The patched GDI will be generated in the folder from with the application is launched.
* Click "Quit" to exit the application.

Details on the current step of the patching process will be updated as they progress.  Any errors encountered during sanity or integrity checks will be presented and the patching process will be halted.

Note that throughout the patching process, several temporary folders and files will be written to the folder from which the application is launched.

## Usage: Creating Patches
The DCP patch format was designed specifically for Universal Dreamcast Patcher.  This format is not complex or difficult to understand.  It is extremely simple by design and as a result DPC patches can be created without the use of any special software.  The steps to creating a patch are as follows.

1. Create a ZIP file containing all of the files/folder from the game's data that have been modified or are new.  Be sure to retain original folder structure and hierarchy.
2. Change the extension on the file from ZIP to DCP.
   * Note that the base filename of the DCP will be used when this application generates the patched GDI.

# Example
In this example... ... ...
