from PIL import Image
import numpy as np
import os
import torch
import torch.nn as nn
import torchvision.models as models
import onnxruntime as ort
os.environ["KMP_DUPLICATE_LIB_OK"] = "TRUE"
# Example 이미지 경로
EXAMPLE_IMAGE_DIR = "./py/ref_data"

class GraspabilityModel(nn.Module):
    def __init__(self, feature_dim=256):
        super().__init__()
        # ResNet18 구조와 동일하게 생성
        resnet = models.resnet18(weights=models.ResNet18_Weights.IMAGENET1K_V1)
        resnet.conv1 = nn.Conv2d(3, 64, kernel_size=3, stride=1, padding=1, bias=False)
        resnet.maxpool = nn.Identity()
        self.features = nn.Sequential(*list(resnet.children())[:-1])  # (B, 512, 4, 4)
        self.flatten = nn.Flatten()
        self.fc = nn.Linear(512, feature_dim)

        # feature extractor와 fc는 freeze
        for param in self.features.parameters():
            param.requires_grad = False
        for param in self.fc.parameters():
            param.requires_grad = False

    def forward(self, x):
        x = self.features(x)
        x = self.flatten(x)
        feature_vec = self.fc(x)
        return feature_vec

def load_grasp_model(device):
    grasp_model = GraspabilityModel(feature_dim=256)
    
    feature_path = os.path.expanduser(".grasp_model.pth")
    
    if os.path.exists(feature_path):
        feature_weights = torch.load(feature_path, map_location=device)
        if isinstance(feature_weights, dict) and 'features' in feature_weights:
            grasp_model.features.load_state_dict(feature_weights['features'])
            grasp_model.fc.load_state_dict(feature_weights['fc'])
        else:
            grasp_model.load_state_dict(feature_weights, strict=False)

    grasp_model.to(device)
    grasp_model.eval()
    return grasp_model

def load_head(device):
    head_path = os.path.expanduser("./Assets/weights/grasp_head_bolt.onnx")
    
    providers = ['CPUExecutionProvider']
    if torch.cuda.is_available() and device.type == 'cuda':
        providers.insert(0, 'CUDAExecutionProvider')
        
    head_session = ort.InferenceSession(head_path, providers=providers)

    input_info = head_session.get_inputs()
    output_info = head_session.get_outputs()

    return head_session

def load_agent(device):
    # 문자열로 전달된 경우 torch.device 객체로 변환
    if isinstance(device, str):
        device = torch.device(device)

    agent_path = os.path.expanduser("./results/MyGrasp251107/My Behavior/My Behavior-7999.onnx")
    # agent_path = os.path.expanduser("./Assets/weights/My Behavior.onnx")
    
    providers = ['CPUExecutionProvider']
    if torch.cuda.is_available() and device.type == 'cuda':
        providers.insert(0, 'CUDAExecutionProvider')
        
    onnx_session = ort.InferenceSession(agent_path, providers=providers)

    input_info = onnx_session.get_inputs()
    output_info = onnx_session.get_outputs()

    return onnx_session

def run_head_inference(head_session, feature_vector):
    output_info = head_session.get_outputs()
    for output in output_info:
        print(f"Output name: {output.name}, shape: {output.shape}, type: {output.type}")

    """ONNX 모델로 graspability 추론"""
    if head_session is None:
        print("Error: head_session is None")
        return None

    if isinstance(feature_vector, torch.Tensor):
        feature_vector = feature_vector.cpu().numpy()

    if feature_vector.ndim == 1:
        feature_vector = np.expand_dims(feature_vector, axis=0)

    input_dict = {
        'feature_vec': feature_vector.astype(np.float32)
    }
    # print(f"Input dict: {input_dict}")

    try:
        outputs = head_session.run(None, input_dict)
        print(f"ONNX model outputs: {outputs}")
    except Exception as e:
        print(f"Error during ONNX inference: {e}")
        return None

    output_names = [output.name for output in head_session.get_outputs()]
    output_dict = dict(zip(output_names, outputs))

    graspability = output_dict.get('grasp_prob', None)
    if graspability is None:
        print("Error: ONNX model did not return 'output'")
        return None

    graspability = graspability.squeeze(0)
    return graspability

