#!/usr/bin/env python3
"""
Builds the native 64-bit Windows/Proton proxy DLL `bink2w64.dll` for
No Man's Starfield ("Atmospheric Entry Autopilot").

How it works:
1. Forwards all 79 Bink2 video exports to `bink2w64_orig.<ExportName>` via
   standard PE export forwarding (zero runtime overhead in NTDLL/Proton).
2. On `DLL_PROCESS_ATTACH`, locates `Starfield.exe` in memory via
   `GetModuleHandleA(NULL)` and verifies the exact instruction signature at
   `base + 0x13FFEF0` (the epilogue of `SpaceCruise::OnOrbitalArrivalComplete`
   at `0x1413FFD00`).
3. Installs a 14-byte trampoline jump at `base + 0x13FFEF0` that runs on the
   Main Game Thread the moment the player's ship crosses a planet's 150 km
   upper-atmosphere cruise threshold (`[base + 0x5EAAA97] != 0`,
   `planetBodyID = [base + 0x5EB0714]`).
4. Automatically invokes Starfield's native animated planetary landing sequence
   (`0x1417DEC70` + `0x141897ED0(1)`, the internal engine implementation of
   `LandOnPlanetAnimated`) with automatic ocean/gas-giant coordinate fallback,
   allowing players to fly directly into planets from space without opening
   the Starmap UI.
"""

import struct
from pathlib import Path

BINK2_EXPORTS = [
    "BinkAllocateFrameBuffers",
    "BinkClose",
    "BinkCloseTrack",
    "BinkControlBackgroundIO",
    "BinkCopyToBuffer",
    "BinkCopyToBufferRect",
    "BinkCurrentSubtitle",
    "BinkDoFrame",
    "BinkDoFrameAsync",
    "BinkDoFrameAsyncMulti",
    "BinkDoFrameAsyncWait",
    "BinkDoFramePlane",
    "BinkFindXAudio2WinDevice",
    "BinkFreeGlobals",
    "BinkGetError",
    "BinkGetFrameBuffersInfo",
    "BinkGetGPUDataBuffersInfo",
    "BinkGetKeyFrame",
    "BinkGetPlatformInfo",
    "BinkGetRealtime",
    "BinkGetRects",
    "BinkGetSubtitleByIndex",
    "BinkGetSummary",
    "BinkGetTrackData",
    "BinkGetTrackID",
    "BinkGetTrackMaxSize",
    "BinkGetTrackType",
    "BinkGoto",
    "BinkLoadSubtitles",
    "BinkLogoAddress",
    "BinkNextFrame",
    "BinkOpen",
    "BinkOpenDirectSound",
    "BinkOpenTrack",
    "BinkOpenWaveOut",
    "BinkOpenWithOptions",
    "BinkOpenXAudio2",
    "BinkOpenXAudio27",
    "BinkOpenXAudio28",
    "BinkOpenXAudio29",
    "BinkPause",
    "BinkRegisterFrameBuffers",
    "BinkRegisterGPUDataBuffers",
    "BinkRequestStopAsyncThread",
    "BinkRequestStopAsyncThreadsMulti",
    "BinkService",
    "BinkSetError",
    "BinkSetFileOffset",
    "BinkSetFrameRate",
    "BinkSetIO",
    "BinkSetIOSize",
    "BinkSetMemory",
    "BinkSetOSFileCallbacks",
    "BinkSetPan",
    "BinkSetSimulate",
    "BinkSetSoundOnOff",
    "BinkSetSoundSystem",
    "BinkSetSoundSystem2",
    "BinkSetSoundTrack",
    "BinkSetSpeakerVolumes",
    "BinkSetVideoOnOff",
    "BinkSetVolume",
    "BinkSetWillLoop",
    "BinkShouldSkip",
    "BinkStartAsyncThread",
    "BinkUtilCPUs",
    "BinkUtilFree",
    "BinkUtilMalloc",
    "BinkUtilMutexCreate",
    "BinkUtilMutexDestroy",
    "BinkUtilMutexLock",
    "BinkUtilMutexLockTimeOut",
    "BinkUtilMutexUnlock",
    "BinkUtilSoundGlobalLock",
    "BinkUtilSoundGlobalUnlock",
    "BinkWait",
    "BinkWaitStopAsyncThread",
    "BinkWaitStopAsyncThreadsMulti",
    "RADTimerRead",
]


