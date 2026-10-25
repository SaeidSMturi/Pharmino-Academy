#!/usr/bin/env python3
"""
=============================================================================
PHARMINO FORENSIC VIDEO LEAK DETECTOR (Phase 2 A/B Watermark Decoder)
=============================================================================
This tool is an air-gapped, offline forensics utility for IT Administrators.
It analyzes leaked or recorded video files (MP4, MKV, TS, AVI) and extracts
the deterministically embedded 40-bit A/B segment watermark to reveal the exact
student User ID responsible for the leak.

Protocol:
- 8-bit Barker Sync Header (0xB3: 1 0 1 1 0 0 1 1)
- 24-bit Student User ID (Big-Endian)
- 8-bit Checksum ((b0 ^ b1 ^ b2 ^ 0x5A) & 0xFF)
- Total Frame: 40 bits distributed across odd segments (1, 3, 5... 79)
=============================================================================
"""

import sys
import os
import argparse
import numpy as np

# Ensure UTF-8 output on Windows consoles
if hasattr(sys.stdout, 'reconfigure'):
    try:
        sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass

# Barker Sync Header
SYNC_HEADER = 0xB3
SYNC_BITS = [1, 0, 1, 1, 0, 0, 1, 1]
TOTAL_FRAME_BITS = 40
CODE_SEGMENTS_COUNT = 40
SEGMENT_DURATION_SEC = 10.0


def compute_checksum(user_id: int) -> int:
    """Computes the 8-bit salt checksum for a 24-bit user ID."""
    b0 = user_id & 0xFF
    b1 = (user_id >> 8) & 0xFF
    b2 = (user_id >> 16) & 0xFF
    return (b0 ^ b1 ^ b2 ^ 0x5A) & 0xFF


def encode_user_bitstream(user_id: int) -> list:
    """Encodes a user ID into the 40-bit forensic bitstream."""
    bits = list(SYNC_BITS)
    for i in range(23, -1, -1):
        bits.append((user_id >> i) & 1)
    checksum = compute_checksum(user_id)
    for i in range(7, -1, -1):
        bits.append((checksum >> i) & 1)
    return bits


def decode_bitstream(bits: list) -> tuple:
    """
    Scans a sequence of extracted bits for the sync header, extracts the user ID,
    and validates the checksum.
    Returns: (is_valid, user_id, confidence_ratio)
    """
    if len(bits) < TOTAL_FRAME_BITS:
        return False, 0, 0.0

    # Search for sync header
    for offset in range(len(bits) - TOTAL_FRAME_BITS + 1):
        header_val = 0
        for i in range(8):
            header_val = (header_val << 1) | int(bits[offset + i])

        if header_val == SYNC_HEADER:
            # Extract 24-bit User ID
            extracted_uid = 0
            for i in range(24):
                extracted_uid = (extracted_uid << 1) | int(bits[offset + 8 + i])

            # Extract 8-bit Checksum
            extracted_chk = 0
            for i in range(8):
                extracted_chk = (extracted_chk << 1) | int(bits[offset + 32 + i])

            expected_chk = compute_checksum(extracted_uid)
            if extracted_chk == expected_chk:
                return True, extracted_uid, 1.0

    return False, 0, 0.0


def extract_frame_at_time(cap, target_sec: float):
    """Retrieves a video frame at a given timestamp in seconds using OpenCV."""
    cap.set(1, int(target_sec * cap.get(5)))  # CAP_PROP_POS_FRAMES = 1, CAP_PROP_FPS = 5
    ret, frame = cap.read()
    return ret, frame


def inspect_watermark_region(frame) -> tuple:
    """
    Analyzes the dual-box forensic badge region in the top-right corner:
    Outer box: x = w - 64, y = 24, w = 32, h = 32 (Darkened by 25%)
    Inner box: x = w - 60, y = 28, w = 24, h = 24 (Brightened by 30%)
    Returns: (detected_bit, confidence_score)
    """
    h, w, _ = frame.shape
    if w < 120 or h < 80:
        return 0, 0.0

    # Crop outer region and inner region
    x_outer, y_outer = w - 64, 24
    outer_crop = frame[y_outer:y_outer + 32, x_outer:x_outer + 32]
    inner_crop = frame[y_outer + 4:y_outer + 28, x_outer + 4:x_outer + 28]

    # Convert to grayscale luminance
    gray_outer = np.mean(outer_crop, axis=2)
    gray_inner = np.mean(inner_crop, axis=2)

    # Calculate contrast gradient between inner core and outer margin
    margin_mean = (np.sum(gray_outer) - np.sum(gray_inner)) / (32 * 32 - 24 * 24)
    inner_mean = np.mean(gray_inner)

    # In Variant B, the inner core is explicitly boosted relative to outer margin
    delta = inner_mean - margin_mean

    # Baseline delta for typical slide content is near 0 or negative
    is_variant_b = delta > 3.5
    confidence = min(1.0, max(0.5, abs(delta) / 10.0))

    return (1 if is_variant_b else 0), confidence