def run_grasp_inference(grasp_model, image_path, device):
    input_image = preprocess_image(image_path)
    input_tensor = torch.from_numpy(input_image).float().to(device)
    
    with torch.no_grad():
        feature_vectors = grasp_model(input_tensor)

    grasp_probs = run_head_inference(head, feature_vectors)
    if grasp_probs is None:
        print("Error: grasp_probs is None")
        return None, feature_vectors.cpu().numpy()
    
    return grasp_probs, feature_vectors.cpu().numpy()

def run_onnx_inference(rl_session, feature_vector):
    """ONNX 모델로 deterministic continuous actions 추론"""
    if rl_session is None:
        return None

    if isinstance(feature_vector, torch.Tensor):
        feature_vector = feature_vector.cpu().numpy()

    if feature_vector.ndim == 1:
        feature_vector = np.expand_dims(feature_vector, axis=0)

    input_dict = {
        'obs_0': feature_vector.astype(np.float32)
    }

    output_names = [output.name for output in rl_session.get_outputs()]
    outputs = rl_session.run(None, input_dict)

    output_dict = dict(zip(output_names, outputs))

    deterministic_continuous_actions = output_dict.get('continuous_actions', None)
    actions = deterministic_continuous_actions.squeeze(0) if deterministic_continuous_actions is not None else None

    return actions

# 이미지 전처리 함수 (Pillow와 NumPy 사용)
def preprocess_image(image_path):
    image = Image.open(image_path).convert("RGB")  # 이미지를 RGB로 변환
    image = image.resize((150, 150))  # 모델 입력 크기로 리사이즈
    image = np.array(image).astype(np.float32) / 255.0  # 정규화
    image = np.transpose(image, (2, 0, 1))  # (H, W, C) -> (C, H, W)
    image = np.expand_dims(image, axis=0)  # 배치 차원 추가
    return image

import matplotlib
# matplotlib.use('Agg')  # GUI 백엔드 비활성화 - 주석처리하여 화면에 띄우기
import matplotlib.pyplot as plt
import matplotlib.patches as patches
from mpl_toolkits.mplot3d import Axes3D
import math
import numpy as np

def coord_conv(rx, ry, rz):
    import numpy as np
    from scipy.spatial.transform import Rotation as R

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

    return euler_ZYZ[0], euler_ZYZ[1], euler_ZYZ[2] 

def visualize_gripper(ax, image_center, yaw_angle, gripper_size=30):
    """
    그리퍼를 시각화하는 함수
    
    Args:
        ax: matplotlib axis
        image_center: 이미지 중심 좌표 (x, y)
        yaw_angle: 그리퍼의 yaw 각도 (라디안)
        gripper_size: 그리퍼 크기
    """
    x_center, y_center = image_center
    
    # 그리퍼 jaw의 길이와 너비 (6배로 증가)
    jaw_length = gripper_size
    jaw_width = 10  # 두께 2배로 증가
    jaw_separation = 90  # 두 jaw 사이의 간격 (6배로 증가)
    
    # yaw 각도만큼 시계반대방향으로 회전 (음수 적용)
    cos_yaw = math.cos(-yaw_angle)
    sin_yaw = math.sin(-yaw_angle)
    
    # 첫 번째 jaw (왼쪽)
    jaw1_start_x = x_center - jaw_separation/2 * cos_yaw - jaw_length/2 * sin_yaw
    jaw1_start_y = y_center - jaw_separation/2 * sin_yaw + jaw_length/2 * cos_yaw
    jaw1_end_x = x_center - jaw_separation/2 * cos_yaw + jaw_length/2 * sin_yaw
    jaw1_end_y = y_center - jaw_separation/2 * sin_yaw - jaw_length/2 * cos_yaw
    
    # 두 번째 jaw (오른쪽)
    jaw2_start_x = x_center + jaw_separation/2 * cos_yaw - jaw_length/2 * sin_yaw
    jaw2_start_y = y_center + jaw_separation/2 * sin_yaw + jaw_length/2 * cos_yaw
    jaw2_end_x = x_center + jaw_separation/2 * cos_yaw + jaw_length/2 * sin_yaw
    jaw2_end_y = y_center + jaw_separation/2 * sin_yaw - jaw_length/2 * cos_yaw
    
    # jaw 그리기 (검정색으로 변경)
    ax.plot([jaw1_start_x, jaw1_end_x], [jaw1_start_y, jaw1_end_y], 
            color='black', linewidth=jaw_width, label='Gripper Jaw')
    ax.plot([jaw2_start_x, jaw2_end_x], [jaw2_start_y, jaw2_end_y], 
            color='black', linewidth=jaw_width)
    
    # 그리퍼 중심점 표시
    ax.plot(x_center, y_center, 'ro', markersize=8, label='Gripper Center')

