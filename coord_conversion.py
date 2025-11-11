import numpy as np
from scipy.spatial.transform import Rotation as R

# --- Unity에서 사용하는 Euler 각도 (deg 단위) ---
rx, ry, rz = 1.43, 51.37, -2.87  # 예시

# --- 1. Unity(왼손좌표계, y-up) 기준 회전행렬 ---
R_LH = R.from_euler('ZYZ', [rz, ry, rx], degrees=True).as_matrix()

# --- 2. 좌표축 재배치 (Unity y-up → 현실 z-up)
T = np.array([
    [1, 0, 0],
    [0, 0, 1],
    [0, 1, 0]
])

# --- 3. 왼손 → 오른손 변환 (z축 반전)
S = np.diag([1, 1, -1])

# --- 4. 전체 변환 (좌표 재배치 + 축 반전)
R_RH = S @ (T @ R_LH @ T.T) @ S

# --- ✅ 5. 현실 좌표계 기준 Y축 180도 회전 추가 ---
R_y180 = R.from_euler('Y', 180, degrees=True).as_matrix()
R_RH = R_y180 @ R_RH

# --- 6. 오른손 좌표계의 Quaternion ---
quat_RH = R.from_matrix(R_RH).as_quat()  # [x, y, z, w]
euler_ZYZ = R.from_quat(quat_RH).as_euler('ZYZ', degrees=True)
print("Right-handed Quaternion with Y+180°:", quat_RH)
print("Right-handed Euler angles (ZYZ) with Y+180°:", euler_ZYZ)