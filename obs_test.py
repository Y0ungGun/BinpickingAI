from mlagents_envs.environment import UnityEnvironment
import numpy as np
import matplotlib.pyplot as plt

# ① Unity 환경 실행 (Unity Editor Play 중이면 file_name=None)
env = UnityEnvironment(file_name="build/BinpickingAI.exe")  # 또는 exe 경로 넣기: "path/to/MyEnv.exe"

# ② 환경 초기화
env.reset()

# ③ Behavior 이름 확인 (Unity에서 지정한 Behavior Name)
behavior_name = list(env.behavior_specs.keys())[0]
print("Behavior Name:", behavior_name)

# ④ Behavior Spec 확인
spec = env.behavior_specs[behavior_name]
print("Observation Specs:", spec.observation_specs)

# ⑤ 첫 Step 실행
decision_steps, terminal_steps = env.get_steps(behavior_name)

# ⑥ Observation 받아오기
obs = decision_steps.obs[0]  # 첫 번째 observation
print("Obs shape:", obs.shape)  # 예: (1, 84, 84, 3)

# ⑦ 시각화 (matplotlib)
# obs[0]은 (H, W, C) 구조. normalize 되어 있으면 [0,1], 아니면 [0,255].
image = obs[0]
# Unity는 (C, H, W) 형식으로 보낼 수 있으므로 (H, W, C)로 변환
if image.shape[0] == 3 or image.shape[0] == 1:
    image = np.transpose(image, (1, 2, 0))
if image.max() <= 1.0:
    image = (image * 255).astype(np.uint8)

plt.imshow(image)
plt.title("RenderTexture Observation")
plt.axis("off")
plt.show()

# ⑧ 환경 닫기
env.close()