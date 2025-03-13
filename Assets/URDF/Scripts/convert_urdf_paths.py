import os
import xml.etree.ElementTree as ET
import argparse
import re

def convert_urdf_paths(input_file, output_file):
    """
    URDF/Xacro 파일에서 모든 'package://' 경로를 상대 경로('meshes/')로 변환합니다.

    Args:
        input_file (str): 입력 URDF/Xacro 파일 경로
        output_file (str): 출력 파일 경로
    """
    # XML 파일 파싱
    tree = ET.parse(input_file)
    root = tree.getroot()

    # 모든 <mesh> 태그 찾기
    for mesh in root.findall(".//mesh"):
        filename = mesh.get("filename")
        if filename and "package://" in filename:
            # 'package://<package_name>/meshes/...' -> 'meshes/...'
            new_filename = re.sub(r"package://[^/]+/", "", filename)
            mesh.set("filename", new_filename)
            print(f"Converted: {filename} -> {new_filename}")

    # 수정된 파일 저장
    tree.write(output_file, encoding="utf-8", xml_declaration=True)
    print(f"Saved converted file to: {output_file}")

def main():
    # 명령줄 인자 파싱
    parser = argparse.ArgumentParser(description="Convert all 'package://' paths in URDF/Xacro to relative paths.")
    parser.add_argument("input_file", help="Input URDF or Xacro file path")
    parser.add_argument("output_file", help="Output file path")
    args = parser.parse_args()

    # 파일 존재 여부 확인
    if not os.path.exists(args.input_file):
        print(f"Error: Input file '{args.input_file}' does not exist.")
        return

    # 경로 변환 실행
    convert_urdf_paths(args.input_file, args.output_file)

if __name__ == "__main__":
    main()
