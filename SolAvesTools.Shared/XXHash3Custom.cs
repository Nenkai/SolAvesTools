// Full credits goes to mvit for figuring this out
// https://github.com/easyaspi314/xxhash-clean/blob/master/xxh3-64b-ref.c#L156 (for stock implementation reference)

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace SolAvesTools.Shared;

public class XXHash3Custom
{
    const int KARS_MAX_LEN = 128;

    const ulong PRIME64_1 = 0x9E3779B185EBCA87UL;
    const ulong PRIME64_2 = 0xC2B2AE3D27D4EB4FUL;
    const ulong PRIME64_3 = 0x165667B19E3779F9UL;
    const ulong PRIME_MX1 = 0x165667919E3779F9UL;  /* avalanche */
    const ulong PRIME_MX2 = 0x9FB21C651E98DF25UL;  /* rrmxmx    */

    const ulong SEED13 = 0x0273A394DA509842UL;  /* 1-3 bytes, stock secret */    
    const ulong SEED48 = 0x42D50092CE5C0008UL;  /* 4-8 bytes, stock secret */
    const ulong BF1 = 0x9a210f10b9cea8b5UL;  /* 9-16 bytes */
    const ulong BF2 = 0xacd5101ebf8972aaUL;  /* 9-16 bytes */
    const ulong SEED = 0x1c7f4aa70df3a74eUL;  /* 17+ bytes  */
    const ulong K0 = 0xdacaeeca19af37c8UL;  /* 17+ bytes, lane 0 override */

    static readonly ulong[] StockSecret =
    [
        0xbe4ba423396cfeb8UL, 0x1cad21f72c81017cUL, 0xdb979083e96dd4deUL, 0x1f67b3b7a4a44072UL,
        0x78e5c0cc4ee679cbUL, 0x2172ffcc7dd05a82UL, 0x8e2443f7744608b8UL, 0x4c263a81e69035e0UL,
        0xcb00c391bb52283cUL, 0xa32e531b8b65d088UL, 0x4ef90da297486471UL, 0xd8acdea946ef1938UL,
        0x3f349ce33f76faa8UL, 0x1d4f0bc7c7bbdcf9UL, 0x3159b4cd4be0518aUL, 0x647378d9c97e9fc8UL
    ];

    static readonly ulong[] NewSecret = new ulong[16];
    static XXHash3Custom()
    {
        int i;
        for (i = 0; i < 16; i++)
            NewSecret[i] = unchecked((i % 2 == 0) ? StockSecret[i] + SEED : StockSecret[i] - SEED);
        NewSecret[0] = K0;
    }

    private static uint XXH_read32(Span<byte> p)
    {
        return (uint)p[0] | ((uint)p[1] << 8) | ((uint)p[2] << 16) | ((uint)p[3] << 24);
    }

    private static ulong XXH_read64(Span<byte> p)
    {
        return (ulong)XXH_read32(p) | ((ulong)XXH_read32(p[4..]) << 32);
    }

    private static uint XXH_swap32(uint x)
    {
        return ((x << 24) & 0xff000000U) | ((x << 8) & 0x00ff0000U) |
               ((x >> 8) & 0x0000ff00U) | ((x >> 24) & 0x000000ffU);
    }

    private static ulong XXH_swap64(ulong x)
    {
        return ((ulong)XXH_swap32((uint)x) << 32) | XXH_swap32((uint)(x >> 32));
    }

    private static ulong XXH_rotl64(ulong v, int r)
    {
        return (v << r) | (v >> (64 - r));
    }

    private static ulong XXH3_mul128_fold64(ulong lhs, ulong rhs)
    {
        ulong lo_lo = (lhs & 0xFFFFFFFF) * (rhs & 0xFFFFFFFF);
        ulong hi_lo = (lhs >> 32)        * (rhs & 0xFFFFFFFF);
        ulong lo_hi = (lhs & 0xFFFFFFFF) * (rhs >> 32);
        ulong hi_hi = (lhs >> 32)        * (rhs >> 32);
        ulong cross = (lo_lo >> 32) + (hi_lo & 0xFFFFFFFF) + lo_hi;
        ulong upper = (hi_lo >> 32) + (cross >> 32) + hi_hi;
        ulong lower = (cross << 32) | (lo_lo & 0xFFFFFFFF);
        return upper ^ lower;
    }

