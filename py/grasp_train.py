#import socket
#import struct
import torch
import torch.nn as nn
import torch.onnx
#import onnxruntime as ort
import numpy as np
import cv2
#from PIL import Image
#from queue import Queue
import torchvision.transforms as T
import torchvision.models as models
import threading
from threading import Thread#, Semaphore
#import nms
#import io
import os
import glob
import time
import csv
from ultralytics import YOLO    

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
save_lock = threading.Lock()

os.chdir("/home/dudrjs/BinpickingAI")
# os.chdir("D:/unityworkspace/BinpickingAI") # here
# os.chdir("C:/Users/sms2/Unity_ws/BinpickingAI_new") # here
current_dir = os.getcwd()
print(f"Current working directory: {current_dir}")

class GraspabilityModel(nn.Module):
    def __init__(self, feature_dim=256):
        super().__init__()
        # ResNet18 êµ¬ì¡°ì™€ ë™ì¼í•˜ê²Œ ìƒì„±
        resnet = models.resnet18(weights=models.ResNet18_Weights.IMAGENET1K_V1)
        resnet.conv1 = nn.Conv2d(3, 64, kernel_size=3, stride=1, padding=1, bias=False)
        resnet.maxpool = nn.Identity()
        self.features = nn.Sequential(*list(resnet.children())[:-1])  # (B, 512, 4, 4)
        self.flatten = nn.Flatten()
        self.fc = nn.Linear(512, feature_dim)
        self.output = nn.Linear(feature_dim, 1)  # ë…¸ë“œ 1ê°œì§œë¦¬ ì¶œë ¥ì¸µ ì¶”ê°€
    
        nn.init.normal_(self.output.weight, mean=0.0, std=0.01)
        nn.init.constant_(self.output.bias, 0.0)

        # feature extractorì™€ fcëŠ” freeze + í•™ìŠµ ê°€ëŠ¥í•œ layer ì„¤ì • ì–´ë–»ê²Œ í•  ê²ƒì¸ì§€?
        for param in self.features.parameters():
            param.requires_grad = False
        for param in self.fc.parameters():
            param.requires_grad = False

        # outputë§Œ í•™ìŠµ ê°€ëŠ¥
        for param in self.output.parameters():
            param.requires_grad = True

    def forward(self, x):
        x = self.features(x)
        x = self.flatten(x)
        feature_vec = self.fc(x)
        grasp_prob = torch.sigmoid(self.output(feature_vec)).squeeze(1)
        return grasp_prob, feature_vec

def clean_online_data():
    save_dir = "OnlineData"
    if not os.path.exists(save_dir):
        return
    for fname in os.listdir(save_dir):
        file_path = os.path.join(save_dir, fname)
        try:
            os.remove(file_path)
            print(f"Deleted: {file_path}")
        except Exception as e:
            print(f"Failed to delete {file_path}: {e}")

def online_learning_from_dir(buffer_size=512, batch_size=64, epochs=10, delete_size=128):
    global optimizer
    pred_dir = "OnlineData"
    loss_log_path = "loss_log.csv" 
    # ìµœê·¼ buffer_sizeê°œ ì‚¬ìš©
    img_files = sorted(
        glob.glob(os.path.join(pred_dir, "*_[01].png")),
        key=os.path.getmtime,
        reverse=True
    )[:buffer_size]
    if len(img_files) < buffer_size:
        return  # ë²„í¼ê°€ ì¶©ë¶„í•˜ì§€ ì•Šìœ¼ë©´ í•™ìŠµí•˜ì§€ ì•ŠìŒ

    images, labels = [], []
    #outputs = []
    for img_file in img_files:
        img = cv2.imread(img_file)
        if img is None:
            continue
        img = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
        img = cv2.resize(img, (150, 150))
        images.append(img)
        feedback = int(img_file.split("_")[-1].split(".")[0])
        labels.append(feedback)

        #output = float(img_file.split("_")[-2])
        #outputs.append(output)

    if not images:
        return

    images = np.stack(images)
    images = torch.from_numpy(images).permute(0, 3, 1, 2).float() / 255.0
    labels = torch.tensor(labels, dtype=torch.float32)
    #outputs = torch.tensor(outputs, dtype=torch.float32)

    images = images.to(device, non_blocking=True)
    labels = labels.to(device, non_blocking=True)
    #outputs = outputs.to(device, non_blocking=True)

    n = images.size(0)  # == buffer_size

    grasp_model.train()
    criterion = torch.nn.BCELoss()

    for epoch in range(epochs):
        perm = torch.randperm(n, device=labels.device)
        for i in range(0, n, batch_size):
            idx = perm[i:i+batch_size]
            batch_imgs = images.index_select(0, idx)
            batch_labels = labels.index_select(0, idx)
            # batch_outputs = outputs.index_select(0, idx)
            optimizer.zero_grad()
            outputs, _ = grasp_model(batch_imgs)  # outputs: (B,)
            if outputs.shape != batch_labels.shape:
                batch_labels = batch_labels.view_as(outputs)
            loss = criterion(outputs, batch_labels)
            loss.backward()
            optimizer.step()
        print(f"Epoch {epoch+1}/{epochs} finished.")
    grasp_model.eval()

    # === loss ê¸°ë¡ ===
    # loss_log.csvì— (timestamp, loss) ì €ìž¥
    with save_lock:
        import datetime
        now = datetime.datetime.now().isoformat()
        with open(loss_log_path, "a", newline="") as f:
            writer = csv.writer(f)
            writer.writerow([now, float(loss.item())])

        # === best loss ì²´í¬í¬ì¸íŠ¸ ì €ìž¥ ===
        best_loss = float("inf")
        try:
            with open(loss_log_path, "r") as f:
                reader = csv.reader(f)
                for row in reader:
                    if len(row) >= 2:
                        try:
                            l = float(row[1])
                            if l < best_loss:
                                best_loss = l
                        except:
                            continue
        except FileNotFoundError:
            best_loss = float("inf")

        os.chdir(current_dir + "/Assets/weights") # here
        if loss.item() <= best_loss:
            # FC + Output layerë§Œ ONNXë¡œ ì €ìž¥
            class FCOutputModel(nn.Module):
                def __init__(self, output_layer):
                    super().__init__()
                    self.output = output_layer
                
                def forward(self, feature_vec):
                    x = self.output(feature_vec)
                    grasp_prob = torch.sigmoid(x).squeeze(1)
                    return grasp_prob
            
            fc_output_model = FCOutputModel(grasp_model.output).eval()
            dummy_feature = torch.randn(1, 256, device=device)  # feature extractor ì¶œë ¥ í¬ê¸°
            
            torch.onnx.export(
                fc_output_model,
                dummy_feature,
                "grasp_head_bolt.onnx",
                export_params=True,
                opset_version=11,
                do_constant_folding=True,
                input_names=['feature_vec'],
                output_names=['grasp_prob'],
                dynamic_axes={'feature_vec': {0: 'batch_size'},
                             'grasp_prob': {0: 'batch_size'}}
            )
            print(f"New best loss {loss.item():.4f}, FC+Output layers saved to grasp_fc_output.onnx")
        else:
            print(f"Loss {loss.item():.4f} (best: {best_loss:.4f})")
        os.chdir(current_dir)
        for img_file in img_files[:delete_size]:
            try:
                os.remove(img_file)
            except Exception as e:
                print(f"Failed to delete {img_file}: {e}")
        
        print(f"[Online Learning] Trained on {batch_size} samples. Loss: {loss.item():.4f}")


