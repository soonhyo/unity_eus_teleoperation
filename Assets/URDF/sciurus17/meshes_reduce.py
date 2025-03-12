import os
import pymeshlab

def simplify_stl_files(directory, reduction_ratio=0.5):
    """
    지정한 디렉토리 내의 모든 STL 파일을 로드하여 정점 수를 줄인 후 저장합니다.

    :param directory: STL 파일들이 위치한 디렉토리
    :param reduction_ratio: 정점 수를 줄이는 비율 (0.0 ~ 1.0 사이)
    """
    for filename in os.listdir(directory):
        if filename.endswith(".stl"):
            filepath = os.path.join(directory, filename)

            # PyMeshLab을 사용하여 STL 로드
            ms = pymeshlab.MeshSet()
            ms.load_new_mesh(filepath)

            # 원본 정점 수
            original_vertices = ms.current_mesh().vertex_number()
            target_vertices = int(original_vertices * reduction_ratio)

            # 정점 개수 줄이기 (Simplification)
            ms.apply_filter("simplification_quadric_edge_collapse_decimation", targetfacenum=target_vertices)

            # 새로운 파일명 생성 (예: example_reduced.stl)
            new_filename = filename.replace(".stl", "_reduced.stl")
            new_filepath = os.path.join(directory, new_filename)

            # STL 저장
            ms.save_current_mesh(new_filepath)
            print(f"Saved reduced STL: {new_filename} ({original_vertices} → {target_vertices} vertices)")

# 사용 예시
directory_path = "./meshes"  # STL 파일이 들어 있는 폴더 경로
simplify_stl_files(directory_path, reduction_ratio=0.3)  # 정점을 30%로 감소
