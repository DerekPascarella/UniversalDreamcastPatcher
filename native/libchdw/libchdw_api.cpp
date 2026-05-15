// libchdw: C ABI around MAME's chd_file_compressor.
// Links the vendored MAME 0.238 utils for format and codec work.
//
// Written by Derek Pascarella (ateam)

#define LIBCHDW_BUILDING
#include "libchdw.h"

#include "chd.h"
#include "chdcd.h"
#include "cdrom.h"
#include "chdcodec.h"
#include "corefile.h"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <exception>
#include <memory>
#include <stdexcept>
#include <string>
#include <system_error>

namespace {

// Last error buffer. UDP runs one compression at a time so a plain static
// buffer is fine.
constexpr std::size_t kLastErrorCap = 1024;
char g_last_error[kLastErrorCap] = {};

void set_last_error(const std::string& msg)
{
    const std::size_t n = std::min(msg.size(), kLastErrorCap - 1);
    std::memcpy(g_last_error, msg.data(), n);
    g_last_error[n] = '\0';
}

void clear_last_error()
{
    g_last_error[0] = '\0';
}

// Default CD codec stack: cdlz, cdzl, cdfl, none.
constexpr chd_codec_type kDefaultCdCompression[4] = {
    CHD_CODEC_CD_LZMA,
    CHD_CODEC_CD_ZLIB,
    CHD_CODEC_CD_FLAC,
    CHD_CODEC_NONE,
};

// I/O errors thrown from read_data() and caught at the C ABI boundary.
class chdw_io_error : public std::runtime_error
{
public:
    explicit chdw_io_error(const std::string& msg) : std::runtime_error(msg) {}
};

// CD compressor with read_data throwing on I/O failures instead of exit()ing.
class chdw_cd_compressor : public chd_file_compressor
{
public:
    chdw_cd_compressor(cdrom_toc& toc, chdcd_track_input_info& info)
        : m_toc(toc), m_info(info) {}

    virtual std::uint32_t read_data(void* dest_void, std::uint64_t offset, std::uint32_t length) override
    {
        if ((offset % CD_FRAME_SIZE) != 0 || (length % CD_FRAME_SIZE) != 0)
            throw chdw_io_error("internal: misaligned read_data request");

        std::uint8_t* dest = static_cast<std::uint8_t*>(dest_void);
        std::memset(dest, 0, length);

        std::uint64_t startoffs = 0;
        std::uint32_t length_remaining = length;

        for (int tracknum = 0; tracknum < m_toc.numtrks; tracknum++)
        {
            const cdrom_track_info& trackinfo = m_toc.tracks[tracknum];
            std::uint64_t endoffs = startoffs +
                std::uint64_t(trackinfo.frames + trackinfo.extraframes) * CD_FRAME_SIZE;

            if (offset >= startoffs && offset < endoffs)
            {
                if (!m_file || m_lastfile.compare(m_info.track[tracknum].fname) != 0)
                {
                    m_file.reset();
                    m_lastfile = m_info.track[tracknum].fname;
                    auto err = util::core_file::open(m_lastfile, OPEN_FLAG_READ, m_file);
                    if (err)
                        throw chdw_io_error("error opening track file: " + m_lastfile + ": " + err.message());
                }

                std::uint64_t bytesperframe = trackinfo.datasize + trackinfo.subsize;
                std::uint64_t src_track_start = m_info.track[tracknum].offset;
                std::uint64_t src_track_end = src_track_start + bytesperframe * std::uint64_t(trackinfo.frames);
                std::uint64_t pad_track_start = src_track_end -
                    (std::uint64_t(m_toc.tracks[tracknum].padframes) * bytesperframe);
                std::uint64_t split_track_start = pad_track_start -
                    (std::uint64_t(m_toc.tracks[tracknum].splitframes) * bytesperframe);

                if (m_toc.tracks[tracknum].splitframes == 0)
                    split_track_start = UINT64_MAX;

                while (length_remaining != 0 && offset < endoffs)
                {
                    std::uint64_t src_frame_start =
                        src_track_start + ((offset - startoffs) / CD_FRAME_SIZE) * bytesperframe;

                    if (src_frame_start == split_track_start &&
                        m_lastfile.compare(m_info.track[tracknum + 1].fname) != 0)
                    {
                        m_file.reset();
                        m_lastfile = m_info.track[tracknum + 1].fname;
                        auto err = util::core_file::open(m_lastfile, OPEN_FLAG_READ, m_file);
                        if (err)
                            throw chdw_io_error("error opening split track file: " + m_lastfile + ": " + err.message());
                    }

                    if (src_frame_start < src_track_end)
                    {
                        if (src_frame_start >= pad_track_start)
                        {
                            std::memset(dest, 0, bytesperframe);
                        }
                        else
                        {
                            auto err = m_file->seek(
                                (src_frame_start >= split_track_start)
                                    ? src_frame_start - split_track_start
                                    : src_frame_start,
                                SEEK_SET);
                            std::size_t count = 0;
                            if (!err)
                                err = m_file->read(dest, bytesperframe, count);
                            if (err || count != bytesperframe)
                                throw chdw_io_error("error reading track file: " + m_lastfile);
                        }

                        if (m_info.track[tracknum].swap)
                        {
                            for (std::uint32_t s = 0; s < 2352; s += 2)
                            {
                                std::uint8_t t = dest[s];
                                dest[s] = dest[s + 1];
                                dest[s + 1] = t;
                            }
                        }
                    }

                    offset += CD_FRAME_SIZE;
                    dest += CD_FRAME_SIZE;
                    length_remaining -= CD_FRAME_SIZE;
                    if (length_remaining == 0)
                        break;
                }
            }

            startoffs = endoffs;
        }

        return length - length_remaining;
    }

private:
    std::string m_lastfile;
    util::core_file::ptr m_file;
    cdrom_toc& m_toc;
    chdcd_track_input_info& m_info;
};

} // namespace

