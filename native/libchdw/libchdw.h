// libchdw - C ABI around MAME's CHD writer, for P/Invoke from UDP's C# side.
// Produces CHDv5 CD/GD-ROM CHDs using the same codec defaults as chdman.
//
// Written by Derek Pascarella (ateam)

#ifndef LIBCHDW_H
#define LIBCHDW_H

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_WIN32)
#  ifdef LIBCHDW_BUILDING
#    define LIBCHDW_API __declspec(dllexport)
#  else
#    define LIBCHDW_API __declspec(dllimport)
#  endif
#else
#  define LIBCHDW_API __attribute__((visibility("default")))
#endif

typedef enum {
    CHDW_OK = 0,
    CHDW_ERR_INVALID_INPUT = 1,    // input file missing or unreadable
    CHDW_ERR_PARSE_FAILED = 2,     // CUE/GDI could not be parsed
    CHDW_ERR_CREATE_FAILED = 3,    // chd_file::create returned an error
    CHDW_ERR_METADATA_FAILED = 4,  // metadata write failed
    CHDW_ERR_COMPRESS_FAILED = 5,  // compression loop failed
    CHDW_ERR_CANCELLED = 6,        // user cancelled via callback
    CHDW_ERR_UNKNOWN = 99
} chdw_error;

// percent is 0..100. Return non-zero to cancel.
typedef int (*chdw_progress_cb)(void* user, double percent);

// Compress a .cue or .gdi to a .chd. chdcd_parse_toc auto-detects the format.
// On error returns a chdw_error code; chdw_last_error() has the message.
LIBCHDW_API int chdw_create_cd_chd(
    const char* input_path,
    const char* output_chd_path,
    chdw_progress_cb cb,
    void* user_data);

// Thread-local. Valid until the next libchdw call on the same thread.
LIBCHDW_API const char* chdw_last_error(void);

LIBCHDW_API const char* chdw_version(void);

#ifdef __cplusplus
}
#endif

#endif
