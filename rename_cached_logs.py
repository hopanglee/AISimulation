#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
CachedLogs 폴더의 캐릭터별 로그 파일 번호를 이동시키는 스크립트
사용법: python rename_cached_logs.py [캐릭터명] [시작번호] [이동칸수] [방향]
"""

import os
import re
import sys
import shutil
from pathlib import Path

def get_log_files(character_dir):
    """캐릭터 폴더에서 로그 파일들을 가져옵니다."""
    log_files = []
    for file in os.listdir(character_dir):
        if file.endswith('.json'):
            # 파일명 패턴: 숫자_날짜시간_에이전트명_ID.json
            match = re.match(r'^(\d+)_(.+)$', file)
            if match:
                number = int(match.group(1))
                rest = match.group(2)
                log_files.append((number, file, rest))
    
    return sorted(log_files, key=lambda x: x[0])

def rename_log_files(character_dir, start_number, offset, direction='forward'):
    """로그 파일들의 번호를 이동시킵니다."""
    log_files = get_log_files(character_dir)
    
    if not log_files:
        print(f"❌ {character_dir}에서 로그 파일을 찾을 수 없습니다.")
        return
    
    print(f"📁 {character_dir}에서 {len(log_files)}개의 로그 파일을 찾았습니다.")
    
    # 이동할 파일들 필터링
    files_to_move = []
    for number, filename, rest in log_files:
        if number >= start_number:
            files_to_move.append((number, filename, rest))
    
    if not files_to_move:
        print(f"❌ 시작 번호 {start_number} 이후의 파일이 없습니다.")
        return
    
    print(f"🔄 {len(files_to_move)}개 파일을 이동합니다:")
    
    # 이동 방향에 따라 오름차순/내림차순 정렬
    if direction == 'backward':
        files_to_move.sort(key=lambda x: x[0], reverse=True)  # 큰 번호부터
    else:
        files_to_move.sort(key=lambda x: x[0])  # 작은 번호부터
    
    # 파일 이동
    moved_count = 0
    for number, filename, rest in files_to_move:
        old_path = os.path.join(character_dir, filename)
        
        if direction == 'backward':
            new_number = number - offset
        else:
            new_number = number + offset
        
        new_filename = f"{new_number:02d}_{rest}"
        new_path = os.path.join(character_dir, new_filename)
        
        # 새 파일명이 이미 존재하는지 확인
        if os.path.exists(new_path):
            print(f"⚠️  {filename} → {new_filename} (충돌: 이미 존재)")
            continue
        
        try:
            shutil.move(old_path, new_path)
            print(f"✅ {filename} → {new_filename}")
            moved_count += 1
        except Exception as e:
            print(f"❌ {filename} 이동 실패: {e}")
    
    print(f"\n🎉 {moved_count}개 파일 이동 완료!")

def main():
    if len(sys.argv) < 4:
        print("사용법: python rename_cached_logs.py [캐릭터명] [시작번호] [이동칸수] [방향]")
        print("예시:")
        print("  python rename_cached_logs.py 히노 10 5 forward   # 10번 이후 파일들을 5칸 앞으로")
        print("  python rename_cached_logs.py 카미야 5 3 backward  # 5번 이후 파일들을 3칸 뒤로")
        print("  python rename_cached_logs.py 와타야 1 10 forward  # 1번 이후 모든 파일을 10칸 앞으로")
        return
    
    character_name = sys.argv[1]
    start_number = int(sys.argv[2])
    offset = int(sys.argv[3])
    direction = sys.argv[4] if len(sys.argv) > 4 else 'forward'
    
    if direction not in ['forward', 'backward']:
        print("❌ 방향은 'forward' 또는 'backward'여야 합니다.")
        return
    
    # CachedLogs 폴더 경로
    cached_logs_dir = Path("Assets/11.GameDatas/CachedLogs")
    character_dir = cached_logs_dir / character_name
    
    if not character_dir.exists():
        print(f"❌ 캐릭터 폴더를 찾을 수 없습니다: {character_dir}")
        print("사용 가능한 캐릭터:")
        for item in cached_logs_dir.iterdir():
            if item.is_dir():
                print(f"  - {item.name}")
        return
    
    print(f"🎯 캐릭터: {character_name}")
    print(f"🎯 시작 번호: {start_number}")
    print(f"🎯 이동 칸수: {offset}")
    print(f"🎯 방향: {direction}")
    print(f"🎯 대상 폴더: {character_dir}")
    print()
    
    # 확인
    confirm = input("계속하시겠습니까? (y/N): ").strip().lower()
    if confirm not in ['y', 'yes']:
        print("취소되었습니다.")
        return
    
    rename_log_files(character_dir, start_number, offset, direction)

if __name__ == "__main__":
    main()
