import os
import pymeshlab

def simplify_mesh_files(directory, reduction_ratio=0.5):
    """
    지정한 디렉토리 내의 모든 STL과 DAE 파일을 로드하여 정점 수를 줄인 후 저장합니다.

    :param directory: STL/DAE 파일들이 위치한 디렉토리
    :param reduction_ratio: 정점 수를 줄이는 비율 (0.0 ~ 1.0 사이)
    """
    # 지원하는 파일 확장자
    supported_extensions = (".stl", ".dae")

    for filename in os.listdir(directory):
        if filename.lower().endswith(supported_extensions):
            filepath = os.path.join(directory, filename)

            # PyMeshLab을 사용하여 메시 로드
            ms = pymeshlab.MeshSet()
            try:
                ms.load_new_mesh(filepath)
            except Exception as e:
                print(f"Error loading {filename}: {e}")
                continue

            # 원본 정점 수
            original_vertices = ms.current_mesh().vertex_number()
            if original_vertices == 0:
                print(f"Skipping {filename}: No vertices found.")
                continue

            # 목표 정점 수 계산
            target_vertices = int(original_vertices * reduction_ratio)
            if target_vertices < 1:
                target_vertices = 1  # 최소 1개의 정점 유지

            # 정점 개수 줄이기 (Simplification)
            try:
                ms.apply_filter("simplification_quadric_edge_collapse_decimation", targetfacenum=target_vertices)
            except Exception as e:
                print(f"Error simplifying {filename}: {e}")
                continue

            # 새로운 파일명 생성 (예: example_reduced.stl 또는 example_reduced.dae)
            new_filename = filename.rsplit(".", 1)[0] + "_reduced." + filename.rsplit(".", 1)[1]
            new_filepath = os.path.join(directory, new_filename)

            # 메시 저장
            try:
                ms.save_current_mesh(new_filepath)
                print(f"Saved reduced mesh: {new_filename} ({original_vertices} → {target_vertices} vertices)")
            except Exception as e:
                print(f"Error saving {new_filename}: {e}")

# 사용 예시
directory_path = "./meshes"  # STL/DAE 파일이 들어 있는 폴더 경로
simplify_mesh_files(directory_path, reduction_ratio=0.3)  # 정점을 30%로 감소
