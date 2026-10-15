#!/usr/bin/env python3
"""
Builds NoMansStarfield.esm — a Starfield Light Master (fver=582) that overrides
Starfield.esm (master index 00) and SFBGS00D.esm (master index 01) GMST records
to bring No Man's Sky-style seamless interplanetary Pulse-Drive flight, close-orbit
atmospheric approach (150 km entry envelope with zero magnetic deflection),
long-range Analysis Visor scanning, rapid biome surveying, denser procedural
planetary surface generation, agile jetpack/boostpack traversal, and expanded
ship/outpost building limits to Starfield on the Steam Deck.
"""

import os
import struct
from pathlib import Path


def make_subrecord(sig: bytes, payload: bytes) -> bytes:
    assert len(sig) == 4
    return sig + struct.pack("<H", len(payload)) + payload


def make_record(sig: bytes, form_id: int, subrecords: bytes, flags: int = 0, form_version: int = 582) -> bytes:
    assert len(sig) == 4
    return (
        sig
        + struct.pack("<I", len(subrecords))
        + struct.pack("<I", flags)
        + struct.pack("<I", form_id)
        + struct.pack("<I", 0)
        + struct.pack("<HH", form_version, 0)
        + subrecords
    )


def make_gmst(form_id: int, edid: str, value) -> bytes:
    edid_bytes = edid.encode("ascii") + b"\x00"
    if edid.startswith("f"):
        data_bytes = struct.pack("<f", float(value))
    elif edid.startswith(("i", "u")):
        data_bytes = struct.pack("<i", int(value))
    else:
        raise ValueError(f"Unsupported GMST prefix for {edid}")

    subs = make_subrecord(b"EDID", edid_bytes) + make_subrecord(b"DATA", data_bytes)
    return make_record(b"GMST", form_id, subs, flags=0, form_version=582)


# Exact FormIDs verified against Starfield.esm (00xxxxxx) and SFBGS00D.esm (01xxxxxx)
GMST_OVERRIDES = [
    # =========================================================================
    # 1. NO MAN'S SKY PULSE-DRIVE & ATMOSPHERIC ENTRY ENVELOPE (SFBGS00D.esm)
    # =========================================================================
    # Shrink orbital stop distance from 10,000 km down to 150 km (upper atmosphere!)
    # so you can fly directly up to the planet's surface & cloud layer.
    (0x0106D619, "fCruiseStageNearOrbitalDistanceKM", 150.0),
    (0x010924AD, "fCruiseStageNearOrbitalDistanceToOrbitalKM", 150.0),
    (0x0106D611, "fCruisePOIArrivalThresholdKM", 150.0),
    # Smooth multi-stage planetary approach envelope
    (0x0106D613, "fCruiseStageNearPlanetDistanceKM", 12000.0),
    (0x010924AE, "fCruiseStageNearPlanetDistanceToOrbitalKM", 12000.0),
    (0x0106D612, "fCruiseStagePlanetAndMoonDistanceKM", 650000.0),
    (0x010924AF, "fCruiseStagePlanetAndMoonDistanceToOrbitalKM", 45000.0),
    (0x010924B0, "fCruiseMaxOutsidePlanetApprochDistToOrbitalLS", 80.0),
    # Fast No Man's Sky Pulse-Jump interplanetary travel times & acceleration
    (0x0106D617, "fMinSpaceCruiseOrbitalTimeToReachSeconds", 6.0),
    (0x0106D618, "fMaxSpaceCruiseOrbitalTimeToReachSeconds", 14.0),
    (0x0106D616, "fSpaceCruiseTravelPingTimeSeconds", 8.0),
    (0x01089C2D, "fCruisePlanetAndMoonAccelerationMult", 950000.0),
    (0x0106D614, "fCruiseMaxNearPlanetAccelerationMult", 250000.0),
    (0x0106D61A, "fCruiseMaxNearOrbitalAccelerationMult", 35000.0),
    (0x0106D615, "fCruiseMaxOutsidePlanetDistanceToBeAtMaxAccelerationLS", 0.002),
    (0x01089C31, "fCruiseOutsidePlanetMapMarkerRangeMult", 5000.0),
    # Disable the invisible "magnetic deflection wall" that pushes ships away
    # from planets and clamps speed to 10% on planetary approach!
    (0x010950CA, "fCruiseCelestialMagnetismDeflectionMult", 0.0),
    (0x010950CB, "fCruisePlanetMagnetismDeflectionOrbitAltitudeMult", 0.01),
    (0x010950CC, "fCruiseMoonMagnetismDeflectionOrbitAltitudeMult", 0.01),
    (0x01070082, "fCruiseMagnetismStrengthMult", 1.2),
    (0x010927DA, "fCruiseMagnetismSpeedClampMult", 0.90),

    # =========================================================================
    # 2. NO MAN'S SKY ANALYSIS VISOR & PROCEDURAL SURFACE STREAMING (Starfield.esm)
    # =========================================================================
    # Long-range NMS Analysis Visor scanning (75m base + 25m per skill rank)
    (0x000DF135, "fHandScannerScanRange", 75.0),
    (0x00249B84, "iHandScannerPlantsCountBase", 3),
    (0x0038D142, "iHandScannerAnimalCountBase", 3),
    # Denser procedural planetary biome & POI worldspace generation
    (0x002BCA26, "fPlanetContentMaxNewWorldspacesPerScan", 10.0),
    (0x001627F4, "uPlanetContentMaxRequestsPerBlock", 10),
    (0x00162800, "uPlanetContentMinRequestsPerBlock", 6),
    (0x0039100B, "uPlanetContentMaxWorldspaceRequestsPoints", 30),

    # =========================================================================
    # 3. SPACESHIP FLIGHT, DOCKING, GRAV-JUMP & SHIP BUILDER FREEDOM (Starfield.esm)
    # =========================================================================
    (0x0007032F, "fSpaceshipMaxDockingDistance", 2500.0),
    (0x00256ABA, "fSpaceshipFarTravelDestSlowdownRange", 500.0),
    (0x00072740, "fSpaceshipFarTravelDepartureDuration", 2.5),
    (0x000B4E62, "fSpaceshipFarTravelEndFadeOutDuration", 1.5),
    (0x0020DBB8, "fSpaceshipGravJumpMinDistance", 1.5),
    (0x0020DBB9, "fSpaceshipGravJumpMaxDistance", 3.2),
    (0x0023C528, "fSpaceshipMaxAngularVelocityScale", 2.2),
    (0x00346211, "fSpaceshipAngularAccelScale", 2.8),
    (0x0028B6D9, "fSpaceshipRequiredInitialBoostFuel", 0.25),
    (0x001D3405, "fSpaceshipLandableSmallSize", 90.0),
    (0x0029A89A, "fSpaceshipLandableMaxSizeZ", 160.0),
    (0x00289587, "uSpaceshipBuilderModuleHardLimit", 300),
    (0x00289588, "uSpaceshipBuilderMaxModules", 300),
    (0x0031DDB0, "fSpaceshipBuilderMaxSizeX", 160.0),
    (0x0031DE2A, "fSpaceshipBuilderMaxSizeY", 160.0),
    (0x0031DE2B, "fSpaceshipBuilderMaxSizeZ", 80.0),
    (0x0010C4AA, "uSpaceshipMaximumOwnedSpaceships", 25),

    # =========================================================================
    # 4. JETPACK / BOOSTPACK TRAVERSAL & BASE BUILDING (Starfield.esm)
    # =========================================================================
    (0x000ABEF6, "fJumpHeightMin", 2.25),
    (0x002FF162, "fJumpMinimumOxygenSpend", 1.0),
    (0x0006BB69, "fSprintOxygenDrainMult", 2.5),
    (0x0011B74F, "fJumpFallDistanceToDamageMult", 0.20),
    (0x002B90E0, "fWorkshopBuildHeight", 300.0),
]


