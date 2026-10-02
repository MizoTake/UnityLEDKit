"""Generate original mathematical animation; no external media, fonts or audio."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import zlib
import numpy as np


def save_png(path, pixels):
    def chunk(kind, data):
        return struct.pack('!I', len(data)) + kind + data + struct.pack('!I', zlib.crc32(kind + data) & 0xffffffff)
    height, width, _ = pixels.shape
    scanlines = b''.join(b'\0' + row.tobytes() for row in pixels)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', width, height, 8, 2, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(scanlines, 6)) + chunk(b'IEND', b''))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1] / 'Assets/LEDGallery/Media')
    parser.add_argument('--encoder', default='h264_nvenc', choices=['h264_nvenc', 'libx264'])
    parser.add_argument('--still-only', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    width, height, fps, seconds = 1280, 720, 30, 12
    y, x = np.mgrid[-1:1:complex(height), -width/height:width/height:complex(width)].astype(np.float32)
    radius = np.sqrt(x*x + y*y)
    angle = np.arctan2(y, x)
    prism = np.zeros((height, width, 3), dtype=np.float32)
    for index, color in enumerate(np.array([[0.08, 1.0, 0.36], [0.24, 0.07, 1.0], [0.93, 0.75, 0.13]], dtype=np.float32)):
        band = np.exp(-((y - 0.55*np.sin(2*x + index*1.8))**2)/0.12)
        prism += band[..., None] * color * 0.7
    save_png(args.output / 'Prism.png', np.clip(prism*255, 0, 255).astype(np.uint8))
    if args.still_only:
        print('Generated original Prism.png')
        return
    video = args.output / 'NeonOrbits.mp4'
    temporary_video = Path(__file__).resolve().parents[1] / 'Temp/NeonOrbits.generated.mp4'
    temporary_video.parent.mkdir(parents=True, exist_ok=True)
    command = ['ffmpeg', '-y', '-hide_banner', '-loglevel', 'warning', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{width}x{height}', '-r', str(fps), '-i', '-', '-an', '-c:v', args.encoder]
    command += ['-gpu', '0', '-preset', 'p6', '-cq', '18', '-b:v', '0'] if args.encoder == 'h264_nvenc' else ['-preset', 'slow', '-crf', '18']
    command += ['-vf', 'scale=in_range=full:out_range=tv:out_color_matrix=bt709', '-pix_fmt', 'yuv420p', '-profile:v', 'baseline', '-bf', '0', '-color_primaries', 'bt709', '-color_trc', 'bt709', '-colorspace', 'bt709', '-color_range', 'tv', '-bsf:v', 'h264_metadata=colour_primaries=1:transfer_characteristics=1:matrix_coefficients=1:video_full_range_flag=0', '-movflags', '+faststart', str(temporary_video)]
    process = subprocess.Popen(command, stdin=subprocess.PIPE)
    palette = np.array([[0.04, 0.78, 1.0], [1.0, 0.06, 0.45], [1.0, 0.52, 0.04]], dtype=np.float32)
    for frame in range(fps * seconds):
        phase = 2*np.pi*frame/(fps*seconds)
        image = np.zeros((height, width, 3), dtype=np.float32)
        image[:] = [0.006, 0.012, 0.026]
        for index, color in enumerate(palette):
            offset = phase + index*2.0943951
            ribbon = y - 0.40*np.sin(1.5*x + offset) - 0.28*np.cos(offset)
            glow = np.exp(-ribbon*ribbon/0.032) * (0.38 + 0.62*np.cos(0.55*x - offset)**2)
            ring = np.exp(-((radius - 0.68 - 0.16*np.sin(3*angle + offset))**2)/0.006) * (0.5 + 0.5*np.sin(angle + offset))
            image += (0.78*glow + 0.42*ring)[..., None] * color
        bars = (np.abs(x) < 1.57) & (y > 0.80) & (y < 0.86)
        image[bars] = palette[(np.floor((x[bars]+1.57)/1.047).astype(int)).clip(0, 2)] * 0.72
        image *= (0.84 + 0.16*np.cos(x*0.65)*np.cos(y))[..., None]
        pixels = np.clip(image*255, 0, 255).astype(np.uint8)
        if frame == 0:
            save_png(args.output / 'NeonOrbits.png', pixels)
        process.stdin.write(pixels.tobytes())
    process.stdin.close()
    if process.wait() != 0:
        raise SystemExit('Video encoding failed; select --encoder libx264 explicitly if NVENC is unavailable.')
    temporary_video.replace(video)
    provenance = {'title': 'Neon Orbits', 'origin': 'Original procedural mathematical animation and still generated for LED Gallery', 'external_media': [], 'audio': False, 'size': [width, height], 'fps': fps, 'seconds': seconds, 'encoder': args.encoder, 'profile': 'H.264 baseline, no B frames', 'color_space': 'BT.709, limited range', 'sha256': hashlib.sha256(video.read_bytes()).hexdigest(), 'poster_sha256': hashlib.sha256((args.output / 'NeonOrbits.png').read_bytes()).hexdigest(), 'still_sha256': hashlib.sha256((args.output / 'Prism.png').read_bytes()).hexdigest(), 'generator': 'Tools/generate_sample_video.py'}
    (args.output / 'PROVENANCE.json').write_text(json.dumps(provenance, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(provenance, indent=2))


if __name__ == '__main__':
    main()