def visualize_results(image_path, graspability, action):
    """
    이미지와 추론 결과를 시각화하는 함수
    
    Args:
        image_path: 이미지 파일 경로
        graspability: 그래스퍼빌리티 값
        action: ONNX 모델의 액션 출력 (6개 값)
    """
    # 이미지 로드
    image = Image.open(image_path).convert("RGB")
    image_array = np.array(image)
    
    # Figure 생성
    fig, ax = plt.subplots(1, 1, figsize=(10, 8))
    
    # 이미지 표시
    ax.imshow(image_array)
    ax.set_title(f"Image: {os.path.basename(image_path)}\nGraspability: {graspability:.4f}")
    
    if action is not None:
        # 액션 값 추출 (a3, a4, a5)
        x_offset, y_offset = 10 * action[0], 10 * action[2]
        a3, a4, a5 = coord_conv(30*action[3], 90*action[4]+90, 30*action[5])
        
        # 이미지 중심에 그리퍼 시각화 (a3, a5 값과 무관하게 항상 시각화)
        image_center = (image_array.shape[1] // 2 + x_offset, image_array.shape[0] // 2 + y_offset)
        yaw_angle = a4 # + 90  # a4가 yaw 각도 (라디안)
        
        # 그리퍼 시각화
        visualize_gripper(ax, image_center, yaw_angle)
        
        # 텍스트 정보 표시
        ax.text(10, 30, f"Action [a3, a4, a5]: [{a3:.3f}, {a4:.3f}, {a5:.3f}]", 
               bbox=dict(boxstyle="round,pad=0.3", facecolor="lightblue", alpha=0.7),
               fontsize=12, color='black')
        ax.text(10, 60, f"Gripper Yaw: {yaw_angle:.1f}°", 
               bbox=dict(boxstyle="round,pad=0.3", facecolor="lightgreen", alpha=0.7),
               fontsize=10, color='black')
        
    else:
        ax.text(10, 30, "No action available", 
               bbox=dict(boxstyle="round,pad=0.3", facecolor="red", alpha=0.7),
               fontsize=12, color='white')
    
    ax.axis('off')
    plt.tight_layout()
    
    # 결과 이미지 저장하지 않고 화면에 표시
    plt.show()
    print(f"Visualization displayed for: {os.path.basename(image_path)}")

# Example 이미지 처리 및 시각화
device = torch.device('cpu')
graspability_model = load_grasp_model(device)
head = load_head(device)
rl_session = load_agent(device)

for image_name in os.listdir(EXAMPLE_IMAGE_DIR):
    if image_name.lower().endswith(('.png', '.jpg', '.jpeg')):
        image_path = os.path.join(EXAMPLE_IMAGE_DIR, image_name)
        
        # 추론 실행
        graspability, feature_vector = run_grasp_inference(graspability_model, image_path, device)
        action = run_onnx_inference(rl_session, feature_vector)
        
        # 결과 출력
        print(f"Image: {image_name}")
        print(f"Graspability: {graspability}")
        if action is not None:
            print(f"Action: {action}")
            print(f"Key actions [a3, a4, a5]: [{action[3]:.3f}, {action[4]:.3f}, {action[5]:.3f}]")
        print("-" * 50)
        
        # 시각화
        visualize_results(image_path, graspability, action)