def handle_client():
            pred_dir = "OnlineData"
            feedback_files = glob.glob(os.path.join(pred_dir, "*_0.png")) + glob.glob(os.path.join(pred_dir, "*_1.png"))
            if len(feedback_files) >= BUFFER_SIZE:
                online_learning_from_dir(buffer_size=BUFFER_SIZE, batch_size=BATCH_SIZE, epochs=EPOCHS, delete_size=DELETE_SIZE)



def worker():
    while True:
        handle_client()

NUM_WORKERS = 1

# GraspabilityModel ì´ˆê¸°í™”
grasp_model = GraspabilityModel().to(device)
BUFFER_SIZE = 256
BATCH_SIZE = 64
EPOCHS = 10
DELETE_SIZE = 128


isONNX = True 

import onnxruntime as ort
from onnx2pytorch import ConvertModel
import onnx 
script_dir = os.path.dirname(os.path.abspath(__file__))
onnx_path = os.path.join(script_dir, "..", "Assets", "weights", "graspability_feature.onnx")
onnx_model = onnx.load(onnx_path)
pytorch_model = ConvertModel(onnx_model)
for name, param in pytorch_model.named_parameters():
    print(name, param.shape)
grasp_dict = grasp_model.state_dict()
onnx_dict = pytorch_model.state_dict()
for k in onnx_dict:
    if k in grasp_dict:
        grasp_dict[k] = onnx_dict[k]
        print(f"Loaded {k} from ONNX model.")
grasp_model.load_state_dict(grasp_dict)

os.chdir(current_dir)
if isONNX:
    onnx_path = os.path.join(current_dir, "Assets", "weights", "grasp_head_bolt.onnx")
    if os.path.exists(onnx_path):
        fc_output_session = ort.InferenceSession(onnx_path)
        print(f"Loaded FC+Output layers from ONNX: {onnx_path}")
    else:
        fc_output_session = None
        print(current_dir)
        print(f"ONNX file not found: {onnx_path}")


grasp_model.eval()
optimizer = torch.optim.Adam(grasp_model.output.parameters(), lr=1e-5)  # ì¶œë ¥ì¸µë§Œ í•™ìŠµ

script_dir = os.path.dirname(os.path.abspath(__file__))
os.chdir(script_dir)

# í•™ìŠµ ì´í›„ì— ì‚­ì œí•˜ëŠ” ê²ƒìœ¼ë¡œ ë°”ê¿”ì•¼í•¨ + Online learning _from_dir í•¨ìˆ˜ ë‚´ì—ì„œ ì´ë¯¸ í•™ìŠµì— ì‚¬ìš©ëœ ë°ì´í„° ì‚­ì œí•˜ê³  ìžˆìŒ.
#clean_online_data()
global data_no

for _ in range(NUM_WORKERS):
    os.chdir(current_dir)
    Thread(target=worker, daemon=True).start()

while True: # ë©”ì¸ ìŠ¤ë ˆë“œê°€ ì¢…ë£Œë˜ì§€ ì•Šë„ë¡ ëŒ€ê¸°
    time.sleep(1)