def analyze_video(video_path: str, verbose: bool = False):
    """Analyzes a video file and recovers the leaker's User ID."""
    print("=" * 65)
    print("PHARMINO FORENSIC VIDEO LEAK DETECTOR v2.0 (Phase 2)")
    print("=" * 65)
    print(f"Target Video File : {video_path}")

    if not os.path.isfile(video_path):
        print(f"[-] Error: File '{video_path}' not found.")
        sys.exit(1)

    try:
        import cv2
    except ImportError:
        print("[-] OpenCV (cv2) is required. Install via: pip install opencv-python")
        sys.exit(1)

    cap = cv2.VideoCapture(video_path)
    if not cap.isOpened():
        print(f"[-] Error: Could not open video file '{video_path}'.")
        sys.exit(1)

    fps = cap.get(cv2.CAP_PROP_FPS) or 25.0
    total_frames = cap.get(cv2.CAP_PROP_FRAME_COUNT)
    duration_sec = total_frames / fps

    print(f"Video Duration    : {int(duration_sec // 60):02d}:{int(duration_sec % 60):02d} ({duration_sec:.1f}s)")
    print(f"Framerate         : {fps:.2f} fps")
    print(f"Total Frames      : {int(total_frames)}")
    print("-" * 65)
    print("Scanning code segment intervals (Odd segments 1, 3, 5... 79)...")

    extracted_bits = []
    confidences = []

    for bit_idx in range(CODE_SEGMENTS_COUNT):
        seg_idx = 1 + (bit_idx * 2)
        target_timestamp = (seg_idx * SEGMENT_DURATION_SEC) + (SEGMENT_DURATION_SEC / 2.0)

        if target_timestamp >= duration_sec:
            if verbose:
                print(f"[!] Warning: Video ends before segment {seg_idx}. Partial decode will be attempted.")
            break

        ret, frame = extract_frame_at_time(cap, target_timestamp)
        if not ret or frame is None:
            extracted_bits.append(0)
            confidences.append(0.5)
            continue

        bit, conf = inspect_watermark_region(frame)
        extracted_bits.append(bit)
        confidences.append(conf)

        if verbose:
            ts_str = f"{int(target_timestamp // 60):02d}:{int(target_timestamp % 60):02d}"
            var_name = "Variant B (1)" if bit == 1 else "Variant A (0)"
            print(f"  [Seg {seg_idx:02d} @ {ts_str}] Extracted: {var_name} (Conf: {conf * 100:.1f}%)")

    cap.release()

    print(f"Extracted {len(extracted_bits)} bits from video.")
    print(f"Bitstream: {''.join(str(b) for b in extracted_bits)}")

    # Decode bitstream
    is_valid, user_id, _ = decode_bitstream(extracted_bits)

    print("-" * 65)
    if is_valid:
        print("[MATCH] FORENSIC MATCH CONFIRMED!")
        print(f"   * Leaker Account User ID : {user_id}")
        print(f"   * Sync Header Match     : 0x{SYNC_HEADER:02X} (Verified)")
        print(f"   * Checksum Verification : 0x{compute_checksum(user_id):02X} (Valid)")
        print(f"   * Confidence Score      : {np.mean(confidences) * 100:.1f}%")
        print("=" * 65)
        print(f"ACTION RECOMMENDATION: Suspend User ID #{user_id} and notify compliance team.")
    else:
        print("[!] Could not lock onto forensic sync header or checksum mismatch.")
        print("   Tips:")
        print("   - Ensure the leaked clip is at least 6.5 minutes long to include all 40 code segments.")
        print("   - Use --verbose to inspect segment-by-segment confidence.")
    print("=" * 65)


def run_simulation(user_id: int):
    """Simulates bitstream generation and decoding for testing/validation."""
    print("=" * 65)
    print(f"SIMULATION TEST FOR USER ID #{user_id}")
    print("=" * 65)
    bits = encode_user_bitstream(user_id)
    print(f"1. Encoded 40-Bit Frame:")
    print(f"   Sync Header (8 bits) : {''.join(str(b) for b in bits[:8])} (0x{SYNC_HEADER:02X})")
    print(f"   User ID (24 bits)    : {''.join(str(b) for b in bits[8:32])} ({user_id})")
    chk = compute_checksum(user_id)
    print(f"   Checksum (8 bits)    : {''.join(str(b) for b in bits[32:40])} (0x{chk:02X})")
    print(f"   Full Bitstream       : {''.join(str(b) for b in bits)}")

    print("\n2. Decoding Simulated Stream...")
    is_valid, decoded_uid, conf = decode_bitstream(bits)
    print(f"   Decoded Match        : {is_valid}")
    print(f"   Decoded User ID      : {decoded_uid}")
    assert is_valid and decoded_uid == user_id, "Simulation assertion failed!"
    print("[OK] Simulation SUCCESS: Bitstream encoding and decoding is 100% loss-free.")
    print("=" * 65)


def main():
    parser = argparse.ArgumentParser(
        description="Pharmino Forensic Video Leak Detector (Phase 2 A/B Watermarking)"
    )
    parser.add_argument("--input", "-i", type=str, help="Path to leaked video file to inspect")
    parser.add_argument("--verbose", "-v", action="store_true", help="Print per-segment extraction details")
    parser.add_argument("--simulate", "-s", type=int, help="Run simulation test for a specific User ID")

    args = parser.parse_args()

    if args.simulate is not None:
        run_simulation(args.simulate)
    elif args.input:
        analyze_video(args.input, args.verbose)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()