    private static ulong XXH3_avalanche(ulong h)
    {
        h ^= h >> 37;
        h *= PRIME_MX1;
        return h ^ (h >> 32);
    }

    private static ulong XXH3_len_1to3_64b(Span<byte> b, int n, ulong[] secret, ulong seed)
    {
        uint byte1 = b[0];
        uint byte2 = b[n >> 1];
        uint byte3 = b[n - 1];

        ulong combined = ((ulong)byte1 << 16) | ((ulong)byte2 << 24) |
                         (ulong)byte3 | ((ulong)n << 8);
        ulong h = (ulong)((uint)secret[0] ^ (uint)(secret[0] >> 32));
        h += seed;
        h ^= combined;
        /* XXH64-style avalanche (not XXH3_avalanche) */
        h ^= h >> 33;
        h *= PRIME64_2;
        h ^= h >> 29;
        h *= PRIME64_3;
        return h ^ (h >> 32);
    }

    private static ulong XXH3_len_4to8_64b(Span<byte> input, int length, ulong[] secret, ulong seed)
    {
        uint input_hi = XXH_read32(input);
        uint input_lo = XXH_read32(input[(length - 4)..]);
        ulong input_64 = (ulong)input_lo | ((ulong)input_hi << 32);
        ulong acc = secret[1] ^ secret[2];
        seed ^= (ulong)XXH_swap32((uint)(seed & 0xFFFFFFFF)) << 32;
        acc -= seed;
        acc ^= input_64;
        /* rrmxmx mix, skips XXH3_avalanche */
        acc ^= XXH_rotl64(acc, 49) ^ XXH_rotl64(acc, 24);
        acc *= PRIME_MX2;
        acc ^= (acc >> 35) + (ulong)length;
        acc *= PRIME_MX2;
        acc ^= (acc >> 28);
        return acc;
    }

    // Slightly different
    private static ulong XXH3_len_9to16_64b(Span<byte> b, int length, ulong[] secret, ulong seed)
    {
        ulong lo = XXH_read64(b) ^ BF1;
        ulong hi = XXH_read64(b[(length - 8)..]) ^ BF2;
        return XXH3_avalanche((ulong)length + XXH_swap64(lo) + hi + XXH3_mul128_fold64(lo, hi));
    }

    private static ulong XXH3_mix16B(ReadOnlySpan<byte> input, ulong k0, ulong k1, ulong seed)
    {
        ulong lo = (k0 + seed) ^ BinaryPrimitives.ReadUInt64LittleEndian(input);
        ulong hi = (k1 - seed) ^ BinaryPrimitives.ReadUInt64LittleEndian(input[8..]);
        return XXH3_mul128_fold64(lo, hi);
    }

    private static ulong XXH3_len_17to128_64b(Span<byte> b, int n, ulong[] K, ulong seed)
    {
        ulong acc = (ulong)n * PRIME64_1;
        int pairs = (n - 1) / 32 + 1;
        for (int i = 0; i < pairs; i++)
        {
            acc += XXH3_mix16B(b[(16 * i)..], K[4 * i], K[4 * i + 1], seed);
            acc += XXH3_mix16B(b[(n - 16 * (i + 1))..], K[4 * i + 2], K[4 * i + 3], seed);
        }

        return XXH3_avalanche(acc);
    }

    public static bool XXH3_64(Span<byte> input, int length, out XXHash3CustomHash outHash)
    {
        Span<byte> b = input;
        outHash = 0;

        if (length == 0 || length > KARS_MAX_LEN) return false;
        if (length <= 3) outHash = XXH3_len_1to3_64b(b, length, StockSecret, SEED13);
        else if (length <= 8) outHash = XXH3_len_4to8_64b(b, length, StockSecret, SEED48);
        else if (length <= 16) outHash = XXH3_len_9to16_64b(b, length, NewSecret, 0);
        else outHash = XXH3_len_17to128_64b(b, length, NewSecret, 0);
        return true;
    }


    public static XXHash3CustomHash Hash(string str)
    {
        var bytes = Encoding.ASCII.GetBytes(str);
        XXH3_64(bytes, bytes.Length, out XXHash3CustomHash hash);
        return hash;
    }

    public static XXHash3CustomHash HashPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) 
            return 0;

        path = path.ToLowerInvariant().Replace('\\', '/');

        var bytes = Encoding.ASCII.GetBytes(path);
        XXH3_64(bytes, bytes.Length, out XXHash3CustomHash hash);
        return hash;
    }
}