extern "C" {

LIBCHDW_API const char* chdw_version(void)
{
    return "libchdw 1.0 (MAME 0.238 codecs)";
}

LIBCHDW_API const char* chdw_last_error(void)
{
    return g_last_error;
}

LIBCHDW_API int chdw_create_cd_chd(
    const char* input_path,
    const char* output_chd_path,
    chdw_progress_cb cb,
    void* user_data)
{
    clear_last_error();

    if (!input_path || !output_chd_path)
    {
        set_last_error("input_path and output_chd_path must not be NULL");
        return CHDW_ERR_INVALID_INPUT;
    }

    try
    {
        chdcd_track_input_info track_info;
        cdrom_toc toc{};
        auto parse_err = chdcd_parse_toc(input_path, toc, track_info);
        if (parse_err)
        {
            set_last_error(std::string("failed to parse input: ") + parse_err.message());
            return CHDW_ERR_PARSE_FAILED;
        }

        // Pad each track up to a 4-frame boundary. Decoders subtract this on read.
        std::uint32_t totalsectors = 0;
        for (int t = 0; t < toc.numtrks; t++)
        {
            cdrom_track_info& ti = toc.tracks[t];
            int padded = (ti.frames + CD_TRACK_PADDING - 1) / CD_TRACK_PADDING;
            ti.extraframes = padded * CD_TRACK_PADDING - ti.frames;
            totalsectors += ti.frames + ti.extraframes;
        }

        chd_codec_type compression[4];
        std::memcpy(compression, kDefaultCdCompression, sizeof(compression));

        // Heap-allocate. The class has multi-KB inline members that overflow
        // macOS's 512 KB secondary-thread stack from .NET's worker pool.
        auto compressor = std::make_unique<chdw_cd_compressor>(toc, track_info);

        const std::uint64_t logicalbytes = std::uint64_t(totalsectors) * std::uint64_t(CD_FRAME_SIZE);
        const std::uint32_t hunkbytes = CD_FRAMES_PER_HUNK * CD_FRAME_SIZE;
        const std::uint32_t unitbytes = CD_FRAME_SIZE;

        auto create_err = compressor->create(output_chd_path, logicalbytes, hunkbytes, unitbytes, compression);
        if (create_err)
        {
            set_last_error(std::string("failed to create CHD: ") + create_err.message());
            return CHDW_ERR_CREATE_FAILED;
        }

        auto meta_err = cdrom_write_metadata(compressor.get(), &toc);
        if (meta_err)
        {
            set_last_error(std::string("failed to write CD metadata: ") + meta_err.message());
            return CHDW_ERR_METADATA_FAILED;
        }

        compressor->compress_begin();

        double complete = 0.0;
        double ratio = 0.0;
        std::error_condition err;
        while ((err = compressor->compress_continue(complete, ratio)) == chd_file::error::WALKING_PARENT
            || err == chd_file::error::COMPRESSING)
        {
            if (cb)
            {
                int cancel = cb(user_data, 100.0 * complete);
                if (cancel)
                {
                    set_last_error("compression cancelled by caller");
                    return CHDW_ERR_CANCELLED;
                }
            }
        }

        if (err)
        {
            set_last_error(std::string("compression failed: ") + err.message());
            return CHDW_ERR_COMPRESS_FAILED;
        }

        if (cb)
            cb(user_data, 100.0);
        return CHDW_OK;
    }
    catch (const chdw_io_error& ex)
    {
        set_last_error(ex.what());
        return CHDW_ERR_COMPRESS_FAILED;
    }
    catch (const std::exception& ex)
    {
        set_last_error(std::string("unexpected exception: ") + ex.what());
        return CHDW_ERR_UNKNOWN;
    }
    catch (...)
    {
        set_last_error("unknown exception");
        return CHDW_ERR_UNKNOWN;
    }
}

} // extern "C"
