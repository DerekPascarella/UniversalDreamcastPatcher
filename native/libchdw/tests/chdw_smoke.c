// Compresses a .gdi or .cue to .chd via libchdw. For round-trip testing,
// pair with chdman extractcd and byte-diff the result.
//
// Usage: chdw_smoke <input.gdi|input.cue> <output.chd>
//
// Written by Derek Pascarella (ateam)

#include "libchdw.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static int progress(void* user, double percent)
{
    (void)user;
    fprintf(stderr, "\rcompressing... %.1f%%", percent);
    fflush(stderr);
    return 0;
}

int main(int argc, char** argv)
{
    if (argc != 3)
    {
        fprintf(stderr, "Usage: %s <input.gdi|input.cue> <output.chd>\n", argv[0]);
        return 2;
    }

    fprintf(stderr, "%s\n", chdw_version());
    fprintf(stderr, "input:  %s\n", argv[1]);
    fprintf(stderr, "output: %s\n", argv[2]);

    int rc = chdw_create_cd_chd(argv[1], argv[2], progress, NULL);
    fprintf(stderr, "\n");

    if (rc != 0)
    {
        fprintf(stderr, "chdw_create_cd_chd returned %d: %s\n", rc, chdw_last_error());
        return 1;
    }

    fprintf(stderr, "OK\n");
    return 0;
}
