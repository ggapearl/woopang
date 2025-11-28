#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""faster-whisper GPU 테스트 스크립트"""

import time
import torch
from faster_whisper import WhisperModel

print("=" * 60)
print("Faster-Whisper GPU 테스트")
print("=" * 60)

# GPU 확인
device = "cuda" if torch.cuda.is_available() else "cpu"
compute_type = "float16" if device == "cuda" else "int8"

print(f"\n[환경 정보]")
print(f"  Device: {device}")
print(f"  Compute Type: {compute_type}")
if device == "cuda":
    print(f"  GPU: {torch.cuda.get_device_name(0)}")
    print(f"  CUDA Version: {torch.version.cuda}")

# 비디오 파일 경로
video_path = r"c:\woopang\T28_박진수 인터뷰 공원 및 자택_20251120_FX6_part02.mp4"

print(f"\n[테스트 파일]")
print(f"  {video_path}")

# 모델 로드
print(f"\n[모델 로드]")
print(f"  모델: medium (정확도와 속도 균형)")
model_start = time.time()
model = WhisperModel("medium", device=device, compute_type=compute_type)
model_load_time = time.time() - model_start
print(f"  로드 완료: {model_load_time:.1f}초")

# 전사 시작
print(f"\n[전사 시작]")
transcribe_start = time.time()

segments, info = model.transcribe(
    video_path,
    language="ko",
    vad_filter=True,
    beam_size=5
)

# 결과 수집
result_segments = []
for seg in segments:
    result_segments.append({
        'start': seg.start,
        'end': seg.end,
        'text': seg.text
    })

transcribe_time = time.time() - transcribe_start

# 결과 출력
print(f"\n[전사 완료]")
print(f"  전사 시간: {int(transcribe_time // 60)}분 {int(transcribe_time % 60)}초")
print(f"  언어: {info.language}")
print(f"  영상 길이: {info.duration:.1f}초")
print(f"  세그먼트 수: {len(result_segments)}개")

# 처음 5개 세그먼트 출력
print(f"\n[전사 결과 샘플 (처음 5개)]")
for i, seg in enumerate(result_segments[:5], 1):
    mins = int(seg['start'] // 60)
    secs = int(seg['start'] % 60)
    timestamp = f"{mins:02d}:{secs:02d}"
    print(f"  {timestamp} - {seg['text']}")

print(f"\n[성능 분석]")
processing_speed = info.duration / transcribe_time
print(f"  처리 속도: {processing_speed:.2f}x (실시간 대비)")
print(f"  10분 영상 예상 시간: {600 / processing_speed / 60:.1f}분")

# JSON 파일로 저장
import json
output_file = r"c:\woopang\whisper_test_result.json"
result_data = {
    'language': info.language,
    'duration': info.duration,
    'segments': result_segments,
    'performance': {
        'transcribe_time_seconds': transcribe_time,
        'processing_speed': processing_speed,
        'device': device,
        'compute_type': compute_type
    }
}

with open(output_file, 'w', encoding='utf-8') as f:
    json.dump(result_data, f, ensure_ascii=False, indent=2)

print(f"\n[결과 저장]")
print(f"  파일: {output_file}")

print("\n" + "=" * 60)
print("테스트 완료!")
print("=" * 60)