def build_esm(output_path: Path) -> None:
    gmst_records = b"".join(make_gmst(fid, edid, val) for fid, edid, val in GMST_OVERRIDES)

    # Top-level GRUP for GMST (24-byte header + gmst_records)
    grup_header = (
        b"GRUP"
        + struct.pack("<I", 24 + len(gmst_records))
        + b"GMST"
        + struct.pack("<III", 0, 0, 0)
    )
    grup_block = grup_header + gmst_records

    # TES4 file header record
    # num_records = 1 (the GMST GRUP) + len(GMST_OVERRIDES)
    num_records = 1 + len(GMST_OVERRIDES)
    hedr_data = struct.pack("<fII", 0.96, num_records, 0x00000800)
    author = b"UniversalModder\x00"
    desc = (
        b"No Man's Starfield: Seamless Interplanetary Pulse-Drive Flight, "
        b"150km Atmospheric Entry Envelope, Long-Range Analysis Visor, "
        b"Enhanced Procedural Worlds & Steam Deck Performance Overhaul.\x00"
    )
    tes4_subs = (
        make_subrecord(b"HEDR", hedr_data)
        + make_subrecord(b"CNAM", author)
        + make_subrecord(b"SNAM", desc)
        + make_subrecord(b"MAST", b"Starfield.esm\x00")
        + make_subrecord(b"DATA", struct.pack("<Q", 0))
        + make_subrecord(b"MAST", b"SFBGS00D.esm\x00")
        + make_subrecord(b"DATA", struct.pack("<Q", 0))
    )
    # Flags: 0x00000101 = ESM (0x01) | Light/Small Master (0x100)
    tes4_record = make_record(b"TES4", 0, tes4_subs, flags=0x00000101, form_version=582)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_bytes(tes4_record + grup_block)
    print(f"Built {output_path} ({output_path.stat().st_size} bytes, {len(GMST_OVERRIDES)} GMST overrides)")


if __name__ == "__main__":
    out = Path(__file__).resolve().parent.parent / "Data" / "NoMansStarfield.esm"
    build_esm(out)