def align(val: int, alignment: int) -> int:
    return (val + alignment - 1) & ~(alignment - 1)


def build_proxy_dll(output_path: Path) -> None:
    image_base = 0x180000000
    sec_align = 0x1000
    file_align = 0x200

    # Layout:
    # RVA 0x1000: .text  (DllMain + AtmosphereEntryTrampoline)
    # RVA 0x2000: .rdata (Import Directory, IAT, Candidate lon/lat table, Export Directory)
    # RVA 0x4000: .data  (g_starfieldBase, oldProtect)
    # RVA 0x5000: .reloc (Minimal base relocation block so ASLR works anywhere)

    text_rva = 0x1000
    rdata_rva = 0x2000
    data_rva = 0x4000
    reloc_rva = 0x5000

    # --- Build .rdata ---
    # 0x00..0x18: IAT for KERNEL32.dll (2 functions + null terminator)
    #   IAT[0] @ rdata_rva + 0x00: GetModuleHandleA
    #   IAT[1] @ rdata_rva + 0x08: VirtualProtect
    #   IAT[2] @ rdata_rva + 0x10: 0
    iat_rva = rdata_rva + 0x00
    ilt_rva = rdata_rva + 0x20
    import_dir_rva = rdata_rva + 0x40  # 2 descriptors (40 bytes)
    hn_gmh_rva = rdata_rva + 0x68
    hn_vp_rva = rdata_rva + 0x80
    dll_name_rva = rdata_rva + 0x98
    lonlat_rva = rdata_rva + 0xB0  # 6 pairs of doubles = 96 bytes (0x60)
    export_dir_rva = rdata_rva + 0x120

    rdata = bytearray(0x120)
    # Hint/Name entries
    hn_gmh = struct.pack("<H", 0) + b"GetModuleHandleA\x00"
    if len(hn_gmh) % 2:
        hn_gmh += b"\x00"
    hn_vp = struct.pack("<H", 0) + b"VirtualProtect\x00"
    if len(hn_vp) % 2:
        hn_vp += b"\x00"

    rdata[0x68 : 0x68 + len(hn_gmh)] = hn_gmh
    rdata[0x80 : 0x80 + len(hn_vp)] = hn_vp
    rdata[0x98 : 0x98 + 13] = b"KERNEL32.dll\x00"

    # IAT & ILT
    struct.pack_into("<QQQ", rdata, 0x00, hn_gmh_rva, hn_vp_rva, 0)
    struct.pack_into("<QQQ", rdata, 0x20, hn_gmh_rva, hn_vp_rva, 0)

    # Import Descriptor for KERNEL32.dll
    struct.pack_into(
        "<IIIII",
        rdata,
        0x40,
        ilt_rva,      # OriginalFirstThunk
        0,            # TimeDateStamp
        0,            # ForwarderChain
        dll_name_rva, # Name
        iat_rva,      # FirstThunk
    )

    # 6 candidate (lon, lat) landing coordinates in radians
    candidates = [
        (0.0, 0.0),
        (0.35, 0.15),
        (-0.35, -0.15),
        (0.75, 0.30),
        (-0.75, -0.30),
        (0.0, 0.55),
    ]
    for idx, (lon, lat) in enumerate(candidates):
        struct.pack_into("<dd", rdata, 0xB0 + idx * 16, lon, lat)

    # Export Directory at rdata_rva + 0x120
    num_exports = len(BINK2_EXPORTS)
    exp_header_size = 40
    eat_off = exp_header_size
    enpt_off = eat_off + num_exports * 4
    eot_off = enpt_off + num_exports * 4
    strings_off = eot_off + num_exports * 2

    exp_blob = bytearray(strings_off)
    exp_dll_name_rva = export_dir_rva + len(exp_blob)
    exp_blob.extend(b"bink2w64.dll\x00")

    for i, name in enumerate(BINK2_EXPORTS):
        name_rva = export_dir_rva + len(exp_blob)
        exp_blob.extend(name.encode("ascii") + b"\x00")
        fwd_rva = export_dir_rva + len(exp_blob)
        exp_blob.extend(f"bink2w64_orig.{name}".encode("ascii") + b"\x00")

        struct.pack_into("<I", exp_blob, eat_off + i * 4, fwd_rva)
        struct.pack_into("<I", exp_blob, enpt_off + i * 4, name_rva)
        struct.pack_into("<H", exp_blob, eot_off + i * 2, i)

    struct.pack_into(
        "<IIHHIIIIIII",
        exp_blob,
        0,
        0,                            # Characteristics
        0,                            # TimeDateStamp
        0, 0,                         # MajorVersion, MinorVersion
        exp_dll_name_rva,             # Name RVA
        1,                            # Base ordinal
        num_exports,                  # NumberOfFunctions
        num_exports,                  # NumberOfNames
        export_dir_rva + eat_off,     # AddressOfFunctions
        export_dir_rva + enpt_off,    # AddressOfNames
        export_dir_rva + eot_off,     # AddressOfNameOrdinals
    )
    export_dir_size = len(exp_blob)
    rdata.extend(exp_blob)

    # --- Build .text ---
    # .text layout:
    # 0x0000: DllMain(rcx=hinst, edx=reason, r8=reserved)
    # 0x00C0: AtmosphereEntryTrampoline (jumped to from Starfield.exe + 0x13FFEF0)
    text = bytearray(0x280)
    trampoline_rva = text_rva + 0xC0

    # Helper to compute 32-bit RIP-relative displacement
    def rip_disp(next_instr_off: int, target_rva: int) -> bytes:
        next_rva = text_rva + next_instr_off
        return struct.pack("<i", target_rva - next_rva)

    # Assemble DllMain at offset 0x00
    c = bytearray()
    # cmp edx, 1 (DLL_PROCESS_ATTACH)
    c += b"\x83\xFA\x01"
    # jne .ret_true (rel32)
    jne_ret_pos = len(c)
    c += b"\x0F\x85\x00\x00\x00\x00"
    # push rbx; sub rsp, 0x30 (stack aligned: 8 + 8 + 0x30 = 0x40)
    c += b"\x53\x48\x83\xEC\x30"
    # xor ecx, ecx
    c += b"\x33\xC9"
    # call QWORD PTR [rip + iat_GetModuleHandleA]
    c += b"\xFF\x15" + rip_disp(len(c) + 6, iat_rva + 0x00)
    # test rax, rax; jz .done_attach (rel32)
    c += b"\x48\x85\xC0"
    jz_done_pos = len(c)
    c += b"\x0F\x84\x00\x00\x00\x00"
    # mov rbx, rax
    c += b"\x48\x89\xC3"
    # mov QWORD PTR [rip + g_starfieldBase], rbx (data_rva + 0x00)
    c += b"\x48\x89\x1D" + rip_disp(len(c) + 7, data_rva + 0x00)
    # Verify signature at rbx + 0x13FFEF0 == 0x0510FAC5
    # mov eax, DWORD PTR [rbx + 0x13FFEF0]
    c += b"\x8B\x83\xF0\xFE\x3F\x01"
    # cmp eax, 0x0510FAC5
    c += b"\x3D\xC5\xFA\x10\x05"
    jne_done_pos = len(c)
    c += b"\x0F\x85\x00\x00\x00\x00"
    # VirtualProtect(rbx + 0x13FFEF0, 16, 0x40, &oldProt)
    # lea rcx, [rbx + 0x13FFEF0]
    c += b"\x48\x8D\x8B\xF0\xFE\x3F\x01"
    # mov edx, 16
    c += b"\xBA\x10\x00\x00\x00"
    # mov r8d, 0x40 (PAGE_EXECUTE_READWRITE)
    c += b"\x41\xB8\x40\x00\x00\x00"
    # lea r9, [rip + oldProt] (data_rva + 0x08)
    c += b"\x4C\x8D\x0D" + rip_disp(len(c) + 7, data_rva + 0x08)
    # call QWORD PTR [rip + iat_VirtualProtect]
    c += b"\xFF\x15" + rip_disp(len(c) + 6, iat_rva + 0x08)
    # Write 14-byte jump + 2 NOPs at [rbx + 0x13FFEF0]:
    # FF 25 00 00 00 00 <8-byte addr of AtmosphereEntryTrampoline> 90 90
    # lea rax, [rip + AtmosphereEntryTrampoline]
    c += b"\x48\x8D\x05" + rip_disp(len(c) + 7, trampoline_rva)
    # mov WORD PTR [rbx + 0x13FFEF0], 0x25FF
    c += b"\x66\xC7\x83\xF0\xFE\x3F\x01\xFF\x25"
    # mov DWORD PTR [rbx + 0x13FFEF2], 0
    c += b"\xC7\x83\xF2\xFE\x3F\x01\x00\x00\x00\x00"
    # mov QWORD PTR [rbx + 0x13FFEF6], rax
    c += b"\x48\x89\x83\xF6\xFE\x3F\x01"
    # mov WORD PTR [rbx + 0x13FFEFE], 0x9090
    c += b"\x66\xC7\x83\xFE\xFE\x3F\x01\x90\x90"
    # Restore protection: VirtualProtect(rbx + 0x13FFEF0, 16, oldProt, &oldProt)
    # lea rcx, [rbx + 0x13FFEF0]
    c += b"\x48\x8D\x8B\xF0\xFE\x3F\x01"
    # mov edx, 16
    c += b"\xBA\x10\x00\x00\x00"
    # mov r8d, DWORD PTR [rip + oldProt]
    c += b"\x44\x8B\x05" + rip_disp(len(c) + 7, data_rva + 0x08)
    # lea r9, [rip + oldProt]
    c += b"\x4C\x8D\x0D" + rip_disp(len(c) + 7, data_rva + 0x08)
    # call QWORD PTR [rip + iat_VirtualProtect]
    c += b"\xFF\x15" + rip_disp(len(c) + 6, iat_rva + 0x08)

    done_attach_pos = len(c)
    struct.pack_into("<i", c, jz_done_pos + 2, done_attach_pos - (jz_done_pos + 6))
    struct.pack_into("<i", c, jne_done_pos + 2, done_attach_pos - (jne_done_pos + 6))

    # add rsp, 0x30; pop rbx
    c += b"\x48\x83\xC4\x30\x5B"
    ret_true_pos = len(c)
    struct.pack_into("<i", c, jne_ret_pos + 2, ret_true_pos - (jne_ret_pos + 6))
    # mov eax, 1; ret
    c += b"\xB8\x01\x00\x00\x00\xC3"

    assert len(c) <= 0xC0, f"DllMain too large: {len(c)}"
    text[0 : len(c)] = c

    # Assemble AtmosphereEntryTrampoline at offset 0xC0
    t = bytearray()
    t_base = 0xC0

    def t_rip_disp(next_instr_len: int, target_rva: int) -> bytes:
        next_rva = text_rva + t_base + len(t) + next_instr_len
        return struct.pack("<i", target_rva - next_rva)

    # mov r10, QWORD PTR [rip + g_starfieldBase]
    t += b"\x4C\x8B\x15" + t_rip_disp(7, data_rva + 0x00)
    # Execute overwritten instructions:
    # mov eax, DWORD PTR [r10 + 0x5F29FD8]
    t += b"\x41\x8B\x82\xD8\x9F\xF2\x05"
    # mov DWORD PTR [r10 + 0x5EB0D34], eax
    t += b"\x41\x89\x82\x34\x0D\xEB\x05"
    # Check if Planet/Moon Cruise Arrival ([r10 + 0x5EAAA97] != 0)
    # cmp BYTE PTR [r10 + 0x5EAAA97], 0
    t += b"\x41\x80\xBA\x97\xAA\xEA\x05\x00"
    je_done_pos = len(t)
    t += b"\x0F\x84\x00\x00\x00\x00"
    # Clear [r10 + 0x5EAAA97] = 0 so it only triggers once per approach
    t += b"\x41\xC6\x82\x97\xAA\xEA\x05\x00"
    # Read planetBodyID = DWORD PTR [r10 + 0x5EB0714]
    t += b"\x41\x8B\x82\x14\x07\xEB\x05"
    # test eax, eax
    t += b"\x85\xC0"
    jz_done2_pos = len(t)
    t += b"\x0F\x84\x00\x00\x00\x00"

    # Save non-volatile registers & allocate 16-byte aligned stack frame
    # push rsi; push rbp; sub rsp, 0x50 (8 + 8 + 80 = 96 = 0x60 bytes)
    t += b"\x56\x55\x48\x83\xEC\x50"
    # mov ebp, eax (planetBodyID)
    t += b"\x89\xC5"
    # mov rsi, r10 (g_starfieldBase)
    t += b"\x4C\x89\xD6"
    # xor ebx, ebx (candidate coordinate index = 0)
    t += b"\x31\xDB"

    loop_pos = len(t)
    # eax = invalid sentinel [rsi + 0x61FAEB0]
    t += b"\x8B\x86\xB0\xAE\x1F\x06"
    # mov DWORD PTR [rsp + 0x30], eax (outResult = sentinel)
    t += b"\x89\x44\x24\x30"
    # lea rcx, [rsp + 0x30] (&outResult)
    t += b"\x48\x8D\x4C\x24\x30"
    # mov edx, ebp (planetBodyID)
    t += b"\x89\xEA"
    # lea r8, [rip + lonlat_rva]
    t += b"\x4C\x8D\x05" + t_rip_disp(7, lonlat_rva)
    # mov eax, ebx; shl eax, 4; add r8, rax
    t += b"\x89\xD8\xC1\xE0\x04\x49\x01\xC0"
    # mov r9d, 1 (centered = 1)
    t += b"\x41\xB9\x01\x00\x00\x00"
    # mov BYTE PTR [rsp + 0x20], 0
    t += b"\xC6\x44\x24\x20\x00"
    # mov DWORD PTR [rsp + 0x28], 1 (animated = 1)
    t += b"\xC7\x44\x24\x28\x01\x00\x00\x00"
    # lea rax, [rsi + 0x17DEC70]
    t += b"\x48\x8D\x86\x70\xEC\x7D\x01"
    # call rax
    t += b"\xFF\xD0"
    # Compare outResult with sentinel [rsi + 0x61FAEB0]
    t += b"\x8B\x86\xB0\xAE\x1F\x06"
    t += b"\x39\x44\x24\x30"
    jne_success_pos = len(t)
    t += b"\x75\x00"
    # inc ebx; cmp ebx, 6; jl loop_pos
    t += b"\xFF\xC3\x83\xFB\x06"
    jl_disp = loop_pos - (len(t) + 2)
    t += b"\x7C" + struct.pack("<b", jl_disp)
    jmp_cleanup_pos = len(t)
    t += b"\xEB\x00"

    success_pos = len(t)
    t[jne_success_pos + 1] = success_pos - (jne_success_pos + 2)
    # Valid procedural landing site found! Initiate animated atmospheric entry:
    # mov BYTE PTR [rsi + 0x5ED40C5], 1
    t += b"\xC6\x86\xC5\x40\xED\x05\x01"
    # mov cl, 1
    t += b"\xB1\x01"
    # lea rax, [rsi + 0x1897ED0]
    t += b"\x48\x8D\x86\xD0\x7E\x89\x01"
    # call rax
    t += b"\xFF\xD0"

    cleanup_pos = len(t)
    t[jmp_cleanup_pos + 1] = cleanup_pos - (jmp_cleanup_pos + 2)
    # add rsp, 0x50; pop rbp; pop rsi
    t += b"\x48\x83\xC4\x50\x5D\x5E"

    done_pos = len(t)
    struct.pack_into("<i", t, je_done_pos + 2, done_pos - (je_done_pos + 6))
    struct.pack_into("<i", t, jz_done2_pos + 2, done_pos - (jz_done2_pos + 6))

    # Original epilogue of 0x1413FFD00:
    # mov rbx, QWORD PTR [rsp + 0x40]; add rsp, 0x30; pop rdi; ret
    t += b"\x48\x8B\x5C\x24\x40\x48\x83\xC4\x30\x5F\xC3"

    text[t_base : t_base + len(t)] = t

    # --- Build .data ---
    data_sec = bytearray(0x20)

    # --- Build .reloc ---
    # One empty/dummy page relocation block at 0x1000 (8 bytes header + 4 bytes padding)
    # since all our code uses RIP-relative addressing!
    reloc_sec = struct.pack("<IIHH", 0x1000, 12, 0, 0)

    # Pad sections to file_align
    text_raw = bytes(text).ljust(align(len(text), file_align), b"\x00")
    rdata_raw = bytes(rdata).ljust(align(len(rdata), file_align), b"\x00")
    data_raw = bytes(data_sec).ljust(align(len(data_sec), file_align), b"\x00")
    reloc_raw = bytes(reloc_sec).ljust(align(len(reloc_sec), file_align), b"\x00")

    # Section file offsets
    headers_size = 0x400
    text_foff = headers_size
    rdata_foff = text_foff + len(text_raw)
    data_foff = rdata_foff + len(rdata_raw)
    reloc_foff = data_foff + len(data_raw)
    size_of_image = 0x6000

    # --- Build DOS + PE64 Headers ---
    pe = bytearray(headers_size)
    pe[0:2] = b"MZ"
    struct.pack_into("<I", pe, 0x3C, 0x80)

    pe_off = 0x80
    pe[pe_off : pe_off + 4] = b"PE\x00\x00"
    # COFF File Header (Machine=0x8664 AMD64, NumberOfSections=4, SizeOfOptionalHeader=240, Characteristics=0x2022 Executable|LargeAddressAware|DLL)
    struct.pack_into("<HHIIIHH", pe, pe_off + 4, 0x8664, 4, 0, 0, 0, 240, 0x2022)

    opt_off = pe_off + 24
    struct.pack_into(
        "<HBBIIIIIQIIHHHHHHIIIIHHQQQQII",
        pe,
        opt_off,
        0x020B,             # Magic (PE32+)
        14, 0,              # Linker version
        len(text_raw),      # SizeOfCode
        len(rdata_raw) + len(data_raw) + len(reloc_raw),  # SizeOfInitializedData
        0,                  # SizeOfUninitializedData
        text_rva,           # AddressOfEntryPoint (DllMain)
        text_rva,           # BaseOfCode
        image_base,         # ImageBase
        sec_align,          # SectionAlignment
        file_align,         # FileAlignment
        6, 0,               # OS Version
        0, 0,               # Image Version
        6, 0,               # Subsystem Version
        0,                  # Win32VersionValue
        size_of_image,      # SizeOfImage
        headers_size,       # SizeOfHeaders
        0,                  # CheckSum
        2,                  # Subsystem (WINDOWS_GUI)
        0x0160,             # DllCharacteristics (HIGH_ENTROPY_VA | DYNAMIC_BASE | NX_COMPAT)
        0x100000,           # SizeOfStackReserve
        0x1000,             # SizeOfStackCommit
        0x100000,           # SizeOfHeapReserve
        0x1000,             # SizeOfHeapCommit
        0,                  # LoaderFlags
        16,                 # NumberOfRvaAndSizes
    )

    dd_off = opt_off + 112
    # Data Directory 0: Export Table
    struct.pack_into("<II", pe, dd_off + 0 * 8, export_dir_rva, export_dir_size)
    # Data Directory 1: Import Table
    struct.pack_into("<II", pe, dd_off + 1 * 8, import_dir_rva, 40)
    # Data Directory 5: Base Relocation Table
    struct.pack_into("<II", pe, dd_off + 5 * 8, reloc_rva, len(reloc_sec))
    # Data Directory 12: IAT
    struct.pack_into("<II", pe, dd_off + 12 * 8, iat_rva, 24)

    sec_table_off = opt_off + 240
    sec_defs = [
        (b".text\x00\x00\x00", len(text), text_rva, len(text_raw), text_foff, 0x60000020),
        (b".rdata\x00\x00", len(rdata), rdata_rva, len(rdata_raw), rdata_foff, 0x40000040),
        (b".data\x00\x00\x00", len(data_sec), data_rva, len(data_raw), data_foff, 0xC0000040),
        (b".reloc\x00\x00", len(reloc_sec), reloc_rva, len(reloc_raw), reloc_foff, 0x42000040),
    ]
    for idx, (name, vsize, rva, rsize, foff, chars) in enumerate(sec_defs):
        off = sec_table_off + idx * 40
        pe[off : off + 8] = name
        struct.pack_into("<IIIIIIHHI", pe, off + 8, vsize, rva, rsize, foff, 0, 0, 0, 0, chars)

    full_dll = bytes(pe) + text_raw + rdata_raw + data_raw + reloc_raw
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(full_dll)
    print(f"Built {output_path} ({len(full_dll)} bytes, {num_exports} forwarded exports)")


if __name__ == "__main__":
    out = Path(__file__).resolve().parent.parent / "bin" / "bink2w64.dll"
    build_proxy_dll(out)
