import os
import glob
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.data import Dataset, DataLoader
import torchvision.transforms as T
from PIL import Image
import numpy as np
import torchvision.models as models
from tqdm import tqdm
# Device 설정
device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

# GraspabilityModel 정의
class GraspabilityModel(nn.Module):
    def __init__(self, feature_dim=256):
        super().__init__()
        resnet = models.resnet18(weights=models.ResNet18_Weights.IMAGENET1K_V1)
        resnet.conv1 = nn.Conv2d(3, 64, kernel_size=3, stride=1, padding=1, bias=False)
        resnet.maxpool = nn.Identity()
        self.encoder = nn.Sequential(*list(resnet.children())[:-1])  # Encoder 부분
        self.flatten = nn.Flatten()
        self.fc = nn.Linear(512, feature_dim)  # Feature 추출
        self.head = nn.Linear(feature_dim, 1)  # Graspability 예측

    def forward(self, x):
        x = self.encoder(x)
        x = self.flatten(x)
        feature_vec = self.fc(x)
        grasp_prob = torch.sigmoid(self.head(feature_vec)).squeeze(1)
        return grasp_prob, feature_vec

# Custom Dataset 정의
class GraspDataset(Dataset):
    def __init__(self, data_dir, transform=None):
        self.data_dir = data_dir
        self.transform = transform
        self.image_files = glob.glob(os.path.join(data_dir, "*.png"))

    def __len__(self):
        return len(self.image_files)

    def __getitem__(self, idx):
        file_path = self.image_files[idx]
        file_name = os.path.basename(file_path)

        # 파일 이름에서 id, graspability, label 추출
        _, graspability, label = file_name.split("_")
        label = int(label.split(".")[0])  # 0 또는 1
        graspability = float(graspability)

        # 이미지 로드 및 전처리
        image = Image.open(file_path).convert("RGB")
        if self.transform:
            image = self.transform(image)

        return image, torch.tensor(label, dtype=torch.float32), torch.tensor(graspability, dtype=torch.float32)

# 학습 함수 정의
def train_graspability_model(model, dataloader, criterion, optimizer, epochs, save_dir):
    model.train()
    for epoch in range(epochs):
        epoch_loss = 0.0
        print(f"Epoch {epoch + 1}/{epochs}")  # 현재 에포크 출력

        # tqdm으로 DataLoader 감싸기
        progress_bar = tqdm(dataloader, desc=f"Epoch {epoch + 1}/{epochs}", unit="batch")

        for images, labels, graspabilities in progress_bar:
            images = images.to(device)
            labels = labels.to(device)

            optimizer.zero_grad()
            outputs, _ = model(images)
            loss = criterion(outputs, labels)
            loss.backward()
            optimizer.step()

            epoch_loss += loss.item()

            # tqdm의 진행률 표시 업데이트
            progress_bar.set_postfix(loss=loss.item())

        print(f"Epoch {epoch + 1}/{epochs}, Loss: {epoch_loss / len(dataloader):.4f}")

    # 가중치 저장
    encoder_path = os.path.join(save_dir, "grasp_encoder.pth")
    torch.save(model.encoder.state_dict(), encoder_path)
    print(f"Encoder weights saved to {encoder_path}")

    head_path = os.path.join(save_dir, "grasp_head.pth")
    torch.save(model.head.state_dict(), head_path)
    print(f"Head weights saved to {head_path}")

# Main 함수
def main():
    # 데이터셋 경로 및 설정
    data_dir = "./OnlineData"  # 데이터셋 경로
    save_dir = "./weights"  # 가중치 저장 경로
    os.makedirs(save_dir, exist_ok=True)

    # 데이터 전처리
    transform = T.Compose([
        T.Resize((150, 150)),
        T.ToTensor(),
        T.Normalize(mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]),
    ])

    # Dataset 및 DataLoader 생성
    dataset = GraspDataset(data_dir, transform=transform)
    dataloader = DataLoader(dataset, batch_size=64, shuffle=True, num_workers=4)

    # 모델 초기화
    model = GraspabilityModel().to(device)

    # 손실 함수 및 옵티마이저 설정
    criterion = nn.BCELoss()
    optimizer = optim.Adam(model.parameters(), lr=1e-4)

    # 학습
    train_graspability_model(model, dataloader, criterion, optimizer, epochs=50, save_dir=save_dir)

if __name__ == "__main__":
    main()